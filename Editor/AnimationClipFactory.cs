using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        private const string RootTransformPath = "";

        public static AnimationClip PopulateHumanoidClip(RecordingTake take, Animator animator, float frameRate)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || !animator.isHuman || boneTake.IsEmpty)
                return null;

            var frameCount = boneTake.FrameCount;

            ExtractWorldKeys(boneTake, out var hipKeys, out var boneKeys);

            var muscleCount = HumanTrait.MuscleCount;
            var muscleKeys = CreateKeyframeArrays(muscleCount, frameCount);
            var rootPosKeys = CreateKeyframeArrays(3, frameCount);
            var rootRotKeys = CreateKeyframeArrays(4, frameCount);

            var poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            var humanPose = new HumanPose();
            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var prevRootRot = Quaternion.identity;

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var recordTime = hipKeys[0][frameIndex].time;

                ApplyWorldPoseToAnimator(animator, hipsTransform, hipKeys, boneKeys, frameIndex);

                poseHandler.GetHumanPose(ref humanPose);

                // Manual EnsureQuaternionContinuity, absolute classic Unity quirk
                var currentRootRot = humanPose.bodyRotation;
                if (frameIndex == 0) prevRootRot = currentRootRot;
                if (Quaternion.Dot(prevRootRot, currentRootRot) < 0f)
                    currentRootRot = new Quaternion(
                        -currentRootRot.x, -currentRootRot.y, -currentRootRot.z, -currentRootRot.w);
                prevRootRot = currentRootRot;

                rootPosKeys[0][frameIndex] = new Keyframe(recordTime, humanPose.bodyPosition.x);
                rootPosKeys[1][frameIndex] = new Keyframe(recordTime, humanPose.bodyPosition.y);
                rootPosKeys[2][frameIndex] = new Keyframe(recordTime, humanPose.bodyPosition.z);

                rootRotKeys[0][frameIndex] = new Keyframe(recordTime, currentRootRot.x);
                rootRotKeys[1][frameIndex] = new Keyframe(recordTime, currentRootRot.y);
                rootRotKeys[2][frameIndex] = new Keyframe(recordTime, currentRootRot.z);
                rootRotKeys[3][frameIndex] = new Keyframe(recordTime, currentRootRot.w);

                for (var i = 0; i < muscleCount; i++)
                    muscleKeys[i][frameIndex] = new Keyframe(recordTime, humanPose.muscles[i]);
            }

            return BuildHumanoidClip(rootPosKeys, rootRotKeys, muscleKeys, frameRate);
        }

        public static AnimationClip PopulateObjectClip(RecordingTake take, float frameRate)
        {
            if (take is not ObjectTake objectTake || objectTake.ObjectCurves == null)
                return null;

            var clip = new AnimationClip
            {
                frameRate = frameRate
            };
            SetObjectCurves(clip, RootTransformPath, objectTake.ObjectCurves);
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator, float frameRate)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || boneTake.IsEmpty)
                return null;

            var frameCount = boneTake.FrameCount;
            var rotationCount = boneTake.BoneCurves.Length;

            ExtractWorldKeys(boneTake, out var hipKeys, out var boneKeys);

            var localHipKeys = CreateKeyframeArrays(3, frameCount);
            var localBoneKeys = new Keyframe[rotationCount][][];
            for (var i = 0; i < rotationCount; i++)
                if (boneKeys[i] != null)
                    localBoneKeys[i] = CreateKeyframeArrays(3, frameCount);

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var armatureRoot = hipsTransform != null ? hipsTransform.parent : null;
            var previousEulerAngles = new Vector3?[rotationCount];

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var recordTime = hipKeys[0][frameIndex].time;

                ApplyWorldPoseToAnimator(animator, hipsTransform, hipKeys, boneKeys, frameIndex);

                var worldHipPos = hipsTransform != null ? hipsTransform.position : Vector3.zero;
                var localHipPos = armatureRoot == null ? worldHipPos : armatureRoot.InverseTransformPoint(worldHipPos);

                localHipKeys[0][frameIndex] = new Keyframe(recordTime, localHipPos.x);
                localHipKeys[1][frameIndex] = new Keyframe(recordTime, localHipPos.y);
                localHipKeys[2][frameIndex] = new Keyframe(recordTime, localHipPos.z);

                for (var boneIndex = 0; boneIndex < rotationCount; boneIndex++)
                {
                    if (boneKeys[boneIndex] == null) continue;
                    var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                    if (boneTransform == null) continue;

                    var localEuler = boneTransform.localEulerAngles;
                    if (previousEulerAngles[boneIndex].HasValue)
                        localEuler = GetContinuousEulerAngles(previousEulerAngles[boneIndex].Value, localEuler);

                    previousEulerAngles[boneIndex] = localEuler;

                    localBoneKeys[boneIndex][0][frameIndex] = new Keyframe(recordTime, localEuler.x);
                    localBoneKeys[boneIndex][1][frameIndex] = new Keyframe(recordTime, localEuler.y);
                    localBoneKeys[boneIndex][2][frameIndex] = new Keyframe(recordTime, localEuler.z);
                }
            }

            return BuildBoneRotationsClip(animator, localHipKeys, localBoneKeys, frameRate);
        }

        public static void SaveAnimationAsset(AnimationClip clip, string animAssetPath)
        {
            if (clip == null || string.IsNullOrEmpty(animAssetPath))
                return;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.keepOriginalOrientation = true;
            settings.loopBlendOrientation = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            
            AssetDatabase.CreateAsset(clip, animAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectCreatedAsset(clip);
        }

        private static void ExtractWorldKeys(BoneRotationsTake boneTake, out Keyframe[][] hipKeys,
            out Keyframe[][][] boneKeys)
        {
            hipKeys = new[]
            {
                boneTake.HipCurves[0].curve.keys, boneTake.HipCurves[1].curve.keys, boneTake.HipCurves[2].curve.keys
            };

            var rotationCount = boneTake.BoneCurves.Length;
            boneKeys = new Keyframe[rotationCount][][];

            for (var i = 0; i < rotationCount; i++)
            {
                if (boneTake.BoneCurves[i][0].curve.length <= 0) continue;

                boneKeys[i] = new[]
                {
                    boneTake.BoneCurves[i][0].curve.keys,
                    boneTake.BoneCurves[i][1].curve.keys,
                    boneTake.BoneCurves[i][2].curve.keys,
                    boneTake.BoneCurves[i][3].curve.keys
                };
            }
        }

        private static Keyframe[][] CreateKeyframeArrays(int arrayCount, int frameCount)
        {
            var arrays = new Keyframe[arrayCount][];
            for (var i = 0; i < arrayCount; i++) arrays[i] = new Keyframe[frameCount];
            return arrays;
        }

        private static void ApplyWorldPoseToAnimator(Animator animator, Transform hips, Keyframe[][] hipKeys,
            Keyframe[][][] boneKeys, int frame)
        {
            if (hips != null)
                hips.position = new Vector3(hipKeys[0][frame].value, hipKeys[1][frame].value, hipKeys[2][frame].value);

            for (var boneIndex = 0; boneIndex < boneKeys.Length; boneIndex++)
            {
                if (boneKeys[boneIndex] == null) continue;

                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                boneTransform.rotation = new Quaternion(
                    boneKeys[boneIndex][0][frame].value,
                    boneKeys[boneIndex][1][frame].value,
                    boneKeys[boneIndex][2][frame].value,
                    boneKeys[boneIndex][3][frame].value
                );
            }
        }

        private static AnimationClip BuildHumanoidClip(Keyframe[][] rootPosKeys, Keyframe[][] rootRotKeys,
            Keyframe[][] muscleKeys, float frameRate)
        {
            var clip = new AnimationClip
            {
                frameRate = frameRate
            };
            var animatorType = typeof(Animator);

            var posNames = new[] { "RootT.x", "RootT.y", "RootT.z" };
            var rotNames = new[] { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };

            SetBoneRotationsCurves(clip, RootTransformPath, animatorType, posNames, rootPosKeys);
            SetBoneRotationsCurves(clip, RootTransformPath, animatorType, rotNames, rootRotKeys);

            var muscleCount = HumanTrait.MuscleCount;
            var muscleNames = new string[muscleCount];
            for (var i = 0; i < muscleCount; i++) muscleNames[i] = GetMusclePropertyName(HumanTrait.MuscleName[i]);

            SetBoneRotationsCurves(clip, RootTransformPath, animatorType, muscleNames, muscleKeys);

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void SetObjectCurves(
            AnimationClip clip, string transformPath, PropertyCurve[] propertyCurves)
        {
            foreach (var propertyCurve in propertyCurves)
            {
                var curve = propertyCurve.curve;
                if (curve == null || curve.length == 0) continue;

                curve = ResampleCurve(curve, clip.frameRate);
                SetLinearTangents(curve);

                clip.SetCurve(transformPath, typeof(Transform), propertyCurve.propertyName, curve);
            }
        }
        
        private static void SetBoneRotationsCurves(
            AnimationClip clip, string path, Type type, string[] propertyNames, Keyframe[][] keyframes)
        {
            for (var i = 0; i < propertyNames.Length; i++)
            {
                if (keyframes[i] == null || keyframes[i].Length == 0) continue;

                var curve = ResampleCurve(new AnimationCurve(keyframes[i]), clip.frameRate);
                SetLinearTangents(curve);

                clip.SetCurve(path, type, propertyNames[i], curve);
            }
        }
        
        private static AnimationCurve ResampleCurve(AnimationCurve source, float frameRate)
        {
            if (source == null || source.length == 0 || frameRate <= 0f)
                return source;

            var keys = source.keys;
            var startTime = keys[0].time;
            var endTime = keys[^1].time;

            if (Mathf.Approximately(startTime, endTime))
                return new AnimationCurve(new Keyframe(startTime, source.Evaluate(startTime)));

            var frameInterval = 1f / frameRate;
            var frameCount = Mathf.RoundToInt((endTime - startTime) * frameRate) + 1;
            frameCount = Mathf.Max(frameCount, 2);

            var resampledKeys = new Keyframe[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                var time = i == frameCount - 1
                    ? endTime
                    : Mathf.Min(startTime + i * frameInterval, endTime);

                resampledKeys[i] = new Keyframe(time, source.Evaluate(time));
            }

            return new AnimationCurve(resampledKeys);
        }

        private static void SetLinearTangents(AnimationCurve curve)
        {
            for (var k = 0; k < curve.keys.Length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            }
        }
        
        private static Vector3 GetContinuousEulerAngles(Vector3 previous, Vector3 current)
        {
            return new Vector3(
                previous.x + Mathf.DeltaAngle(previous.x, current.x),
                previous.y + Mathf.DeltaAngle(previous.y, current.y),
                previous.z + Mathf.DeltaAngle(previous.z, current.z)
            );
        }

        private static AnimationClip BuildBoneRotationsClip(Animator animator, Keyframe[][] localHipKeys,
            Keyframe[][][] localBoneKeys, float frameRate)
        {
            var clip = new AnimationClip
            {
                frameRate = frameRate
            };
            var transformType = typeof(Transform);

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform != null)
            {
                var hipsPath = AnimationUtility.CalculateTransformPath(hipsTransform, animator.transform);
                var posNames = new[] { "localPosition.x", "localPosition.y", "localPosition.z" };
                SetBoneRotationsCurves(clip, hipsPath, transformType, posNames, localHipKeys);
            }

            for (var boneIndex = 0; boneIndex < localBoneKeys.Length; boneIndex++)
            {
                if (localBoneKeys[boneIndex] == null) continue;

                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                var rotNames = new[] { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };

                SetBoneRotationsCurves(clip, bonePath, transformType, rotNames, localBoneKeys[boneIndex]);
            }

            return clip;
        }

        private static void SelectCreatedAsset(AnimationClip clip)
        {
            EditorUtility.FocusProjectWindow();
            var createdAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GetAssetPath(clip));
            if (createdAsset == null) return;

            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
        }

        private static string GetMusclePropertyName(string muscleName)
        {
            if (!muscleName.StartsWith("Left ") && !muscleName.StartsWith("Right ")) return muscleName;

            var fingers = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
            foreach (var finger in fingers)
                if (muscleName.Contains(finger))
                    return muscleName
                        .Replace("Left ", "LeftHand.")
                        .Replace("Right ", "RightHand.")
                        .Replace($"{finger} ", $"{finger}.");

            return muscleName;
        }
    }
}