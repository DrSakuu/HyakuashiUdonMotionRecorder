using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        private const string RootTransformPath = "";

        public static AnimationClip PopulateHumanoidClip(RecordingTake take, Animator animator)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || !animator.isHuman || boneTake.IsEmpty)
                return null;

            var frameCount = boneTake.HipCurves[0].curve.length;

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
                    currentRootRot = new Quaternion(-currentRootRot.x, -currentRootRot.y, -currentRootRot.z,
                        -currentRootRot.w);
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

            return BuildHumanoidClip(rootPosKeys, rootRotKeys, muscleKeys);
        }

        private static AnimationClip BuildHumanoidClip(Keyframe[][] rootPosKeys, Keyframe[][] rootRotKeys,
            Keyframe[][] muscleKeys)
        {
            var clip = CreateAnimationClip();
            var animatorType = typeof(Animator);

            var posNames = new[] { "RootT.x", "RootT.y", "RootT.z" };
            var rotNames = new[] { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };

            SetCurves(clip, RootTransformPath, animatorType, posNames, rootPosKeys);
            SetCurves(clip, RootTransformPath, animatorType, rotNames, rootRotKeys);

            var muscleCount = HumanTrait.MuscleCount;
            var muscleNames = new string[muscleCount];
            for (var i = 0; i < muscleCount; i++) muscleNames[i] = GetMusclePropertyName(HumanTrait.MuscleName[i]);

            SetCurves(clip, RootTransformPath, animatorType, muscleNames, muscleKeys);

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || boneTake.IsEmpty)
                return null;

            var frameCount = boneTake.HipCurves[0].curve.length;
            var rotationCount = boneTake.BoneCurves.Length;

            ExtractWorldKeys(boneTake, out var hipKeys, out var boneKeys);

            var localHipKeys = CreateKeyframeArrays(3, frameCount);
            var localBoneKeys = new Keyframe[rotationCount][][];
            for (var i = 0; i < rotationCount; i++)
                if (boneKeys[i] != null)
                    localBoneKeys[i] = CreateKeyframeArrays(4, frameCount);

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var armatureRoot = hipsTransform != null ? hipsTransform.parent : null;

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

                    var localRot = boneTransform.localRotation;
                    localBoneKeys[boneIndex][0][frameIndex] = new Keyframe(recordTime, localRot.x);
                    localBoneKeys[boneIndex][1][frameIndex] = new Keyframe(recordTime, localRot.y);
                    localBoneKeys[boneIndex][2][frameIndex] = new Keyframe(recordTime, localRot.z);
                    localBoneKeys[boneIndex][3][frameIndex] = new Keyframe(recordTime, localRot.w);
                }
            }

            return BuildBoneRotationsClip(animator, localHipKeys, localBoneKeys);
        }

        public static AnimationClip PopulateObjectClip(RecordingTake take)
        {
            if (take is not ObjectTake objectTake || objectTake.ObjectCurves == null)
                return null;

            var clip = CreateAnimationClip();
            SetPropertyCurves(clip, RootTransformPath, objectTake.ObjectCurves);
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void SetPropertyCurves(AnimationClip clip, string transformPath, PropertyCurve[] propertyCurves)
        {
            foreach (var propertyCurve in propertyCurves)
            {
                var curve = propertyCurve.curve;
                if (curve == null || curve.length == 0) continue;

                for (var k = 0; k < curve.keys.Length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                }

                clip.SetCurve(transformPath, typeof(Transform), propertyCurve.propertyName, curve);
            }
        }

        private static AnimationClip BuildBoneRotationsClip(Animator animator, Keyframe[][] localHipKeys,
            Keyframe[][][] localBoneKeys)
        {
            var clip = CreateAnimationClip();
            var transformType = typeof(Transform);

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform != null)
            {
                var hipsPath = AnimationUtility.CalculateTransformPath(hipsTransform, animator.transform);
                var posNames = new[] { "localPosition.x", "localPosition.y", "localPosition.z" };
                SetCurves(clip, hipsPath, transformType, posNames, localHipKeys);
            }

            for (var boneIndex = 0; boneIndex < localBoneKeys.Length; boneIndex++)
            {
                if (localBoneKeys[boneIndex] == null) continue;

                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                var rotNames = new[] { "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };

                SetCurves(clip, bonePath, transformType, rotNames, localBoneKeys[boneIndex]);
            }

            clip.EnsureQuaternionContinuity();
            return clip;
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

        private static void SetCurves(AnimationClip clip, string path, Type type, string[] propertyNames,
            Keyframe[][] keyframes)
        {
            for (var i = 0; i < propertyNames.Length; i++)
            {
                if (keyframes[i] == null || keyframes[i].Length == 0) continue;

                var curve = new AnimationCurve(keyframes[i]);
                for (var k = 0; k < curve.keys.Length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                }

                clip.SetCurve(path, type, propertyNames[i], curve);
            }
        }

        private static string GetMusclePropertyName(string muscleName)
        {
            if (muscleName.StartsWith("Left ") || muscleName.StartsWith("Right "))
            {
                var fingers = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
                foreach (var finger in fingers)
                    if (muscleName.Contains(finger))
                        return muscleName
                            .Replace("Left ", "LeftHand.")
                            .Replace("Right ", "RightHand.")
                            .Replace($"{finger} ", $"{finger}.");
            }

            return muscleName;
        }

        private static Keyframe[][] CreateKeyframeArrays(int arrayCount, int frameCount)
        {
            var arrays = new Keyframe[arrayCount][];
            for (var i = 0; i < arrayCount; i++) arrays[i] = new Keyframe[frameCount];
            return arrays;
        }

        private static AnimationClip CreateAnimationClip()
        {
            return new AnimationClip
            {
                legacy = false,
                frameRate = 60f
            };
        }

        public static void SaveAnimationAsset(AnimationClip clip, string animAssetPath)
        {
            if (clip == null || string.IsNullOrEmpty(animAssetPath))
                return;

            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                Debug.LogWarning(
                    $"[Humr] Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
            }

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectCreatedAsset(clip);
        }

        private static void SelectCreatedAsset(AnimationClip clip)
        {
            EditorUtility.FocusProjectWindow();
            var createdAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GetAssetPath(clip));
            if (createdAsset == null) return;

            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
        }
    }
}