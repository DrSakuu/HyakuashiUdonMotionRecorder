using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        private const int PositionCurveCount = 3;
        private const int RotationCurveCount = 4;
        private const int ScaleCurveCount = 3;
        private const int RootTransformCurveCount = PositionCurveCount + RotationCurveCount + ScaleCurveCount;

        private const int HipPositionCurveStartIndex = 0;
        private const int BoneRotationCurveStartIndex = PositionCurveCount;

        private const string RootTransformPath = "";

        private static readonly string[] PositionProperties =
        {
            "localPosition.x",
            "localPosition.y",
            "localPosition.z"
        };

        private static readonly string[] RotationProperties =
        {
            "localRotation.x",
            "localRotation.y",
            "localRotation.z",
            "localRotation.w"
        };

        private static readonly string[] ScaleProperties =
        {
            "localScale.x",
            "localScale.y",
            "localScale.z"
        };

        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator)
        {
            if (take == null || animator == null || take.Frames.Count == 0)
                return null;

            var frameCount = take.Frames.Count;
            var totalCurves = PositionCurveCount + HumanTrait.BoneName.Length * RotationCurveCount;
            var keyframes = CreateKeyframeArrays(totalCurves, frameCount);

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (take.Frames[frameIndex] is not BoneRotationsFrame frame)
                    continue;

                ProcessBoneRotationsFrame(frame, keyframes, frameIndex, animator);
            }

            return CreateBoneRotationClip(keyframes, animator);
        }

        public static AnimationClip PopulateObjectClip(RecordingTake take)
        {
            if (take == null || take.Frames.Count == 0)
                return null;

            var frameCount = take.Frames.Count;
            var keyframes = CreateKeyframeArrays(RootTransformCurveCount, frameCount);

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (take.Frames[frameIndex] is not ObjectFrame frame)
                    continue;

                ProcessObjectFrame(frame, keyframes, frameIndex);
            }

            return CreateObjectClip(keyframes);
        }

        public static void SaveGenericAnimationAsset(AnimationClip clip, string animAssetPath)
        {
            if (clip == null || string.IsNullOrEmpty(animAssetPath))
                return;

            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                HumrLogger.Warning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
            }

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectCreatedAsset(clip);
        }

        private static Keyframe[][] CreateKeyframeArrays(int curveCount, int frameCount)
        {
            var keyframes = new Keyframe[curveCount][];

            for (var i = 0; i < curveCount; i++)
                keyframes[i] = new Keyframe[frameCount];

            return keyframes;
        }

        private static void ProcessObjectFrame(ObjectFrame frame, Keyframe[][] keyframes, int frameIndex)
        {
            SetVector3Keyframes(keyframes, frameIndex, frame.RecordTime, 0, frame.Position);
            SetQuaternionKeyframes(keyframes, frameIndex, frame.RecordTime, PositionCurveCount, frame.Rotation);
            SetVector3Keyframes(
                keyframes,
                frameIndex,
                frame.RecordTime,
                PositionCurveCount + RotationCurveCount,
                frame.LocalScale);
        }

        private static void ProcessBoneRotationsFrame(
            BoneRotationsFrame frame,
            Keyframe[][] keyframes,
            int frameIndex,
            Animator animator)
        {
            var localHipPosition = ToLocalHipPosition(frame.HipPosition, animator);
            SetVector3Keyframes(
                keyframes,
                frameIndex,
                frame.RecordTime,
                HipPositionCurveStartIndex,
                localHipPosition);

            ApplyWorldBoneRotations(frame, animator);
            RecordLocalBoneRotations(keyframes, frameIndex, frame.RecordTime, animator);
        }

        private static Vector3 ToLocalHipPosition(Vector3 worldHipPosition, Animator animator)
        {
            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var armatureRoot = hipsTransform != null ? hipsTransform.parent : null;

            return armatureRoot == null
                ? worldHipPosition
                : armatureRoot.InverseTransformPoint(worldHipPosition);
        }

        private static void ApplyWorldBoneRotations(BoneRotationsFrame frame, Animator animator)
        {
            var rotationCount = Mathf.Min(frame.BoneRotations.Length, HumanTrait.BoneName.Length);

            for (var boneIndex = 0; boneIndex < rotationCount; boneIndex++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                boneTransform.rotation = frame.BoneRotations[boneIndex];
            }
        }

        private static void RecordLocalBoneRotations(
            Keyframe[][] keyframes,
            int frameIndex,
            float recordTime,
            Animator animator)
        {
            for (var boneIndex = 0; boneIndex < HumanTrait.BoneName.Length; boneIndex++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                var curveStartIndex = GetBoneRotationCurveStartIndex(boneIndex);
                SetQuaternionKeyframes(
                    keyframes,
                    frameIndex,
                    recordTime,
                    curveStartIndex,
                    boneTransform.localRotation);
            }
        }

        private static AnimationClip CreateBoneRotationClip(Keyframe[][] keyframes, Animator animator)
        {
            var clip = CreateAnimationClip();

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform != null)
            {
                var hipsPath = AnimationUtility.CalculateTransformPath(hipsTransform, animator.transform);
                SetTransformCurves(clip, hipsPath, keyframes, HipPositionCurveStartIndex, PositionProperties);
            }

            for (var boneIndex = 0; boneIndex < HumanTrait.BoneName.Length; boneIndex++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                var curveStartIndex = GetBoneRotationCurveStartIndex(boneIndex);

                SetTransformCurves(clip, bonePath, keyframes, curveStartIndex, RotationProperties);
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static AnimationClip CreateObjectClip(Keyframe[][] keyframes)
        {
            var clip = CreateAnimationClip();

            SetTransformCurves(clip, RootTransformPath, keyframes, 0, PositionProperties);
            SetTransformCurves(clip, RootTransformPath, keyframes, PositionCurveCount, RotationProperties);
            SetTransformCurves(
                clip,
                RootTransformPath,
                keyframes,
                PositionCurveCount + RotationCurveCount,
                ScaleProperties);

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static AnimationClip CreateAnimationClip()
        {
            return new AnimationClip
            {
                legacy = false,
                frameRate = 60f
            };
        }

        private static void SetVector3Keyframes(
            Keyframe[][] keyframes,
            int frameIndex,
            float recordTime,
            int startIndex,
            Vector3 value)
        {
            keyframes[startIndex][frameIndex] = new Keyframe(recordTime, value.x);
            keyframes[startIndex + 1][frameIndex] = new Keyframe(recordTime, value.y);
            keyframes[startIndex + 2][frameIndex] = new Keyframe(recordTime, value.z);
        }

        private static void SetQuaternionKeyframes(
            Keyframe[][] keyframes,
            int frameIndex,
            float recordTime,
            int startIndex,
            Quaternion value)
        {
            keyframes[startIndex][frameIndex] = new Keyframe(recordTime, value.x);
            keyframes[startIndex + 1][frameIndex] = new Keyframe(recordTime, value.y);
            keyframes[startIndex + 2][frameIndex] = new Keyframe(recordTime, value.z);
            keyframes[startIndex + 3][frameIndex] = new Keyframe(recordTime, value.w);
        }

        private static void SetTransformCurves(
            AnimationClip clip,
            string transformPath,
            Keyframe[][] keyframes,
            int startIndex,
            IReadOnlyList<string> propertyNames)
        {
            for (var i = 0; i < propertyNames.Count; i++)
                clip.SetCurve(
                    transformPath,
                    typeof(Transform),
                    propertyNames[i],
                    new AnimationCurve(keyframes[startIndex + i]));
        }

        private static int GetBoneRotationCurveStartIndex(int boneIndex)
        {
            return BoneRotationCurveStartIndex + boneIndex * RotationCurveCount;
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