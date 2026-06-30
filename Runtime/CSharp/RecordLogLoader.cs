using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HUMR
{
    [Serializable]
    public class LogEntry
    {
        public RecordingType type;
        public string name;
    }

    public interface RecordLogLoaderInterface : IEventSystemHandler
    {
        void LoadRecordingAndExportAnim();
    }

    [RequireComponent(typeof(Animator))]
    public class RecordLogLoader : MonoBehaviour, RecordLogLoaderInterface
    {
        public string logFileDirectory;
        public string[] logFilePaths;
        public string[] recordFilePaths;
        public string[] recordFileNames;
        public int recordFileIndex;
        public int recordIndex;
        public List<LogEntry> uniqueRecords = new List<LogEntry>();
        
        private Animator _animator;
        private UnityEditor.Animations.AnimatorController _controller;
        private static readonly Regex FilenameRegex = new Regex(@"^output_log_|\.txt$");

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
        
        public void CollectLogFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;

            logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            recordFilePaths = logFilePaths
                .Where(file => File.ReadLines(file).Any(line => line.Contains("-  [HUMR] ")))
                .OrderBy(file => File.GetLastWriteTime(file))
                .Reverse()
                .ToArray();
            recordFileNames = recordFilePaths
                .Select(p => FilenameRegex.Replace(Path.GetFileName(p), ""))
                .ToArray();

            var foundEntries = new HashSet<string>();
            uniqueRecords.Clear();
            foreach (var recordFile in recordFilePaths)
            {
                foreach (var line in File.ReadLines(recordFile))
                {
                    if (!line.Contains("-  [HUMR] START RECORDING")) continue;

                    var content = line.Split(new[] { "-  [HUMR] START RECORDING" }, StringSplitOptions.None)[1];
                    if (!foundEntries.Add(content)) continue;

                    var parts = content.Split(';');
                    if (parts.Length < 3) continue;

                    var typeStr = parts[1];
                    if (!Enum.TryParse<RecordingType>(typeStr, true, out var type))
                    {
                        type = RecordingType.Object;
                    }

                    uniqueRecords.Add(new LogEntry { type = type, name = parts[2] });
                }
            }
        }

        public void LoadRecordingAndExportAnim()
        {
            var recordFile = recordFilePaths[recordFileIndex];
            if (!Validate()) return;

            var logLines = new List<string>();
            using (var fs = new FileStream(recordFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
                while (0 <= sr.Peek())
                    logLines.Add(sr.ReadLine());

            var displayName = uniqueRecords[0].name;
            var segments = CSharpUtilities.PartitionLogLinesIntoSegments(logLines.ToArray(), displayName);
            if (segments.Count == 0)
            {
                Debug.LogWarning($"Motion Data with [{displayName}] does not exist in {recordFile}");
                return;
            }

            SnapshotAvatarPose();

            try
            {
                CreateDirectoryIfNotExist(HumrPath);
                SetupAnimatorController();

                var baseAnimName = CSharpUtilities.GetBaseAnimationName(recordFile);

                for (var i = 0; i < segments.Count; i++)
                {
                    var clip = PopulateAnimationClip(segments[i]);
                    clip.name = $"{baseAnimName}_{i}";

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
        
        private void AddClipToController(AnimationClip clip)
        {
            _controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }

        private void ExportFBX(string fileName)
        {
            var displayName = uniqueRecords[0].name;
            _animator.runtimeAnimatorController = _controller;

            var exportFolderPath = $"{HumrPath}/FBXs/{CSharpUtilities.SanitizeFileName(displayName)}";
            CreateDirectoryIfNotExist(exportFolderPath);

            var finalPath = $"{exportFolderPath}/{fileName}";
            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(finalPath, gameObject);
        }

        private AnimationClip PopulateAnimationClip(MotionSegment segment)
        {
            var frameCount = segment.Frames.Count;
            var totalCurves =
                3 + (HumanTrait.BoneName.Length * 4); // 3 for root position, 4 coordinates per bone rotation

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

                keyframes[curveBaseIndex][frameIdx] = new Keyframe(frame.RecordTime, localRotation.x);
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

                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x",
                    new AnimationCurve(keyframes[curveBaseIndex]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y",
                    new AnimationCurve(keyframes[curveBaseIndex + 1]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z",
                    new AnimationCurve(keyframes[curveBaseIndex + 2]));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w",
                    new AnimationCurve(keyframes[curveBaseIndex + 3]));
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }
    }
}