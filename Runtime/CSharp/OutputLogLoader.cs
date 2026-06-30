/*******
 * OutputLogLoader.cs
 * 
 * メインの処理を行う。ログ出力時と同一のアバターをHierarchy上に置き、これをアタッチして使用することを想定している
 * PackageManagerからFBXExporterをインストールしておく必要あり
 * 
 * フォルダを構成して、OutputLog_xx_xx_xxからアニメーションを作成
 * そのアニメーションをアバターのアニメーターに入れてFBXとして出力
 * FBXをHumanoidにすることでHumanoidAnimationを得られるようにしている
 * 
 * *****/

using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using UnityEngine.EventSystems;

namespace HUMR
{
#if UNITY_EDITOR
    public interface OutputLogLoaderInterface : IEventSystemHandler
    {
        void LoadLogToExportAnim();
    }

    [RequireComponent(typeof(Animator))]
    public class OutputLogLoader : MonoBehaviour, OutputLogLoaderInterface
    {
        [Tooltip("GenericAnimationを出力する場合はチェックを入れてください(チェックがないと複数のAnimationを出力できません)")]
        public bool exportGenericAnimation = true;
        [Tooltip("モーションを出力したいユーザーの名前を書いてください")]
        public string displayName = "";
        
        [HideInInspector]
        public string logFilePath = "";
        [HideInInspector]
        public int selectedIndex;
        
        private Animator _animator;
        private UnityEditor.Animations.AnimatorController _controller;
        
        private const string HumrPath = "Assets/HUMR";
        
        private string[] _files;

        private struct BoneSnapshot
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        private Vector3 _savedRootPosition;
        private Quaternion _savedRootRotation;
        private readonly List<BoneSnapshot> _avatarSnapshot = new List<BoneSnapshot>();

        public void LoadLogToExportAnim()
        {
            if (!Validate()) return;
            
            var logLines = new List<string>();
            using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                    while (0 <= sr.Peek()) logLines.Add(sr.ReadLine());

            var segments = CSharpUtilities.PartitionLogLinesIntoSegments(logLines.ToArray(), displayName);
            if (segments.Count == 0)
            {
                Debug.LogWarning($"Motion Data with [{displayName}] does not exist in {logFilePath}");
                return;
            }

            SnapshotAvatarPose();

            try
            {
                CreateDirectoryIfNotExist(HumrPath);
                SetupAnimatorController();

                var baseAnimName = CSharpUtilities.GetBaseAnimationName(logFilePath);

                for (var i = 0; i < segments.Count; i++)
                {
                    var clip = PopulateAnimationClip(segments[i]);
                    clip.name = $"{baseAnimName}_{i}";

                    SaveGenericAnimationAsset(clip, baseAnimName, i);
                    AddClipToController(clip);
                }
                
                ExportFBX(baseAnimName);
            }
            finally
            {
                RestoreAvatarPose();
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrEmpty(displayName))
            {
                CSharpUtilities.HumrWarning("DisplayName is null or empty.");
                return false;
            }
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
            return _animator != null;
        }

        private void SnapshotAvatarPose()
        {
            _savedRootPosition = transform.position;
            _savedRootRotation = transform.rotation;
            _avatarSnapshot.Clear();

            for (var i = 0; i < HumanTrait.BoneName.Length; i++)
            {
                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)i);
                if (boneTransform == null) continue;

                _avatarSnapshot.Add(new BoneSnapshot
                {
                    Transform = boneTransform,
                    LocalPosition = boneTransform.localPosition,
                    LocalRotation = boneTransform.localRotation
                });
            }
        }

        private void RestoreAvatarPose()
        {
            transform.position = _savedRootPosition;
            transform.rotation = _savedRootRotation;

            foreach (var snapshot in _avatarSnapshot)
            {
                if (snapshot.Transform == null) continue;
                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
            }
        }

        private static void CreateDirectoryIfNotExist(string path)
        {
            //存在するかどうか判定しなくても良いみたいだが気持ち悪いので
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
        }
        
        private void SetupAnimatorController()
        {
            var controllerFolderPath = $"{HumrPath}/AnimationController";
            var controllerPath = $"{controllerFolderPath}/TmpAniCon.controller";

            if (_controller == null)
            {
                CreateDirectoryIfNotExist(controllerFolderPath);
                _controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }
            else
            {
                var clearAllStates = AssetDatabase.GetAssetPath(_controller) == controllerPath;
                CleanControllerStates(clearAllStates);
            }
        }

        private void CleanControllerStates(bool clearAll)
        {
            foreach (var layer in _controller.layers)
            {
                var states = layer.stateMachine.states;
                for (var i = states.Length - 1; i >= 0; i--)
                {
                    if (clearAll || states[i].state.motion == null)
                    {
                        layer.stateMachine.RemoveState(states[i].state);
                    }
                }
            }
        }

        private void SaveGenericAnimationAsset(AnimationClip clip, string baseName, int segmentIndex)
        {
            if (!exportGenericAnimation) return;

            var animFolderPath = $"{HumrPath}/GenericAnimations/{displayName}";
            CreateDirectoryIfNotExist(animFolderPath);

            var animAssetPath = $"{animFolderPath}/{baseName}_{segmentIndex}.anim";

            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                CSharpUtilities.HumrWarning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
                CleanControllerStates(clearAll: false);
            }

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void AddClipToController(AnimationClip clip)
        {
            _controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }

        private void ExportFBX(string fileName)
        {
            _animator.runtimeAnimatorController = _controller;

            var exportFolderPath = $"{HumrPath}/FBXs/{CSharpUtilities.SanitizeFileName(displayName)}";
            CreateDirectoryIfNotExist(exportFolderPath);

            var finalPath = $"{exportFolderPath}/{fileName}";
            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(finalPath, gameObject);
        }
        
        private AnimationClip PopulateAnimationClip(MotionSegment segment)
        {
            var frameCount = segment.Frames.Count;
            var totalCurves = 3 + (HumanTrait.BoneName.Length * 4); // 3 for root position, 4 coordinates per bone rotation

            var keyframes = InitializeKeyframeArrays(totalCurves, frameCount);

            for (var frameIdx = 0; frameIdx < frameCount; frameIdx++)
            {
                var frame = segment.Frames[frameIdx];
                var localHipPos = ProcessHipPosition(frame.HipPosition);
                keyframes[0][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.x);
                keyframes[1][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.y);
                keyframes[2][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.z);
                ApplyWorldRotationsToAvatar(frame);
                RecordLocalRotationsToKeyframes(keyframes, frameIdx, frame);
            }

            return CreateAndBindCurves(keyframes);
        }

        private static Keyframe[][] InitializeKeyframeArrays(int totalCurves, int frameCount)
        {
            var keyframes = new Keyframe[totalCurves][];
            for (var i = 0; i < totalCurves; i++)
            {
                keyframes[i] = new Keyframe[frameCount];
            }
            return keyframes;
        }

        /// <summary>
        /// Converts a raw world hip position into the local space of the parent armature.
        /// </summary>
        private Vector3 ProcessHipPosition(Vector3 rawHipPos)
        {
            //TODO: fix feet sinking into the ground
            var hipTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipTransform == null || hipTransform.parent == null) return rawHipPos;
            
            var armatureParent = hipTransform.parent;
            return armatureParent.InverseTransformPoint(rawHipPos);
        }

        /// <summary>
        /// Iterates across the active avatar bones applying the pre-parsed world quaternions.
        /// </summary>
        private void ApplyWorldRotationsToAvatar(MotionFrame frame)
        {
            for (var k = 0; k < HumanTrait.BoneName.Length; k++)
            {
                // Ensure the frame contains this index
                if (k >= frame.BoneRotations.Count) break;

                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)k);
                if (boneTransform == null) continue;

                boneTransform.rotation = frame.BoneRotations[k];
            }
        }

        /// <summary>
        /// Records current frame-state local transforms down out into curve reference arrays.
        /// </summary>
        private void RecordLocalRotationsToKeyframes(Keyframe[][] keyframes, int frameIdx, MotionFrame frame)
        {
            for (var k = 0; k < HumanTrait.BoneName.Length; k++)
            {
                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)k);
                if (boneTransform == null) continue;

                var localRotation = boneTransform.localRotation;
                var curveBaseIndex = (k * 4) + 3;

                keyframes[curveBaseIndex][frameIdx]     = new Keyframe(frame.RecordTime, localRotation.x);
                keyframes[curveBaseIndex + 1][frameIdx] = new Keyframe(frame.RecordTime, localRotation.y);
                keyframes[curveBaseIndex + 2][frameIdx] = new Keyframe(frame.RecordTime, localRotation.z);
                keyframes[curveBaseIndex + 3][frameIdx] = new Keyframe(frame.RecordTime, localRotation.w);
            }
        }

        private AnimationClip CreateAndBindCurves(Keyframe[][] keyframes)
        {
            var clip = new AnimationClip();
            var hipPath = CSharpUtilities.GetHierarchyPath(_animator.GetBoneTransform(0));

            clip.SetCurve(hipPath, typeof(Transform), "localPosition.x", new AnimationCurve(keyframes[0]));
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.y", new AnimationCurve(keyframes[1]));
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.z", new AnimationCurve(keyframes[2]));

            for (var m = 0; m < HumanTrait.BoneName.Length; m++)
            {
                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)m);
                if (boneTransform == null) continue;

                var bonePath = CSharpUtilities.GetHierarchyPath(boneTransform);
                var curveBaseIndex = (m * 4) + 3;

                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", new AnimationCurve(keyframes[curveBaseIndex]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", new AnimationCurve(keyframes[curveBaseIndex + 1]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", new AnimationCurve(keyframes[curveBaseIndex + 2]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", new AnimationCurve(keyframes[curveBaseIndex + 3]));
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }
    }
#endif
}
