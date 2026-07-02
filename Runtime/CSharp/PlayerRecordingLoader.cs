using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HUMR
{
    public enum LogFileType
    {
        Standard,
        Legacy,
        Unknown
    }

    public class MotionFrame
    {
        public float RecordTime { get; set; }
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new List<Quaternion>();
    }

    public class MotionSegment
    {
        public MotionSegment()
        {
            Frames = new List<MotionFrame>();
        }

        public List<MotionFrame> Frames { get; set; }
    }
    
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

    internal struct BoneSnapshot
    {
        public Transform Transform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    [RequireComponent(typeof(Animator))]
    public class PlayerRecordingLoader : MonoBehaviour, RecordLogLoaderInterface
    {
        public const string LogMatchTarget = "-  [HUMR] ";
        public const string LegacyLogMatchTarget = "-  HUMR:";
        public const string HumrPath = "Assets/HUMR";

        public string logFileDirectory;
        public string[] logFilePaths;
        public List<RecordFileEntry> recordFiles = new List<RecordFileEntry>();
        public string[] recordFileNames;
        public int recordFileIndex;
        public List<LogEntry> recordings = new List<LogEntry>();
        public int recordingIndex;
        public bool exportGenericAnimation;

        private Animator _animator;
        
        public void CollectLogFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;
        
            logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            recordFiles = LogFileScanner.BuildRecordFileEntries(logFilePaths);
            recordFileNames = LogFileScanner.BuildDisplayNames(recordFiles);
        }
        
        public void CollectRecordings()
        {
            if (recordFileIndex < 0 || recordFileIndex >= recordFiles.Count) return;
        
            var currentFile = recordFiles[recordFileIndex];
            recordings.Clear();
        
            if (currentFile.type == LogFileType.Legacy)
            {
                CollectLegacyRecordings(currentFile);
                return;
            }
            CollectStandardRecordings(currentFile);
        }
        
        private void CollectLegacyRecordings(RecordFileEntry currentFile)
        {
            foreach (var line in File.ReadLines(currentFile.path))
            {
                var displayName = LogDataParser.ExtractLegacyDisplayName(line, LegacyLogMatchTarget);
                if (displayName == null) continue;
        
                recordings.Add(new LogEntry { type = RecordingType.Player, name = displayName });
                break;
            }
        }
        
        private void CollectStandardRecordings(RecordFileEntry currentFile)
        {
            var foundEntries = new HashSet<string>();
            var recordingStartLogMatch = $"{LogMatchTarget}{RecorderUtils.RecordingStarted}";
        
            foreach (var line in File.ReadLines(currentFile.path))
            {
                if (!line.Contains(recordingStartLogMatch)) continue;
        
                var splitContent = line.Split(new[] { recordingStartLogMatch }, StringSplitOptions.None);
                if (splitContent.Length < 2) continue;
        
                var content = splitContent[1];
                if (!foundEntries.Add(content)) continue;
        
                var entry = LogDataParser.ParseLogEntry(content);
                if (entry == null) continue;
        
                recordings.Add(entry);
            }
        }
        
        public void LoadRecordingAndExportAnim()
        {
#if !UNITY_EDITOR
            RecorderUtils.HumrError("Exporting animations is only possible in editor.");
            return;
#else
            if (!ValidateIndices()) return;
        
            var currentFile = recordFiles[recordFileIndex];
            
            var currentDisplayName = recordings[recordingIndex].name;
            if (!Validate()) return;
        
            var logLines = LogDataParser.LoadLogFileLines(currentFile.path);
        
            var segments = currentFile.type == LogFileType.Legacy
                ? LogDataParser.ParseLegacySegments(logLines, currentDisplayName)
                : LogDataParser.PartitionLogLinesIntoSegments(logLines.ToArray(), currentDisplayName);
        
            if (segments == null || segments.Count == 0)
            {
                Debug.LogWarning($"Motion Data with [{currentDisplayName}] does not exist in {currentFile.path}");
                return;
            }
        
            var poseSnapshot = new AvatarPoseSnapshot();
            poseSnapshot.Take(transform, _animator);
        
            try
            {
                ExecuteExportPipeline(segments, currentFile.path, currentDisplayName);
            }
            finally
            {
                poseSnapshot.Restore(transform);
            } 
#endif
        }
        
        private bool ValidateIndices()
        {
            if (recordFileIndex < 0 || recordFileIndex >= recordFiles.Count) return false;
            return recordingIndex >= 0 && recordingIndex < recordings.Count;
        }
        
#if UNITY_EDITOR
        private void ExecuteExportPipeline(List<MotionSegment> segments, string filePath, string displayName)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);
            
            var controllerBuilder = new AnimationControllerBuilder();
            controllerBuilder.Setup(HumrPath);
        
            var baseAnimName = PathUtils.GetBaseAnimationName(filePath);
        
            for (var i = 0; i < segments.Count; i++)
            {
                var clip = AnimationClipFactory.PopulateAnimationClip(segments[i], _animator);
                clip.name = $"{baseAnimName}_{i}";
        
                if (exportGenericAnimation)
                {
                    AnimationAssetExporter.SaveGenericAnimationAsset(clip, HumrPath, displayName, baseAnimName, i, controllerBuilder);
                }
                
                controllerBuilder.AddClipToController(clip);
            }
        
            AnimationAssetExporter.ExportFBX(_animator, controllerBuilder.Controller, HumrPath, displayName, baseAnimName, gameObject);
        }
#endif
        
        private bool Validate()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        
            return _animator != null;
        }
    }
}