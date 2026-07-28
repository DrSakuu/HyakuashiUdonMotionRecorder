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

        private HumrRecordingLoader _loader;
        private string _userProfile;
        private string _logPath;
        private List<RecordingFile> _recordingFiles = new List<RecordingFile>();
        private string[] _recordingFileNames;
        private RecordingFile _currentFile;

        public override void OnInspectorGUI()
        {
            _loader = (HumrRecordingLoader)target;
            if (_loader == null) return;

            var errorMessage = "";
            UpdateLogDirectory();
            DrawAdvancedPathSection();
            UpdateRecordingFiles();
            if (!DrawLogFileDropdown()) SetError("No log files found.");

            var targetStrList = _currentFile.Targets
                .Select(t => $"{t.targetType}: {t.name}")
                .ToArray();
            _loader.targetIndex = EditorGUILayout.Popup(
                "Recording Target", _loader.targetIndex, targetStrList);
            if (_currentFile.type == LogType.NoData) SetError("No HUMR data found.");
            if (_currentFile.type == LogType.Corrupt) SetError("HUMR data is corrupt.");

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(_currentFile.foundTakesStr);
            
            var isHumanoidBoneTarget = _currentFile.Targets[_loader.targetIndex].targetType == TargetType.BoneRotations;
            var isHumanoidAvatar = _loader.Animator.avatar != null && _loader.Animator.avatar.isHuman;
            if (isHumanoidBoneTarget && !isHumanoidAvatar) SetError("The Avatar needs to be Humanoid.");
            
            _loader.exportHumanFbx = GUILayout.Toggle(_loader.exportHumanFbx, "Export Humanoid .fbx");
            _loader.exportGenericAnim = GUILayout.Toggle(_loader.exportGenericAnim, "Export Generic .anim");
            if (!_loader.exportHumanFbx && !_loader.exportGenericAnim) 
                SetError("Select either .fbx or .anim export.");
            
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
            if (_loader.showAdvanced) return;

            _userProfile ??= Environment.GetEnvironmentVariable("USERPROFILE");
            _logPath = $"{_userProfile}{VrcLogPathSuffix}";
        }

        private void DrawAdvancedPathSection()
        {
            _loader.showAdvanced = EditorGUILayout.Foldout(
                _loader.showAdvanced, "Advanced: Custom Log Path");
            if (!_loader.showAdvanced) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            _logPath = EditorGUILayout.TextField("Output Log Path (resets when closed)", _logPath);

            if (GUILayout.Button("Explore", GUILayout.Width(100))) OpenLogFolder(_logPath);

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
                () => _loader.fileIndex,
                value => _loader.fileIndex = value,
                _recordingFileNames);
            if (_recordingFiles == null || _recordingFiles.Count == 0) return false;

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
            if (!Directory.Exists(_logPath)) return;

            var logFilePaths = Directory.GetFiles(_logPath, "*.txt");
            if (logFilePaths.Length == _recordingFiles.Count) return;

            _recordingFiles = HumrLogParser.CollectRecordingFiles(logFilePaths);
            if (_recordingFiles == null || _recordingFiles.Count == 0)
            {
                _recordingFileNames = new[] { "No logs found" };
                return;
            }

            _recordingFileNames = _recordingFiles.Select(file => file.fileName).ToArray();
            var humrIndex = _recordingFiles.FindIndex(file => file.type == LogType.Humr);
            if (humrIndex != -1) _loader.fileIndex = humrIndex;
            SetCurrentRecordingFile();
        }

        public void SetCurrentRecordingFile()
        {
            if (_recordingFiles == null || _recordingFiles.Count == 0)
            {
                _currentFile = null;
                return;
            }

            // TODO: is this needed?
            _loader.fileIndex = Mathf.Clamp(_loader.fileIndex, 0, _recordingFiles.Count - 1);
            _currentFile = _recordingFiles[_loader.fileIndex];
            CollectTargets();
            CollectTakes();
        }

        public void CollectTargets()
        {
            _currentFile.Targets = HumrLogParser.ResolveTargets(_currentFile);
            _loader.targetIndex = 0;
        }

        public void CollectTakes()
        {
            if (_currentFile.Targets.Length == 0) return;
            
            var (currentTargetType, currentTargetName) = _currentFile.Targets[_loader.targetIndex];
            var logLines = HumrLogParser.LoadLogFileLines(_currentFile.path);

            _currentFile.takes = currentTargetType == TargetType.Legacy
                ? HumrLogParser.ParseLegacyTakes(logLines, currentTargetName)
                : HumrLogParser.PartitionLogLinesIntoTakes(logLines.ToArray(), (currentTargetType, currentTargetName));

            if (_currentFile.takes == null)
            {
                _currentFile.foundTakesStr = "Found 0 takes.";
            }
            else if (_currentFile.takes.Count == 1)
            {
                _currentFile.foundTakesStr = "Found 1 take.";
            }
            else
            {
                _currentFile.foundTakesStr = $"Found {_currentFile.takes.Count} takes.";
            }
        }

        private void LoadRecordingAndExportAnim()
        {
            var (_, currentTargetName) = _currentFile.Targets[_loader.targetIndex];
            if (_loader.Animator == null) return;

            var poseSnapshot = new AvatarPoseSnapshot();
            poseSnapshot.Take(_loader.transform, _loader.Animator);

            try
            {
                ExecuteExportPipeline(_currentFile.takes, _currentFile.path, currentTargetName);
            }
            finally
            {
                poseSnapshot.Restore(_loader.transform);
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

            if (!_loader.exportHumanFbx) return;
            
            var previousAnimControl = _loader.Animator.runtimeAnimatorController;
            try
            {
                _loader.Animator.runtimeAnimatorController = controllerBuilder.Controller;
                var exportPath = GetAssetPath("FBXs", targetName, baseAnimName, "fbx"); 
                ModelExporter.ExportObject(exportPath, _loader.gameObject);
            }
            finally
            {
                _loader.Animator.runtimeAnimatorController = previousAnimControl;
            }
        }

        private void AddTakeToControllerBuilder(
            RecordingTake take, string takeAnimStr, string targetName, AnimationControllerBuilder controllerBuilder)
        {
            var takeClip = AnimationClipFactory.PopulateAnimationClip(take, _loader.Animator);
            takeClip.name = takeAnimStr;

            if (_loader.exportGenericAnim)
            {
                controllerBuilder.CleanControllerStates(false);
                var animAssetPath = GetAssetPath(
                    "GenericAnimations", targetName, takeAnimStr, "anim");
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