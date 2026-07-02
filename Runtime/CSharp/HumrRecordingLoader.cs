using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HUMR
{
    public interface RecordLogLoaderInterface : IEventSystemHandler
    {
        void LoadRecordingAndExportAnim();
    }

    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour, RecordLogLoaderInterface
    {
        public const string LogMatchTarget = "-  [HUMR] ";
        public const string LegacyLogMatchTarget = "-  HUMR:";
        public const string HumrPath = "Assets/HUMR";

        public string logFileDirectory;
        public string[] logFilePaths;
        public List<RecordingFile> recordingFiles = new List<RecordingFile>();
        public string[] recordingFileNames;
        public int recordingFileIndex;
        public List<Recording> recordings = new List<Recording>();
        public int recordingIndex;
        public bool exportGenericAnimation;

        private Animator _animator;

        public void CollectLogFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;
        
            logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            recordingFiles = RecordingScanner.BuildRecordingFiles(logFilePaths);
            recordingFileNames = RecordingScanner.BuildRecordingFileNames(recordingFiles);
        }

        public void CollectRecordings()
        {
            if (recordingFileIndex < 0 || recordingFileIndex >= recordingFiles.Count) return;
        
            var currentFile = recordingFiles[recordingFileIndex];
            recordings.Clear();
        
            if (currentFile.type == RecordingType.Legacy)
            {
                CollectLegacyRecordings(currentFile);
                return;
            }
            CollectStandardRecordings(currentFile);
        }

        private void CollectLegacyRecordings(RecordingFile currentFile)
        {
            foreach (var line in File.ReadLines(currentFile.path))
            {
                var displayName = LogDataParser.ExtractLegacyDisplayName(line, LegacyLogMatchTarget);
                if (displayName == null) continue;
        
                recordings.Add(new Recording { type = RecordingType.Legacy, target = displayName });
                break;
            }
        }

        private void CollectStandardRecordings(RecordingFile currentFile)
        {
            var foundRecordings = new HashSet<string>();
            var recordingStartLogMatch = $"{LogMatchTarget}{HumrLogger.RecordingStarted}";
        
            foreach (var line in File.ReadLines(currentFile.path))
            {
                if (!line.Contains(recordingStartLogMatch)) continue;
        
                var splitContent = line.Split(new[] { recordingStartLogMatch }, StringSplitOptions.None);
                if (splitContent.Length < 2) continue;
        
                var content = splitContent[1];
                if (!foundRecordings.Add(content)) continue;
        
                var recording = LogDataParser.ParseRecordingEntry(content);
                if (recording == null) continue;
        
                recordings.Add(recording);
            }
        }

        public void LoadRecordingAndExportAnim()
        {
#if !UNITY_EDITOR
            HumrLogger.Error("Exporting animations is only possible in editor.");
            return;
#else
            if (!ValidateIndices()) return;
        
            var currentFile = recordingFiles[recordingFileIndex];
            
            var currentDisplayName = recordings[recordingIndex].target;
            if (!ValidateAnimator()) return;
        
            var logLines = LogDataParser.LoadLogFileLines(currentFile.path);
        
            var takes = currentFile.type == RecordingType.Legacy
                ? LogDataParser.ParseLegacyTakes(logLines, currentDisplayName)
                : LogDataParser.PartitionLogLinesIntoTakes(logLines.ToArray(), currentDisplayName);
        
            if (takes == null || takes.Count == 0)
            {
                Debug.LogWarning($"Motion Data with [{currentDisplayName}] does not exist in {currentFile.path}");
                return;
            }
        
            var poseSnapshot = new AvatarPoseSnapshot();
            poseSnapshot.Take(transform, _animator);
        
            try
            {
                ExecuteExportPipeline(takes, currentFile.path, currentDisplayName);
            }
            finally
            {
                poseSnapshot.Restore(transform);
            } 
#endif
        }

#if UNITY_EDITOR
        private bool ValidateIndices()
        {
            if (recordingFileIndex < 0 || recordingFileIndex >= recordingFiles.Count) return false;
            return recordingIndex >= 0 && recordingIndex < recordings.Count;
        }

        private bool ValidateAnimator()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        
            return _animator != null;
        }

        private void ExecuteExportPipeline(List<RecordingTake> takes, string filePath, string displayName)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);
            
            var controllerBuilder = new AnimationControllerBuilder();
            controllerBuilder.Setup(HumrPath);
        
            var baseAnimName = PathUtils.GetBaseAnimationName(filePath);
        
            for (var i = 0; i < takes.Count; i++)
            {
                var takeClip = AnimationClipFactory.PopulateAnimationClip(takes[i], _animator);
                takeClip.name = $"{baseAnimName}_Take{i+1}";
        
                if (exportGenericAnimation)
                {
                    AnimationAssetExporter.SaveGenericAnimationAsset(takeClip, HumrPath, displayName, baseAnimName, i, controllerBuilder);
                }
                
                controllerBuilder.AddClipToController(takeClip);
            }
        
            AnimationAssetExporter.ExportFBX(_animator, controllerBuilder.Controller, HumrPath, displayName, baseAnimName, gameObject);
        }

#endif
    }
}