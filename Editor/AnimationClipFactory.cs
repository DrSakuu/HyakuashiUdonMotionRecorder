using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    public static class AnimationClipFactory
    {
        private const string RootTransformPath = "";

        public static AnimationClip PopulateBoneRotationsClip(RecordingTake take, Animator animator)
        {
            if (take is not BoneRotationsTake boneTake || animator == null || boneTake.IsEmpty)
                return null;

            var frameCount = boneTake.HipCurves[0].curve.length;
            var rotationCount = boneTake.BoneCurves.Length;

            // Extract all key arrays upfront for rapid batch modification without memory allocations
            var hipKeysX = boneTake.HipCurves[0].curve.keys;
            var hipKeysY = boneTake.HipCurves[1].curve.keys;
            var hipKeysZ = boneTake.HipCurves[2].curve.keys;

            var boneKeys = new Keyframe[rotationCount][][];
            for (var i = 0; i < rotationCount; i++)
            {
                if (boneTake.BoneCurves[i][0].curve.length <= 0) continue;
                
                boneKeys[i] = new Keyframe[4][];
                boneKeys[i][0] = boneTake.BoneCurves[i][0].curve.keys;
                boneKeys[i][1] = boneTake.BoneCurves[i][1].curve.keys;
                boneKeys[i][2] = boneTake.BoneCurves[i][2].curve.keys;
                boneKeys[i][3] = boneTake.BoneCurves[i][3].curve.keys;
            }

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                // 1. Convert world space values from curves and apply to Animator
                var worldHipPos = new Vector3(
                    hipKeysX[frameIndex].value,
                    hipKeysY[frameIndex].value,
                    hipKeysZ[frameIndex].value
                );

                for (var boneIndex = 0; boneIndex < rotationCount; boneIndex++)
                {
                    if (boneKeys[boneIndex] == null) continue;
                    
                    var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                    if (boneTransform == null) continue;

                    var worldRot = new Quaternion(
                        boneKeys[boneIndex][0][frameIndex].value,
                        boneKeys[boneIndex][1][frameIndex].value,
                        boneKeys[boneIndex][2][frameIndex].value,
                        boneKeys[boneIndex][3][frameIndex].value
                    );
                    
                    boneTransform.rotation = worldRot;
                }

                // 2. Read back local space values and overwrite keys in place
                var localHipPos = ToLocalHipPosition(worldHipPos, animator);
                hipKeysX[frameIndex].value = localHipPos.x;
                hipKeysY[frameIndex].value = localHipPos.y;
                hipKeysZ[frameIndex].value = localHipPos.z;

                for (var boneIndex = 0; boneIndex < rotationCount; boneIndex++)
                {
                    if (boneKeys[boneIndex] == null) continue;
                    
                    var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                    if (boneTransform == null) continue;

                    var localRot = boneTransform.localRotation;
                    boneKeys[boneIndex][0][frameIndex].value = localRot.x;
                    boneKeys[boneIndex][1][frameIndex].value = localRot.y;
                    boneKeys[boneIndex][2][frameIndex].value = localRot.z;
                    boneKeys[boneIndex][3][frameIndex].value = localRot.w;
                }
            }

            // 3. Re-assign updated keyframes back to the curves
            boneTake.HipCurves[0].curve.keys = hipKeysX;
            boneTake.HipCurves[1].curve.keys = hipKeysY;
            boneTake.HipCurves[2].curve.keys = hipKeysZ;

            for (var i = 0; i < rotationCount; i++)
            {
                if (boneKeys[i] == null) continue;
                
                boneTake.BoneCurves[i][0].curve.keys = boneKeys[i][0];
                boneTake.BoneCurves[i][1].curve.keys = boneKeys[i][1];
                boneTake.BoneCurves[i][2].curve.keys = boneKeys[i][2];
                boneTake.BoneCurves[i][3].curve.keys = boneKeys[i][3];
            }

            return CreateBoneRotationClip(boneTake, animator);
        }

        private static AnimationClip CreateBoneRotationClip(BoneRotationsTake take, Animator animator)
        {
            var clip = CreateAnimationClip();

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform != null)
            {
                var hipsPath = AnimationUtility.CalculateTransformPath(hipsTransform, animator.transform);
                SetPropertyCurves(clip, hipsPath, take.HipCurves);
            }

            for (var boneIndex = 0; boneIndex < take.BoneCurves.Length; boneIndex++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)boneIndex);
                if (boneTransform == null) continue;

                var bonePath = AnimationUtility.CalculateTransformPath(boneTransform, animator.transform);
                SetPropertyCurves(clip, bonePath, take.BoneCurves[boneIndex]);
            }

            clip.EnsureQuaternionContinuity();
            return clip;
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

        private static AnimationClip CreateAnimationClip()
        {
            return new AnimationClip
            {
                legacy = false,
                frameRate = 60f
            };
        }

        private static Vector3 ToLocalHipPosition(Vector3 worldHipPosition, Animator animator)
        {
            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            var armatureRoot = hipsTransform != null ? hipsTransform.parent : null;

            return armatureRoot == null
                ? worldHipPosition
                : armatureRoot.InverseTransformPoint(worldHipPosition);
        }

        public static void SaveAnimationAsset(AnimationClip clip, string animAssetPath)
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