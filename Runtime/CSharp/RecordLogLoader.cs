using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HUMR
{
    [Serializable]
    public class RecordFileEntry
    {
        public string path;
        public LogFileType type;
    }
    
    [Serializable]
    public class LogEntry
    {
        public string name;
        public RecordingType type;
    }

    public interface RecordLogLoaderInterface : IEventSystemHandler
    {
        void LoadRecordingAndExportAnim();
    }

    // Editor components prevent world build 
#if UNITY_EDITOR
    [RequireComponent(typeof(Animator))]
    public class RecordLogLoader : MonoBehaviour, RecordLogLoaderInterface
    {
        public string logFileDirectory;
        public string[] logFilePaths;
        
        public List<RecordFileEntry> recordFiles = new List<RecordFileEntry>();
        public string[] recordFileNames;
        public int recordFileIndex;
        public List<LogEntry> recordings = new List<LogEntry>();
        public int recordingIndex;
        public bool exportGenericAnimation;
        
        private Animator _animator;
        private UnityEditor.Animations.AnimatorController _controller;
        private static readonly Regex LogFileRegex = new Regex(@"^output_log_|\.txt$");
        
        private string _displayName;

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
            
            var discoveredEntries = new List<RecordFileEntry>();

            foreach (var file in logFilePaths)
            {
                var isStandard = false;
                var isLegacy = false;

                foreach (var line in File.ReadLines(file))
                {
                    if (line.Contains(HumrUtils.LogMatchTarget)) isStandard = true;
                    if (line.Contains(HumrUtils.LegacyLogMatchTarget)) isLegacy = true;
                    if (isStandard || isLegacy) break; 
                }

                if (isStandard)
                {
                    discoveredEntries.Add(new RecordFileEntry { path = file, type = LogFileType.Standard });
                }
                else if (isLegacy)
                {
                    discoveredEntries.Add(new RecordFileEntry { path = file, type = LogFileType.Legacy });
                }
            }

            recordFiles = discoveredEntries
                .OrderByDescending(e => File.GetLastWriteTime(e.path))
                .ToList();

            recordFileNames = recordFiles
                .Select(e => LogFileRegex.Replace(Path.GetFileName(e.path), ""))
                .ToArray();
        }

        public void CollectRecordings()
        {
            if (recordFileIndex < 0 || recordFileIndex >= recordFiles.Count) return;

            var currentFile = recordFiles[recordFileIndex];
            recordings.Clear();

            if (currentFile.type == LogFileType.Legacy)
            {
                var matchTarget = HumrUtils.LegacyLogMatchTarget;
                
                foreach (var line in File.ReadLines(currentFile.path))
                {
                    int prefixIdx = line.IndexOf(matchTarget);
                    if (prefixIdx == -1) continue;

                    string dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();
                    
                    int digitIdx = -1;
                    for (int i = 0; i < dataSegment.Length; i++)
                    {
                        if (char.IsDigit(dataSegment[i]))
                        {
                            digitIdx = i;
                            break;
                        }
                    }

                    if (digitIdx != -1)
                    {
                        string displayName = dataSegment.Substring(0, digitIdx);
                        // Legacy recordings are always processed as Player type
                        recordings.Add(new LogEntry { type = RecordingType.Player, name = displayName });
                        break; 
                    }
                }
            }
            else
            {
                var foundEntries = new HashSet<string>();
                var recordingStartLogMatch = $"{HumrUtils.LogMatchTarget}{HumrUtils.RecordingStarted}";
                
                foreach (var line in File.ReadLines(currentFile.path))
                {
                    if (!line.Contains(recordingStartLogMatch)) continue;
                    
                    var content = line.Split(new[] { recordingStartLogMatch }, StringSplitOptions.None)[1];
                    if (!foundEntries.Add(content)) continue;

                    ParseAndAddLogEntry(content);
                }
            }
        }
        
        private void ParseAndAddLogEntry(string content)
        {
            var parts = content.Split(';');
            if (parts.Length < 3) return;

            var typeStr = parts[1];
            if (!Enum.TryParse<RecordingType>(typeStr, true, out var type))
            {
                type = RecordingType.Object;
            }

            recordings.Add(new LogEntry { type = type, name = parts[2] });
        }

        public void LoadRecordingAndExportAnim()
        {
            if (recordFileIndex < 0 || recordFileIndex >= recordFiles.Count) return;
            if (recordingIndex < 0 || recordingIndex >= recordings.Count) return;

            var currentFile = recordFiles[recordFileIndex];
            _displayName = recordings[recordingIndex].name;
            if (!Validate()) return;

            var logLines = LoadLogFileLines(currentFile.path);

            List<MotionSegment> segments;

            if (currentFile.type == LogFileType.Legacy)
            {
                segments = ParseLegacySegments(logLines, _displayName);
            }
            else
            {
                segments = HumrUtils.PartitionLogLinesIntoSegments(logLines.ToArray(), _displayName);
            }

            if (segments == null || segments.Count == 0)
            {
                Debug.LogWarning($"Motion Data with [{_displayName}] does not exist in {currentFile.path}");
                return;
            }

            SnapshotAvatarPose();

            try
            {
                ExecuteExportPipeline(segments, currentFile.path);
            }
            finally
            {
                RestoreAvatarPose();
            }
        }
        
        private List<MotionSegment> ParseLegacySegments(List<string> logLines, string targetName)
        {
            var segments = new List<MotionSegment>();
            var currentFrames = new List<MotionFrame>();
            float lastTime = -1f;
            var matchTarget = HumrUtils.LegacyLogMatchTarget;

            foreach (var line in logLines)
            {
                int prefixIdx = line.IndexOf(matchTarget);
                if (prefixIdx == -1) continue;

                string dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();
                
                if (!dataSegment.StartsWith(targetName)) continue;

                string numericDataRaw = dataSegment.Substring(targetName.Length);
                string[] tokens = numericDataRaw.Split(',');
                if (tokens.Length < 4) continue;

                var frame = new MotionFrame
                {
                    RecordTime = float.Parse(tokens[0], CultureInfo.InvariantCulture),
                    HipPosition = new Vector3(
                        float.Parse(tokens[1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[3], CultureInfo.InvariantCulture)
                    ),
                    BoneRotations = new List<Quaternion>()
                };
                
                try
                {
                    for (int i = 4; i + 3 < tokens.Length; i += 4)
                    {
                        frame.BoneRotations.Add(new Quaternion(
                            float.Parse(tokens[i], CultureInfo.InvariantCulture),
                            float.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                            float.Parse(tokens[i + 2], CultureInfo.InvariantCulture),
                            float.Parse(tokens[i + 3], CultureInfo.InvariantCulture)
                        ));
                    }

                    if (lastTime >= 0 && (frame.RecordTime < lastTime || frame.RecordTime - lastTime > 1.0f))
                    {
                        if (currentFrames.Count > 0)
                        {
                            segments.Add(new MotionSegment { Frames = new List<MotionFrame>(currentFrames) });
                            currentFrames.Clear();
                        }
                    }

                    currentFrames.Add(frame);
                    lastTime = frame.RecordTime;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to interpret legacy sequential data array line: {ex.Message}");
                }
            }

            if (currentFrames.Count > 0)
            {
                segments.Add(new MotionSegment { Frames = currentFrames });
            }

            return segments;
        }

        private static List<string> LoadLogFileLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                while (0 <= sr.Peek())
                {
                    lines.Add(sr.ReadLine());
                }
            }
            return lines;
        }

        private void ExecuteExportPipeline(List<MotionSegment> segments, string filePath)
        {
            CreateDirectoryIfNotExist(HumrUtils.HumrPath);
            SetupAnimatorController();

            var baseAnimName = HumrUtils.GetBaseAnimationName(filePath);

            for (var i = 0; i < segments.Count; i++)
            {
                var clip = PopulateAnimationClip(segments[i]);
                clip.name = $"{baseAnimName}_{i}";
                
                SaveGenericAnimationAsset(clip, baseAnimName, i);
                AddClipToController(clip);
            }

            ExportFBX(baseAnimName);
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
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
        }

        private void SetupAnimatorController()
        {
            var controllerFolderPath = $"{HumrUtils.HumrPath}/AnimationController";
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

            var animFolderPath = $"{HumrUtils.HumrPath}/GenericAnimations/{_displayName}";
            CreateDirectoryIfNotExist(animFolderPath);

            var animAssetPath = $"{animFolderPath}/{baseName}_{segmentIndex}.anim";

            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                HumrUtils.HumrWarning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
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

            var exportFolderPath = $"{HumrUtils.HumrPath}/FBXs/{HumrUtils.SanitizeFileName(_displayName)}";
            CreateDirectoryIfNotExist(exportFolderPath);

            var finalPath = $"{exportFolderPath}/{fileName}";
            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(finalPath, gameObject);
        }

        private AnimationClip PopulateAnimationClip(MotionSegment segment)
        {
            var frameCount = segment.Frames.Count;
            var totalCurves = 3 + (HumanTrait.BoneName.Length * 4); 

            var keyframes = InitializeKeyframeArrays(totalCurves, frameCount);

            for (var frameIdx = 0; frameIdx < frameCount; frameIdx++)
            {
                var frame = segment.Frames[frameIdx];
                ProcessFrameKeyframes(frame, keyframes, frameIdx);
            }

            return CreateAndBindCurves(keyframes);
        }
        
        private void ProcessFrameKeyframes(MotionFrame frame, Keyframe[][] keyframes, int frameIdx)
        {
            var localHipPos = ProcessHipPosition(frame.HipPosition);
            keyframes[0][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.x);
            keyframes[1][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.y);
            keyframes[2][frameIdx] = new Keyframe(frame.RecordTime, localHipPos.z);
            
            ApplyWorldRotationsToAvatar(frame);
            RecordLocalRotationsToKeyframes(keyframes, frameIdx, frame);
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

        private Vector3 ProcessHipPosition(Vector3 rawHipPos)
        {
            var hipTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipTransform == null || hipTransform.parent == null) return rawHipPos;

            var armatureParent = hipTransform.parent;
            return armatureParent.InverseTransformPoint(rawHipPos);
        }

        private void ApplyWorldRotationsToAvatar(MotionFrame frame)
        {
            for (var k = 0; k < HumanTrait.BoneName.Length; k++)
            {
                if (k >= frame.BoneRotations.Count) break;

                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)k);
                if (boneTransform == null) continue;

                boneTransform.rotation = frame.BoneRotations[k];
            }
        }

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
            var hipPath = HumrUtils.GetHierarchyPath(_animator.GetBoneTransform(0));

            clip.SetCurve(hipPath, typeof(Transform), "localPosition.x", new AnimationCurve(keyframes[0]));
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.y", new AnimationCurve(keyframes[1]));
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.z", new AnimationCurve(keyframes[2]));

            for (var m = 0; m < HumanTrait.BoneName.Length; m++)
            {
                var boneTransform = _animator.GetBoneTransform((HumanBodyBones)m);
                if (boneTransform == null) continue;

                var bonePath = HumrUtils.GetHierarchyPath(boneTransform);
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