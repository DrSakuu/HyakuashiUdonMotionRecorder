using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrSakuu.Humr.Editor
{
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : UnityEditor.Editor
    {
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";
        private const string HumrPath = @"Assets\HUMR";
        private RecordingFile _currentFile;

        private HumrRecordingLoader _loader;
        private string _logPath;
        private string[] _recordingFileNames;
        private List<RecordingFile> _recordingFiles = new();
        private string _userProfile;

        public override void OnInspectorGUI()
        {
            _loader = (HumrRecordingLoader)target;
            if (_loader == null) return;

            var errorMessage = "";
            DrawLogFileSelection(ref errorMessage);

            if (!TryDrawTargetSelection()) return;

            ValidateCurrentRecording(ref errorMessage);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(_currentFile.foundTakesStr);

            DrawHumanoidOptions(ref errorMessage);
            DrawExportOptions(ref errorMessage);

            if (!string.IsNullOrEmpty(errorMessage))
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);

            DrawExportButton(string.IsNullOrEmpty(errorMessage));
        }

        private void DrawLogFileSelection(ref string errorMessage)
        {
            UpdateLogDirectory();
            DrawAdvancedPathSection();
            UpdateRecordingFiles();

            if (!DrawLogFileDropdown())
                SetError(ref errorMessage, "No log files found.");
        }

        private bool TryDrawTargetSelection()
        {
            var currentTargets = _currentFile.Targets;
            if (currentTargets == null)
            {
                EditorGUILayout.HelpBox("Please select the log file again.", MessageType.Error);
                ScanTargets();
                return false;
            }

            var targetStrList = currentTargets
                .Select(t => $"{t.targetType}: {t.name}")
                .ToArray();

            EditorGUI.BeginChangeCheck();
            _loader.targetIndex = EditorGUILayout.Popup("Recording Target", _loader.targetIndex, targetStrList);
            if (EditorGUI.EndChangeCheck()) ParseTakes();

            return true;
        }

        private void ValidateCurrentRecording(ref string errorMessage)
        {
            if (_currentFile.type == LogType.NoData) SetError(ref errorMessage, "No HUMR data found.");
            if (_currentFile.type == LogType.Corrupt) SetError(ref errorMessage, "HUMR data is corrupt.");
        }

        private void DrawHumanoidOptions(ref string errorMessage)
        {
            var targetType = _currentFile.Targets[_loader.targetIndex].targetType;
            var isHumanoidBoneTarget = targetType == TargetType.BoneRotations || targetType == TargetType.Legacy;
            if (!isHumanoidBoneTarget) return;

            var isHumanoidAvatar = _loader.Animator.avatar != null && _loader.Animator.avatar.isHuman;
            if (!isHumanoidAvatar) SetError(ref errorMessage, "The Avatar needs to be Humanoid.");

            _loader.blenderHipFix = GUILayout.Toggle(
                _loader.blenderHipFix,
                new GUIContent(
                    "Blender hip fix",
                    "If a skinned mesh renderer's Root Bone is not set to Armature, the .fbx file will import into Blender with incorrect bone structure."));
        }

        private void DrawExportOptions(ref string errorMessage)
        {
            _loader.exportFbx = GUILayout.Toggle(_loader.exportFbx, "Export .fbx");
            _loader.exportAnim = GUILayout.Toggle(_loader.exportAnim, "Export .anim");

            if (!_loader.exportFbx && !_loader.exportAnim)
                SetError(ref errorMessage, "Select either .fbx or .anim export.");
        }

        private static void SetError(ref string errorMessage, string message)
        {
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = message;
        }

        private void UpdateLogDirectory()
        {
            if (_loader.showAdvanced) return;

            _userProfile ??= Environment.GetEnvironmentVariable("USERPROFILE");
            _logPath = $"{_userProfile}{VrcLogPathSuffix}";
        }

        private void DrawAdvancedPathSection()
        {
            _loader.showAdvanced = EditorGUILayout.Foldout(_loader.showAdvanced, "Advanced: Custom Log Path");
            if (!_loader.showAdvanced) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            _logPath = EditorGUILayout.TextField("Output Log Path (resets when closed)", _logPath);
            if (GUILayout.Button("Explore", GUILayout.Width(100))) ExploreLogFolder(_logPath);

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private static void ExploreLogFolder(string path)
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

        private static bool IsRectClick(Rect rect)
        {
            return Event.current.type == EventType.MouseDown &&
                   Event.current.button == 0 &&
                   rect.Contains(Event.current.mousePosition);
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

                ExportCurrentTargetTakes();
            }
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
            ScanTargets();
            ParseTakes();
        }

        public void ScanTargets()
        {
            _currentFile.Targets = HumrLogParser.ScanTargets(_currentFile);
            _loader.targetIndex = 0;
        }

        public void ParseTakes()
        {
            if (_currentFile.Targets.Length == 0) return;

            var (currentTargetType, currentTargetName) = _currentFile.Targets[_loader.targetIndex];
            var logLines = HumrLogParser.LoadHumrLogLines(_currentFile.path);
            _currentFile.LastWriteTime = File.GetLastWriteTime(_currentFile.path);

            _currentFile.takes = currentTargetType == TargetType.Legacy
                ? HumrLogParser.ParseLegacyTakes(logLines, currentTargetName)
                : HumrLogParser.ParseTakes(logLines, (currentTargetType, currentTargetName));

            if (_currentFile.takes == null)
                _currentFile.foundTakesStr = "Found 0 takes.";
            else if (_currentFile.takes.Count == 1)
                _currentFile.foundTakesStr = "Found 1 take.";
            else
                _currentFile.foundTakesStr = $"Found {_currentFile.takes.Count} takes.";
        }

        private void ExportCurrentTargetTakes()
        {
            if (_loader.Animator == null) return;

            var (currentTargetType, currentTargetName) = _currentFile.Targets[_loader.targetIndex];
            var originalLoader = _loader;
            var tempLoaderObject = Instantiate(_loader.gameObject);
            tempLoaderObject.transform.position = Vector3.zero;
            tempLoaderObject.transform.rotation = Quaternion.identity;
            _loader = tempLoaderObject.GetComponent<HumrRecordingLoader>();

            try
            {
                ExportTargetTakes(_currentFile.takes, _currentFile.path, currentTargetType, currentTargetName);
            }
            finally
            {
                _loader = originalLoader;
                DestroyImmediate(tempLoaderObject);
            }
        }

        private void ExportTargetTakes(
            List<RecordingTake> takes, string filePath, TargetType targetType, string targetName)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);

            var tempController = new TempControllerBuilder();
            tempController.Setup(HumrPath);

            var animTimestamp = PathUtils.GetDateTimeFromFileName(filePath);

            for (var i = 0; i < takes.Count; i++)
            {
                var takeAnimStr = $"{targetName}_{animTimestamp}_Take{i + 1}";
                AddTakeToController(takes[i], takeAnimStr, tempController);
            }

            if (!_loader.exportFbx) return;

            var originalRootBones = ApplyBlenderHipFix();
            var previousAnimControl = _loader.Animator.runtimeAnimatorController;
            try
            {
                _loader.Animator.runtimeAnimatorController = tempController.Controller;

                var exportPath = GetAssetPath("FBXs", targetName, animTimestamp, "fbx");
                ModelExporter.ExportObject(exportPath, _loader.gameObject);

                var importer = AssetImporter.GetAtPath(exportPath) as ModelImporter;
                if (importer == null) return;

                EditorUtility.FocusProjectWindow();
                var createdAsset = AssetDatabase.LoadAssetAtPath<Object>(exportPath);
                Selection.activeObject = createdAsset;
                EditorGUIUtility.PingObject(createdAsset);

                if (targetType != TargetType.BoneRotations && targetType != TargetType.Legacy) return;

                SetHumanImportSettings(importer);
                importer.SaveAndReimport();
            }
            finally
            {
                _loader.Animator.runtimeAnimatorController = previousAnimControl;
                if (originalRootBones.Count > 0)
                    foreach (var (renderer, rootBone) in originalRootBones)
                        renderer.rootBone = rootBone;
                tempController.DeleteControllerAsset();
            }
        }

        private List<(SkinnedMeshRenderer renderer, Transform rootBone)> ApplyBlenderHipFix()
        {
            var originalRootBones = new List<(SkinnedMeshRenderer renderer, Transform rootBone)>();
            if (!_loader.Animator.isHuman || !_loader.blenderHipFix) return originalRootBones;

            var hipsTransform = _loader.Animator.GetBoneTransform(HumanBodyBones.Hips);
            var skinnedRenderers = _loader.Animator.transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.rootBone == hipsTransform.parent) continue;

                originalRootBones.Add((renderer, renderer.rootBone));
                renderer.rootBone = hipsTransform.parent;
            }

            return originalRootBones;
        }

        private static void SetHumanImportSettings(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            var importerClips =
                importer.clipAnimations.Length == 0 ? importer.defaultClipAnimations : importer.clipAnimations;
            foreach (var clipAnimation in importerClips)
            {
                clipAnimation.lockRootRotation = true;
                clipAnimation.keepOriginalOrientation = true;
                clipAnimation.lockRootHeightY = true;
                clipAnimation.keepOriginalPositionY = true;
                clipAnimation.lockRootPositionXZ = true;
                clipAnimation.keepOriginalPositionXZ = true;

                if (clipAnimation.name == "") clipAnimation.name = "HUMRAnimation";
            }

            importer.clipAnimations = importerClips;
        }

        private void AddTakeToController(
            RecordingTake take, string takeAnimStr, TempControllerBuilder controllerBuilder)
        {
            AnimationClip takeClip;
            switch (take.targetType)
            {
                case TargetType.BoneRotations:
                case TargetType.Legacy:
                    takeClip = AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator);
                    break;
                case TargetType.Object:
                    takeClip = AnimationClipFactory.PopulateObjectClip(take);
                    break;
                case TargetType.Unknown:
                case TargetType.BoneRotationsWithIK:
                case TargetType.HumanMuscles:
                default:
                    throw new NotImplementedException();
            }

            if (takeClip == null) return;

            takeClip.name = takeAnimStr;
            if (_loader.exportAnim)
            {
                var animAssetPath = GetAssetPath(
                    "Animations", take.targetName, takeAnimStr, "anim");
                AnimationClipFactory.SaveGenericAnimationAsset(takeClip, animAssetPath);
            }

            controllerBuilder.AddClipToController(takeClip);
        }

        private static string GetAssetPath(string subFolder, string targetName, string fileName, string extension)
        {
            var folderPath = Path.Join(HumrPath, subFolder, PathUtils.SanitizeFileName(targetName));
            PathUtils.CreateDirectoryIfNotExist(folderPath);

            return Path.Join(folderPath, $"{fileName}.{extension}");
        }
    }
}