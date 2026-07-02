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
        public int fileIndex;
        public RecordingFile currentFile;
        public int targetIndex;
        
        public bool exportGenericAnimation;

        private Animator _animator;

        public void CollectLogFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;

            var logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            recordingFiles = RecordingScanner.BuildRecordingFiles(logFilePaths);
        }

        public void CollectTargetNames()
        {
            currentFile.targetNames = LogDataParser.CollectTargetNames(currentFile);
        }

        public void CollectTakes()
        {
            var currentTargetName = currentFile.targetNames[targetIndex];
            var logLines = LogDataParser.LoadLogFileLines(currentFile.path);

            currentFile.recordingTakes = currentFile.type == LogType.Legacy
                ? LogDataParser.ParseLegacyTakes(logLines, currentTargetName)
                : LogDataParser.PartitionLogLinesIntoTakes(logLines.ToArray(), currentTargetName);
            
            if (currentFile.recordingTakes != null && currentFile.recordingTakes.Count != 0) return;
            
            HumrLogger.Warning($"Motion Data with [{currentTargetName}] does not exist in {currentFile.path}");
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