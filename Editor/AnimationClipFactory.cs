using System;
using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        public static AnimationClip PopulateHumanoidClip(RecordingTake take, Animator animator, float frameRate)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || !animator.isHuman || boneTake.IsEmpty)
                return null;

            var rootTCurves = new PropertyCurve[]
            {
                new("RootT.x"),
                new("RootT.y"),
                new("RootT.z")
            };
            var rootQCurves = new PropertyCurve[]
            {
                new("RootQ.x"),
                new("RootQ.y"),
                new("RootQ.z"),
                new("RootQ.w")
            };
            var muscleCount = HumanTrait.MuscleCount;
            var muscleCurves = new PropertyCurve[muscleCount];
            for (var i = 0; i < muscleCount; i++)
            {
                var muscleName = GetAnimatorMuscleName(HumanTrait.MuscleName[i]);
                muscleCurves[i] = new PropertyCurve(muscleName);
            }

            var clip = InitializeAnimationClip(boneTake.length, frameRate, out var frameTimes);
            var poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            var humanPose = new HumanPose();
            var prevRootRot = Quaternion.identity;
            foreach (var frameTime in frameTimes)
            {
                SetAvatarBoneRotations(animator, boneTake, frameTime);
                poseHandler.GetHumanPose(ref humanPose);

                // Manual EnsureQuaternionContinuity, absolute classic Unity quirk
                var currentBodyRot = humanPose.bodyRotation;
                if (frameTime == 0) prevRootRot = currentBodyRot;
                if (Quaternion.Dot(prevRootRot, currentBodyRot) < 0f)
                    currentBodyRot = new Quaternion(
                        -currentBodyRot.x, -currentBodyRot.y, -currentBodyRot.z, -currentBodyRot.w);
                prevRootRot = currentBodyRot;

                rootTCurves[0].curve.AddKey(frameTime, humanPose.bodyPosition.x);
                rootTCurves[1].curve.AddKey(frameTime, humanPose.bodyPosition.y);
                rootTCurves[2].curve.AddKey(frameTime, humanPose.bodyPosition.z);

                rootQCurves[0].curve.AddKey(frameTime, currentBodyRot.x);
                rootQCurves[1].curve.AddKey(frameTime, currentBodyRot.y);
                rootQCurves[2].curve.AddKey(frameTime, currentBodyRot.z);
                rootQCurves[3].curve.AddKey(frameTime, currentBodyRot.w);

                for (var i = 0; i < muscleCount; i++)
                    muscleCurves[i].curve.AddKey(frameTime, humanPose.muscles[i]);
            }

            var animatorType = typeof(Animator);
            SetPropertyCurves(clip, string.Empty, animatorType, rootTCurves, frameTimes);
            SetPropertyCurves(clip, string.Empty, animatorType, rootQCurves, frameTimes);
            SetPropertyCurves(clip, string.Empty, animatorType, muscleCurves, frameTimes);

            return clip;
        }

        public static AnimationClip PopulateObjectClip(RecordingTake take, float frameRate)
        {
            if (take is not ObjectTake objectTake || objectTake.ObjectCurves == null)
                return null;

            var clip = InitializeAnimationClip(objectTake.length, frameRate, out var frameTimes);
            SetPropertyCurves(clip, string.Empty, typeof(Transform), objectTake.ObjectCurves, frameTimes);
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator, float frameRate)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || boneTake.IsEmpty)
                return null;

            var localHipCurves = new PropertyCurve[]
            {
                new("localPosition.x"),
                new("localPosition.y"),
                new("localPosition.z")
            };

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipsPath = AnimationUtility.CalculateTransformPath(hipsTransform, animator.transform);
            var boneCount = HumanTrait.BoneCount;
            var localBoneCurves = new PropertyCurve[boneCount][];
            for (var i = 0; i < boneCount; i++)
                localBoneCurves[i] = new[]
                {
                    new PropertyCurve("localRotation.x"),
                    new PropertyCurve("localRotation.y"),
                    new PropertyCurve("localRotation.z"),
                    new PropertyCurve("localRotation.w")
                };
            var clip = InitializeAnimationClip(boneTake.length, frameRate, out var frameTimes);

            foreach (var frameTime in frameTimes)
            {
                SetAvatarBoneRotations(animator, boneTake, frameTime);

                localHipCurves[0].curve.AddKey(frameTime, hipsTransform.localPosition.x);
                localHipCurves[1].curve.AddKey(frameTime, hipsTransform.localPosition.y);
                localHipCurves[2].curve.AddKey(frameTime, hipsTransform.localPosition.z);

                for (var i = 0; i < localBoneCurves.Length; i++)
                {
                    var boneTransform = animator.GetBoneTransform((HumanBodyBones)i);
                    if (boneTransform == null)
                        continue;
                    localBoneCurves[i][0].curve.AddKey(frameTime, boneTransform.localRotation.x);
                    localBoneCurves[i][1].curve.AddKey(frameTime, boneTransform.localRotation.y);
                    localBoneCurves[i][2].curve.AddKey(frameTime, boneTransform.localRotation.z);
                    localBoneCurves[i][3].curve.AddKey(frameTime, boneTransform.localRotation.w);
                }
            }

            SetPropertyCurves(clip, hipsPath, typeof(Transform), localHipCurves, frameTimes);
            for (var index = 0; index < localBoneCurves.Length; index++)
            {
                var boneRotationCurves = localBoneCurves[index];
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)index);
                if (boneTransform == null)
                    continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                SetPropertyCurves(clip, bonePath, typeof(Transform), boneRotationCurves, frameTimes);
            }

            clip.EnsureQuaternionContinuity();
            return clip;
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

        private static string GetAnimatorMuscleName(string muscleName)
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

        private static AnimationClip InitializeAnimationClip(float length, float frameRate, out float[] frameTimes)
        {
            var clip = new AnimationClip
            {
                frameRate = frameRate
            };
            frameTimes = ResampleFrameTimes(length, clip.frameRate);
            return clip;
        }

        private static void SetAvatarBoneRotations(Animator animator, BoneRotationsTake boneTake, float frameTime)
        {
            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipCurveXValue = boneTake.HipCurves[0].curve.Evaluate(frameTime);
            var hipCurveYValue = boneTake.HipCurves[1].curve.Evaluate(frameTime);
            var hipCurveZValue = boneTake.HipCurves[2].curve.Evaluate(frameTime);

            if (hipsTransform != null)
                hipsTransform.position = new Vector3(hipCurveXValue, hipCurveYValue, hipCurveZValue);

            for (var boneIndex = 0; boneIndex < boneTake.BoneCurves.Length; boneIndex++)
            {
                var boneRotationCurves = boneTake.BoneCurves[boneIndex];
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null)
                    continue;

                boneTransform.rotation = new Quaternion(
                    boneRotationCurves[0].curve.Evaluate(frameTime),
                    boneRotationCurves[1].curve.Evaluate(frameTime),
                    boneRotationCurves[2].curve.Evaluate(frameTime),
                    boneRotationCurves[3].curve.Evaluate(frameTime)
                );
            }
        }

        private static void SetPropertyCurves(AnimationClip clip, string path, Type type, PropertyCurve[] propCurves,
            float[] frameTimes)
        {
            foreach (var propertyCurve in propCurves)
            {
                var curve = propertyCurve.curve;
                if (curve == null || curve.length == 0) continue;

                curve = ResampleCurve(curve, frameTimes);
                SetLinearTangents(curve);

                clip.SetCurve(path, type, propertyCurve.propertyName, curve);
            }
        }

        private static void SelectCreatedAsset(AnimationClip clip)
        {
            EditorUtility.FocusProjectWindow();
            var createdAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GetAssetPath(clip));
            if (createdAsset == null) return;

            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
        }

        private static float[] ResampleFrameTimes(float length, float frameRate)
        {
            if (length <= 0f || frameRate <= 0f)
                return new float[0];

            var startTime = 0f;
            var endTime = length;

            if (Mathf.Approximately(startTime, endTime))
                return new[] { startTime };

            var frameInterval = 1f / frameRate;
            var frameCount = Mathf.RoundToInt((endTime - startTime) * frameRate) + 1;
            frameCount = Mathf.Max(frameCount, 2);

            var resampledTimes = new float[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                var time = i == frameCount - 1
                    ? endTime
                    : Mathf.Min(startTime + i * frameInterval, endTime);

                resampledTimes[i] = time;
            }

            return resampledTimes;
        }

        private static AnimationCurve ResampleCurve(AnimationCurve source, float[] frameTimes)
        {
            if (source == null || source.length == 0 || frameTimes == null || frameTimes.Length == 0)
                return source;

            var resampledCurve = new AnimationCurve();
            foreach (var frameTime in frameTimes) resampledCurve.AddKey(frameTime, source.Evaluate(frameTime));
            return resampledCurve;
        }

        private static void SetLinearTangents(AnimationCurve curve)
        {
            for (var k = 0; k < curve.keys.Length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            }
        }
    }
}