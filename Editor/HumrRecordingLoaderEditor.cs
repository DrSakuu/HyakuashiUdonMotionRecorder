using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace Humr.Editor
{
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : UnityEditor.Editor
    {
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";

        private const string HumrPath = @"Assets\HUMR";

        public string logFileDirectory;
        public List<RecordingFile> recordingFiles = new List<RecordingFile>();
        public string[] recordingFileNames;
        public int fileIndex;

        public RecordingFile currentFile;
        public int targetIndex;
        public bool exportHumanoidFbx = true;
        public bool exportGenericAnimation;
        private HumrRecordingLoader _recordLoader;
        private bool _showAdvanced;
        private string _userProfile;

        public override void OnInspectorGUI()
        {
            _recordLoader = (HumrRecordingLoader)target;
            if (_recordLoader == null) return;

            var errorMessage = "";
            UpdateLogDirectory();
            DrawAdvancedPathSection();
            UpdateRecordingFiles();
            if (!DrawLogFileDropdown()) SetError("No log files found.");

            var targetStrList = currentFile.Targets
                .Select(t => $"{t.targetType}: {t.name}")
                .ToArray();
            targetIndex = EditorGUILayout.Popup("Recording Target", targetIndex, targetStrList);
            if (currentFile.type == LogType.NoData) SetError("No HUMR data found.");
            if (currentFile.type == LogType.Corrupt) SetError("HUMR data is corrupt.");

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(currentFile.foundTakesStr);
            
            var isHumanoidBoneTarget = currentFile.Targets[targetIndex].targetType == TargetType.BoneRotations;
            var isHumanoidAvatar = _recordLoader.Animator.avatar != null && _recordLoader.Animator.avatar.isHuman;
            if (isHumanoidBoneTarget && !isHumanoidAvatar) SetError("The Avatar needs to be Humanoid.");
            
            exportHumanoidFbx = GUILayout.Toggle(exportHumanoidFbx, "Export Humanoid .fbx");
            exportGenericAnimation = GUILayout.Toggle(exportGenericAnimation, "Export Generic .anim");
            if (!exportHumanoidFbx && !exportGenericAnimation) SetError("Select either .fbx or .anim export.");
            
            if (!string.IsNullOrEmpty(errorMessage)) EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            DrawExportButton(string.IsNullOrEmpty(errorMessage));
            return;

            void SetError(string msg)
            {
                if (string.IsNullOrEmpty(errorMessage)) errorMessage = msg;
            }
        }

        private void UpdateLogDirectory()
        {
            if (_showAdvanced) return;

            _userProfile ??= Environment.GetEnvironmentVariable("USERPROFILE");
            logFileDirectory = $"{_userProfile}{VrcLogPathSuffix}";
        }

        private void DrawAdvancedPathSection()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Custom Log Path");
            if (!_showAdvanced) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            logFileDirectory = EditorGUILayout.TextField(
                "Output Log Path (resets when closed)", logFileDirectory);

            if (GUILayout.Button("Explore", GUILayout.Width(100))) OpenLogFolder(logFileDirectory);

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private static void OpenLogFolder(string path)
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            else
                HumrLogger.Error($"Log path does not exist: {path}");
        }

        private static void DrawClickableDropdown(
            string label, Action onClick, Func<int> getSelectedIndex, Action<int> setSelectedIndex, string[] options)
        {
            var lineRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(lineRect, new GUIContent(label));
            if (IsRectClick(popupRect)) onClick?.Invoke();

            setSelectedIndex(EditorGUI.Popup(popupRect, getSelectedIndex(), options));
        }

        private bool DrawLogFileDropdown()
        {
            EditorGUI.BeginChangeCheck();
            DrawClickableDropdown(
                "Recording Log File",
                UpdateRecordingFiles,
                () => fileIndex,
                value => fileIndex = value,
                recordingFileNames);
            if (recordingFiles == null || recordingFiles.Count == 0) return false;

            if (EditorGUI.EndChangeCheck()) SetCurrentRecordingFile();
            return true;
        }

        private void DrawExportButton(bool enabled = true)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (!GUILayout.Button("Export recording")) return;

                LoadRecordingAndExportAnim();
            }
        }

        private static bool IsRectClick(Rect rect)
        {
            return Event.current.type == EventType.MouseDown &&
                   Event.current.button == 0 &&
                   rect.Contains(Event.current.mousePosition);
        }

        public void UpdateRecordingFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;

            var logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            if (logFilePaths.Length == recordingFiles.Count) return;

            recordingFiles = HumrLogParser.CollectRecordingFiles(logFilePaths);
            if (recordingFiles == null || recordingFiles.Count == 0)
            {
                recordingFileNames = new[] { "No logs found" };
                return;
            }

            recordingFileNames = recordingFiles.Select(file => file.fileName).ToArray();
            SetCurrentRecordingFile();
        }

        public void SetCurrentRecordingFile()
        {
            if (recordingFiles == null || recordingFiles.Count == 0)
            {
                currentFile = null;
                return;
            }

            // TODO: is this needed?
            fileIndex = Mathf.Clamp(fileIndex, 0, recordingFiles.Count - 1);
            currentFile = recordingFiles[fileIndex];
            CollectTargets();
            CollectTakes();
        }

        public void CollectTargets()
        {
            currentFile.Targets = HumrLogParser.ResolveTargets(currentFile);
            targetIndex = 0;
        }

        public void CollectTakes()
        {
            if (currentFile.Targets.Length == 0) return;
            
            var (currentTargetType, currentTargetName) = currentFile.Targets[targetIndex];
            var logLines = HumrLogParser.LoadLogFileLines(currentFile.path);

            currentFile.recordingTakes = currentTargetType == TargetType.Legacy
                ? HumrLogParser.ParseLegacyTakes(logLines, currentTargetName)
                : HumrLogParser.PartitionLogLinesIntoTakes(logLines.ToArray(), (currentTargetType, currentTargetName));

            if (currentFile.recordingTakes == null)
            {
                currentFile.foundTakesStr = "Found 0 takes.";
                return;
            }

            currentFile.foundTakesStr = $"Found {currentFile.recordingTakes.Count} takes.";
        }

        private void LoadRecordingAndExportAnim()
        {
            var (_, currentTargetName) = currentFile.Targets[targetIndex];
            if (_recordLoader.Animator == null) return;

            var poseSnapshot = new AvatarPoseSnapshot();
            poseSnapshot.Take(_recordLoader.transform, _recordLoader.Animator);

            try
            {
                ExecuteExportPipeline(currentFile.recordingTakes, currentFile.path, currentTargetName);
            }
            finally
            {
                poseSnapshot.Restore(_recordLoader.transform);
            }
        }

        private void ExecuteExportPipeline(List<RecordingTake> takes, string filePath, string targetName)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);

            var controllerBuilder = new AnimationControllerBuilder();
            controllerBuilder.Setup(HumrPath);

            var baseAnimName = PathUtils.GetBaseAnimationName(filePath);

            for (var i = 0; i < takes.Count; i++)
            {
                var takeAnimStr = $"{baseAnimName}_Take{i + 1}";
                AddTakeToControllerBuilder(takes[i], takeAnimStr, targetName, controllerBuilder);
            }

            if (!exportHumanoidFbx) return;
            
            var previousAnimControl = _recordLoader.Animator.runtimeAnimatorController;
            try
            {
                _recordLoader.Animator.runtimeAnimatorController = controllerBuilder.Controller;
                var exportPath = GetAssetPath("FBXs", targetName, baseAnimName, "fbx"); 
                ModelExporter.ExportObject(exportPath, _recordLoader.gameObject);
            }
            finally
            {
                _recordLoader.Animator.runtimeAnimatorController = previousAnimControl;
            }
        }

        private void AddTakeToControllerBuilder(RecordingTake take, string takeAnimStr, string targetName, AnimationControllerBuilder controllerBuilder)
        {
            var takeClip = AnimationClipFactory.PopulateAnimationClip(take, _recordLoader.Animator);
            takeClip.name = takeAnimStr;

            if (exportGenericAnimation)
            {
                controllerBuilder.CleanControllerStates(false);
                var animAssetPath = GetAssetPath("GenericAnimations", targetName, takeAnimStr, "anim");
                AnimationControllerBuilder.SaveGenericAnimationAsset(takeClip, animAssetPath);
            }

            controllerBuilder.AddClipToController(takeClip);
        }
        
        private static string GetAssetPath(string subFolder, string targetName, string fileName, string extension)
        {
            var folderPath = Path.Join(HumrPath, subFolder, PathUtils.SanitizeFileName(targetName));
            PathUtils.CreateDirectoryIfNotExist(folderPath);

            return Path.Join(folderPath, $"{fileName}.{extension}");
        }

        private static string FbxAssetPath(string targetName, string fileName)
        {
            var exportFolderPath = Path.Join(HumrPath, "FBXs", PathUtils.SanitizeFileName(targetName));
            PathUtils.CreateDirectoryIfNotExist(exportFolderPath);

            return Path.Join(exportFolderPath, $"{fileName}.fbx");
        }
    }
}