using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Humr.Editor
{
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : UnityEditor.Editor
    {
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";

        private const string HumrPath = "Assets/HUMR";

        public string logFileDirectory;
        public List<RecordingFile> recordingFiles = new List<RecordingFile>();
        public string[] recordingFileNames;
        public int fileIndex;

        public RecordingFile currentFile;
        public int targetIndex;
        public bool exportGenericAnimation;
        private HumrRecordingLoader _recordLoader;
        private bool _showAdvanced;
        private string _userProfile;

        public override void OnInspectorGUI()
        {
            _recordLoader = (HumrRecordingLoader)target;
            if (_recordLoader == null) return;

            UpdateLogDirectory();
            DrawAdvancedPathSection();
            UpdateRecordingFiles();
            if (!DrawLogFileDropdown()) return;

            targetIndex = EditorGUILayout.Popup(
                "Recording Target", targetIndex, currentFile.targetNames);
            if (currentFile.type != LogType.Humr
                && currentFile.type != LogType.Legacy) return;

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(currentFile.foundTakesStr);
            exportGenericAnimation = GUILayout.Toggle(exportGenericAnimation, "Export Generic Animation");
            DrawExportButton();
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

        private void DrawExportButton()
        {
            if (!GUILayout.Button("Load recording and export .fbx")) return;

            LoadRecordingAndExportAnim();
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
            CollectTargetNames();
            CollectTakes();
        }

        public void CollectTargetNames()
        {
            currentFile.targetNames = HumrLogParser.ResolveTargetNames(currentFile);
            // TODO: is this needed?
            targetIndex = Mathf.Clamp(targetIndex, 0, recordingFiles.Count - 1);
        }

        public void CollectTakes()
        {
            var currentTargetName = currentFile.targetNames[targetIndex];
            var logLines = HumrLogParser.LoadLogFileLines(currentFile.path);

            currentFile.recordingTakes = currentFile.type == LogType.Legacy
                ? HumrLogParser.ParseLegacyTakes(logLines, currentTargetName)
                : HumrLogParser.PartitionLogLinesIntoTakes(logLines.ToArray(), currentTargetName);

            if (currentFile.recordingTakes == null)
            {
                currentFile.foundTakesStr = "Found 0 takes.";
                return;
            }

            currentFile.foundTakesStr = $"Found {currentFile.recordingTakes.Count} takes.";
        }

        private void LoadRecordingAndExportAnim()
        {
            var currentTargetName = currentFile.targetNames[targetIndex];
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
                ExportTake(takes[i], takeAnimStr, targetName, controllerBuilder);
            }

            _recordLoader.Animator.runtimeAnimatorController = controllerBuilder.Controller;
            var exportPath = FbxAssetPath(HumrPath, targetName, baseAnimName);
            AnimationControllerBuilder.ExportFBX(exportPath, _recordLoader.gameObject);
        }

        private void ExportTake(
            RecordingTake take, string takeAnimStr, string targetName, AnimationControllerBuilder controllerBuilder)
        {
            var takeClip = AnimationClipFactory.PopulateAnimationClip(take, _recordLoader.Animator);
            takeClip.name = takeAnimStr;

            if (exportGenericAnimation)
            {
                controllerBuilder.CleanControllerStates(false);
                var animAssetPath = AnimAssetPath(HumrPath, targetName, takeAnimStr);
                AnimationControllerBuilder.SaveGenericAnimationAsset(takeClip, animAssetPath);
            }

            controllerBuilder.AddClipToController(takeClip);
        }

        private static string AnimAssetPath(string humrPath, string targetName, string takeAnimStr)
        {
            var animFolderPath = $"{humrPath}/GenericAnimations/{PathUtils.SanitizeFileName(targetName)}";
            PathUtils.CreateDirectoryIfNotExist(animFolderPath);
            return $"{animFolderPath}/{takeAnimStr}.anim";
        }

        private static string FbxAssetPath(string humrPath, string targetName, string fileName)
        {
            var exportFolderPath = $"{humrPath}/FBXs/{PathUtils.SanitizeFileName(targetName)}";
            PathUtils.CreateDirectoryIfNotExist(exportFolderPath);

            return $"{exportFolderPath}/{fileName}";
        }
    }
}