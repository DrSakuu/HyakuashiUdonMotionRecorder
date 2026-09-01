using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator)
        {
            var frameCount = take.Frames.Count;
            var totalCurves = 3 + HumanTrait.BoneName.Length * 4;

            var keyframes = InitializeKeyframeArrays(totalCurves, frameCount);
            for (var frameIdx = 0; frameIdx < frameCount; frameIdx++)
                ProcessBoneRotationsKeyframes((BoneRotationsFrame)take.Frames[frameIdx], keyframes, frameIdx, animator);

            return CreateAndBindBoneRotationCurves(keyframes, animator);
        }

        public static AnimationClip PopulateObjectClip(RecordingTake take)
        {
            var frameCount = take.Frames.Count;
            var keyframes = InitializeKeyframeArrays(10, frameCount);
            for (var frameIdx = 0; frameIdx < frameCount; frameIdx++)
                ProcessObjectKeyframes((ObjectFrame)take.Frames[frameIdx], keyframes, frameIdx);

            return CreateAndBindObjectCurves(keyframes);
        }

        private static Keyframe[][] InitializeKeyframeArrays(int totalCurves, int frameCount)
        {
            var keyframes = new Keyframe[totalCurves][];
            for (var i = 0; i < totalCurves; i++) keyframes[i] = new Keyframe[frameCount];
            return keyframes;
        }

        private static void ProcessObjectKeyframes(ObjectFrame frame, Keyframe[][] keyframes, int frameIdx)
        {
            SetKeyframes(keyframes, frameIdx, frame.RecordTime, 0,
                frame.Position.x,
                frame.Position.y,
                frame.Position.z,
                frame.Rotation.x,
                frame.Rotation.y,
                frame.Rotation.z,
                frame.Rotation.w,
                frame.LocalScale.x,
                frame.LocalScale.y,
                frame.LocalScale.z);
        }

        private static void ProcessBoneRotationsKeyframes(
            BoneRotationsFrame frame, Keyframe[][] keyframes, int frameIdx, Animator animator)
        {
            var localHipPos = ProcessHipPosition(frame.HipPosition, animator);
            SetKeyframes(keyframes, frameIdx, frame.RecordTime, 0,
                localHipPos.x,
                localHipPos.y,
                localHipPos.z);

            ApplyWorldRotationsToAvatar(frame, animator);
            RecordLocalRotationsToKeyframes(keyframes, frameIdx, frame, animator);
        }

        private static Vector3 ProcessHipPosition(Vector3 rawHipPos, Animator animator)
        {
            var hipTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipTransform == null || hipTransform.parent == null) return rawHipPos;

            var armatureParent = hipTransform.parent;
            return armatureParent.InverseTransformPoint(rawHipPos);
        }

        private static void ApplyWorldRotationsToAvatar(BoneRotationsFrame frame, Animator animator)
        {
            for (var k = 0; k < HumanTrait.BoneName.Length; k++)
            {
                if (k >= frame.BoneRotations.Count) break;

                var boneTransform = animator.GetBoneTransform((HumanBodyBones)k);
                if (boneTransform == null) continue;

                boneTransform.rotation = frame.BoneRotations[k];
            }
        }

        private static void RecordLocalRotationsToKeyframes(
            Keyframe[][] keyframes, int frameIdx, BoneRotationsFrame frame, Animator animator)
        {
            for (var k = 0; k < HumanTrait.BoneName.Length; k++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)k);
                if (boneTransform == null) continue;

                var localRotation = boneTransform.localRotation;
                var startIndex = k * 4 + 3;

                SetKeyframes(keyframes, frameIdx, frame.RecordTime, startIndex,
                    localRotation.x, localRotation.y, localRotation.z, localRotation.w);
            }
        }

        private static void SetKeyframes(
            Keyframe[][] keyframes,
            int frameIdx,
            float recordTime,
            int startIndex,
            params float[] values)
        {
            for (var i = 0; i < values.Length; i++)
                keyframes[startIndex + i][frameIdx] = new Keyframe(recordTime, values[i]);
        }

        private static AnimationClip CreateAndBindBoneRotationCurves(Keyframe[][] keyframes, Animator animator)
        {
            var clip = new AnimationClip();
            var hipTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipPath = AnimationUtility.CalculateTransformPath(hipTransform, animator.transform);

            SetTransformCurves(clip, hipPath, keyframes, 0,
                "localPosition.x",
                "localPosition.y",
                "localPosition.z");

            for (var m = 0; m < HumanTrait.BoneName.Length; m++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)m);
                if (boneTransform == null) continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                var curveBaseIndex = m * 4 + 3;

                SetTransformCurves(clip, bonePath, keyframes, curveBaseIndex,
                    "localRotation.x",
                    "localRotation.y",
                    "localRotation.z",
                    "localRotation.w");
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static AnimationClip CreateAndBindObjectCurves(Keyframe[][] keyframes)
        {
            var clip = new AnimationClip();
            const string transformPath = "";

            SetTransformCurves(clip, transformPath, keyframes, 0,
                "localPosition.x",
                "localPosition.y",
                "localPosition.z",
                "localRotation.x",
                "localRotation.y",
                "localRotation.z",
                "localRotation.w",
                "localScale.x",
                "localScale.y",
                "localScale.z");

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void SetTransformCurves(
            AnimationClip clip,
            string transformPath,
            Keyframe[][] keyframes,
            int startIndex,
            params string[] propertyNames)
        {
            for (var i = 0; i < propertyNames.Length; i++)
                clip.SetCurve(transformPath, typeof(Transform), propertyNames[i],
                    new AnimationCurve(keyframes[startIndex + i]));
        }
    }
}