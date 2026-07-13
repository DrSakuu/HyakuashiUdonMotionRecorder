using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string LogMatchTarget = "-  [HUMR] RECORDING;";
        public const string LegacyLogMatchTarget = "-  HUMR:";
        public const string HumrPath = "Assets/HUMR";

        public string logFileDirectory;
        public List<RecordingFile> recordingFiles = new List<RecordingFile>();
        public string[] recordingFileNames;
        public int fileIndex;

        public RecordingFile currentFile;
        public int targetIndex;
        public bool exportGenericAnimation;

        private Animator _animator;

        public void UpdateRecordingFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;

            var logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            if (logFilePaths.Length == recordingFiles.Count) return;
            
            recordingFiles = CollectRecordingFiles(logFilePaths);
            if (recordingFiles == null || recordingFiles.Count == 0)
            {
                recordingFileNames = new string[] { "No logs found" };
                return;
            }

            recordingFileNames = recordingFiles.Select(file => file.fileName).ToArray();
            SetCurrentRecordingFile();
        }

        private static List<RecordingFile> CollectRecordingFiles(string[] filePaths)
        {
            var discoveredFiles = new List<RecordingFile>();

            foreach (var filePath in filePaths)
            {
                var fileType = RecordingScanner.DetermineRecordingType(filePath);
                var writeTime = File.GetLastWriteTime(filePath);
                var fileName = RecordingScanner.BuildRecordingFileName(filePath, fileType);
                discoveredFiles.Add(new RecordingFile
                {
                    path = filePath, type = fileType , LastWriteTime = writeTime, fileName =  fileName
                });
            }

            return discoveredFiles
                .OrderByDescending(entry => entry.LastWriteTime)
                .ToList();
        }

        public void SetCurrentRecordingFile()
        {
            if (recordingFiles == null || recordingFiles.Count == 0)
            {
                currentFile = null;
                return;
            }
            
            fileIndex = Mathf.Clamp(fileIndex, 0, recordingFiles.Count - 1);
            currentFile = recordingFiles[fileIndex];
            CollectTargetNames();
            CollectTakes();
        }

        public void CollectTargetNames()
        {
            switch (currentFile.type)
            {
                case LogType.Humr:
                case LogType.Legacy:
                    currentFile.targetNames = LogDataParser.CollectTargetNames(currentFile);
                    break;
                case LogType.Corrupt:
                    currentFile.targetNames = new[] { "Humr data is corrupted" };
                    break;
                case LogType.NoData:
                default:
                    currentFile.targetNames = new[] { "No Humr data" };
                    break;
            }
            targetIndex = Mathf.Clamp(targetIndex, 0, recordingFiles.Count - 1);
        }

        public void CollectTakes()
        {
            var currentTargetName = currentFile.targetNames[targetIndex];
            var logLines = LogDataParser.LoadLogFileLines(currentFile.path);

            currentFile.recordingTakes = currentFile.type == LogType.Legacy
                ? LogDataParser.ParseLegacyTakes(logLines, currentTargetName)
                : LogDataParser.PartitionLogLinesIntoTakes(logLines.ToArray(), currentTargetName);

            if (currentFile.recordingTakes == null)
            {
                currentFile.foundTakesStr = "Found 0 takes.";
                return;
            }
            currentFile.foundTakesStr = $"Found {currentFile.recordingTakes.Count} takes.";
        }

        public void LoadRecordingAndExportAnim()
            {
#if !UNITY_EDITOR
            HumrLogger.Error("Exporting animations is only possible in editor.");
            return;
#else
            var currentTargetName = currentFile.targetNames[targetIndex];
            if (!ValidateAnimator()) return;
            
            var poseSnapshot = new AvatarPoseSnapshot();
            poseSnapshot.Take(transform, _animator);
        
            try
            {
                ExecuteExportPipeline(currentFile.recordingTakes, currentFile.path, currentTargetName);
            }
            finally
            {
                poseSnapshot.Restore(transform);
            } 
#endif
        }

        private bool ValidateIndices()
        {
            return targetIndex >= 0 && targetIndex < currentFile.recordingTakes.Count;
        }

        private bool ValidateAnimator()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        
            return _animator != null;
        }

#if UNITY_EDITOR

        private void ExecuteExportPipeline(List<RecordingTake> takes, string filePath, string targetName)
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
                    AnimationAssetExporter.SaveGenericAnimationAsset(takeClip, HumrPath, targetName, baseAnimName, i, controllerBuilder);
                }
                
                controllerBuilder.AddClipToController(takeClip);
            }
        
            AnimationAssetExporter.ExportFBX(_animator, controllerBuilder.Controller, HumrPath, targetName, baseAnimName, gameObject);
        }

#endif
    }
}