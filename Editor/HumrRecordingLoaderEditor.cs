using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace DrSakuu.Humr.Editor
{
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : UnityEditor.Editor
    {
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";
        private const string HumrPath = @"Assets\HUMR";
        private const string NoLogsOption = "No logs found";
        private const string DefaultAnimationClipName = "HUMRAnimation";

        private static readonly GUIContent BlenderHipFixContent = new(
            "Blender hip fix",
            "If a skinned mesh renderer's Root Bone is not set to Armature, the .fbx file will import into Blender with incorrect bone structure.");

        private RecordingFile _currentFile;
        private HumrRecordingLoader _loader;
        private string[] _recordingFileNames = { NoLogsOption };
        private List<RecordingFile> _recordingFiles = new();
        private string _userProfile;

        private bool HasRecordingFiles => _recordingFiles is { Count: > 0 };

        private TargetType CurrentTargetType => _currentFile.Targets[_loader.targetIndex].targetType;

        public override void OnInspectorGUI()
        {
            _loader = (HumrRecordingLoader)target;
            if (_loader == null) return;

            var errorMessage = string.Empty;

            DrawLogFileSelection(ref errorMessage);
            if (!TryDrawTargetSelection(ref errorMessage))
            {
                DrawError(errorMessage);
                return;
            }

            ValidateCurrentRecording(ref errorMessage);
            DrawTakeSummary();
            DrawHumanoidOptions(ref errorMessage);
            DrawExportOptions(ref errorMessage);
            DrawError(errorMessage);
            DrawExportButton(string.IsNullOrEmpty(errorMessage));
        }

        public void UpdateRecordingFiles()
        {
            if (string.IsNullOrEmpty(_loader.logPath) || !Directory.Exists(_loader.logPath))
            {
                ClearRecordingFiles();
                return;
            }

            var logFilePaths = Directory.GetFiles(_loader.logPath, "*.txt");
            if (logFilePaths.Length == _recordingFiles.Count) return;

            _recordingFiles = HumrLogParser.CollectRecordingFiles(logFilePaths);
            if (_recordingFiles.Count == 0)
            {
                ClearRecordingFiles();
                return;
            }

            _recordingFileNames = _recordingFiles
                .Select(file => file.fileName)
                .ToArray();

            SelectFirstHumrFile();
            SetCurrentRecordingFile();
        }

        public void SetCurrentRecordingFile()
        {
            if (!HasRecordingFiles)
            {
                _currentFile = null;
                return;
            }

            _loader.fileIndex = Mathf.Clamp(_loader.fileIndex, 0, _recordingFiles.Count - 1);
            _currentFile = _recordingFiles[_loader.fileIndex];

            ScanTargets();
            ParseTakes();
        }

        public void ScanTargets()
        {
            if (_currentFile == null) return;

            _currentFile.Targets = HumrLogParser.ScanTargets(_currentFile);
            _loader.targetIndex = 0;
        }

        public void ParseTakes()
        {
            if (_currentFile?.Targets == null || _currentFile.Targets.Length == 0) return;

            _loader.targetIndex = Mathf.Clamp(_loader.targetIndex, 0, _currentFile.Targets.Length - 1);

            var target = _currentFile.Targets[_loader.targetIndex];
            var logLines = HumrLogParser.LoadHumrLogLines(_currentFile.path);

            _currentFile.LastWriteTime = File.GetLastWriteTime(_currentFile.path);

            if (target.targetType == TargetType.Legacy)
                logLines = HumrLogParser.ConvertLegacyLines(logLines, target.name);

            _currentFile.takes = HumrLogParser.ParseTakes(logLines, (target.targetType, target.name));
            _currentFile.foundTakesStr = BuildTakeSummary(_currentFile.takes?.Count ?? 0);
        }

        private void DrawLogFileSelection(ref string errorMessage)
        {
            UpdateLogDirectory();
            DrawAdvancedPathSection();
            UpdateRecordingFiles();

            if (!DrawLogFileDropdown())
                SetError(ref errorMessage, "No log files found.");
        }

        private bool TryDrawTargetSelection(ref string errorMessage)
        {
            if (_currentFile == null)
            {
                SetError(ref errorMessage, "No log file selected.");
                return false;
            }

            if (_currentFile.Targets == null)
            {
                SetError(ref errorMessage, "Please select the log file again.");
                ScanTargets();
                return false;
            }

            if (_currentFile.Targets.Length == 0)
            {
                SetError(ref errorMessage, "No recording targets found.");
                return false;
            }

            _loader.targetIndex = Mathf.Clamp(_loader.targetIndex, 0, _currentFile.Targets.Length - 1);

            var targetOptions = _currentFile.Targets
                .Select(target => $"{target.targetType}: {target.name}")
                .ToArray();

            EditorGUI.BeginChangeCheck();
            _loader.targetIndex = EditorGUILayout.Popup("Recording Target", _loader.targetIndex, targetOptions);
            if (EditorGUI.EndChangeCheck())
                ParseTakes();

            return true;
        }

        private void ValidateCurrentRecording(ref string errorMessage)
        {
            switch (_currentFile.type)
            {
                case LogType.NoData:
                    SetError(ref errorMessage, "No HUMR data found.");
                    break;
                case LogType.Corrupt:
                    SetError(ref errorMessage, "HUMR data is corrupt.");
                    break;
            }
        }

        private void DrawTakeSummary()
        {
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(_currentFile.foundTakesStr);
        }

        private void DrawHumanoidOptions(ref string errorMessage)
        {
            if (!IsHumanoidBoneTarget(CurrentTargetType)) return;

            var animator = _loader.Animator;
            var isHumanoidAvatar = animator != null && animator.avatar != null && animator.avatar.isHuman;
            if (!isHumanoidAvatar)
                SetError(ref errorMessage, "The Avatar needs to be Humanoid.");

            _loader.blenderHipFix = GUILayout.Toggle(_loader.blenderHipFix, BlenderHipFixContent);
        }

        private void DrawExportOptions(ref string errorMessage)
        {
            _loader.exportFbx = GUILayout.Toggle(_loader.exportFbx, "Export .fbx");
            _loader.exportAnim = GUILayout.Toggle(_loader.exportAnim, "Export .anim");

            if (!_loader.exportFbx && !_loader.exportAnim)
                SetError(ref errorMessage, "Select either .fbx or .anim export.");
        }

        private void DrawAdvancedPathSection()
        {
            _loader.showAdvanced = EditorGUILayout.Foldout(_loader.showAdvanced, "Advanced: Custom Log Path");
            if (!_loader.showAdvanced) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            _loader.logPath = EditorGUILayout.TextField("Output Log Path (resets when closed)", _loader.logPath);
            if (GUILayout.Button("Explore", GUILayout.Width(100)))
                ExploreLogFolder(_loader.logPath);

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
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

            if (!HasRecordingFiles) return false;

            if (EditorGUI.EndChangeCheck())
                SetCurrentRecordingFile();

            return true;
        }

        private void DrawExportButton(bool enabled)
        {
            using var disabledScope = new EditorGUI.DisabledScope(!enabled);

            if (GUILayout.Button("Export recording"))
                ExportCurrentTargetTakes();
        }

        private void ExportCurrentTargetTakes()
        {
            if (_loader.Animator == null || _currentFile?.takes == null || _currentFile.takes.Count == 0)
                return;

            var target = _currentFile.Targets[_loader.targetIndex];

            var originalLoader = _loader;
            var tempLoaderObject = Instantiate(_loader.gameObject);

            tempLoaderObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _loader = tempLoaderObject.GetComponent<HumrRecordingLoader>();

            try
            {
                ExportTargetTakes(_currentFile.takes, _currentFile.path, target.targetType, target.name);
            }
            finally
            {
                _loader = originalLoader;
                DestroyImmediate(tempLoaderObject);
            }
        }

        private void ExportTargetTakes(
            List<RecordingTake> takes,
            string filePath,
            TargetType targetType,
            string targetName)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);

            var tempController = new TempControllerBuilder();
            tempController.Setup(HumrPath);

            try
            {
                var animationTimestamp = PathUtils.GetDateTimeFromFileName(filePath);
                AddTakesToController(takes, targetName, animationTimestamp, tempController);

                if (_loader.exportFbx)
                    ExportFbx(targetType, targetName, animationTimestamp, tempController);
            }
            finally
            {
                tempController.DeleteControllerAsset();
            }
        }

        private void AddTakesToController(
            IReadOnlyList<RecordingTake> takes,
            string targetName,
            string animationTimestamp,
            TempControllerBuilder controllerBuilder)
        {
            for (var i = 0; i < takes.Count; i++)
            {
                var animationName = $"{targetName}_{animationTimestamp}_Take{i + 1}";
                AddTakeToController(takes[i], animationName, controllerBuilder);
            }
        }

        private void ExportFbx(
            TargetType targetType,
            string targetName,
            string animationTimestamp,
            TempControllerBuilder tempController)
        {
            var originalRootBones = ApplyBlenderHipFix();
            var previousAnimatorController = _loader.Animator.runtimeAnimatorController;

            try
            {
                _loader.Animator.runtimeAnimatorController = tempController.Controller;

                var exportPath = GetAssetPath("FBXs", targetName, animationTimestamp, "fbx");
                ModelExporter.ExportObject(exportPath, _loader.gameObject);

                SelectExportedAsset(exportPath);

                if (!IsHumanoidBoneTarget(targetType)) return;

                var importer = AssetImporter.GetAtPath(exportPath) as ModelImporter;
                if (importer == null) return;

                SetHumanImportSettings(importer);
                importer.SaveAndReimport();
            }
            finally
            {
                _loader.Animator.runtimeAnimatorController = previousAnimatorController;
                RestoreRootBones(originalRootBones);
            }
        }

        private List<(SkinnedMeshRenderer renderer, Transform rootBone)> ApplyBlenderHipFix()
        {
            var originalRootBones = new List<(SkinnedMeshRenderer renderer, Transform rootBone)>();

            if (!_loader.blenderHipFix || _loader.Animator == null || !_loader.Animator.isHuman)
                return originalRootBones;

            var hipsTransform = _loader.Animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform == null || hipsTransform.parent == null)
                return originalRootBones;

            var armatureRoot = hipsTransform.parent;
            var skinnedRenderers = _loader.Animator.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.rootBone == armatureRoot) continue;

                originalRootBones.Add((renderer, renderer.rootBone));
                renderer.rootBone = armatureRoot;
            }

            return originalRootBones;
        }

        private void AddTakeToController(
            RecordingTake take,
            string animationName,
            TempControllerBuilder controllerBuilder)
        {
            var takeClip = CreateAnimationClip(take);
            if (takeClip == null) return;

            takeClip.name = animationName;

            if (_loader.exportAnim)
            {
                var animationAssetPath = GetAssetPath("Animations", take.targetName, animationName, "anim");
                AnimationClipFactory.SaveGenericAnimationAsset(takeClip, animationAssetPath);
            }

            controllerBuilder.AddClipToController(takeClip);
        }

        private AnimationClip CreateAnimationClip(RecordingTake take)
        {
            return take.targetType switch
            {
                TargetType.BoneRotations => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator),
                TargetType.Legacy => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator),
                TargetType.Object => AnimationClipFactory.PopulateObjectClip(take),
                _ => throw new NotImplementedException($"Unsupported target type: {take.targetType}")
            };
        }

        private void UpdateLogDirectory()
        {
            if (_loader.showAdvanced) return;

            _userProfile ??= Environment.GetEnvironmentVariable("USERPROFILE");
            _loader.logPath = $"{_userProfile}{VrcLogPathSuffix}";
        }

        private void SelectFirstHumrFile()
        {
            var humrIndex = _recordingFiles.FindIndex(file => file.type == LogType.Humr);
            if (humrIndex >= 0)
                _loader.fileIndex = humrIndex;
        }

        private void ClearRecordingFiles()
        {
            _currentFile = null;
            _recordingFiles.Clear();
            _recordingFileNames = new[] { NoLogsOption };
        }

        private static void DrawClickableDropdown(
            string label,
            Action onClick,
            Func<int> getSelectedIndex,
            Action<int> setSelectedIndex,
            string[] options)
        {
            var lineRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(lineRect, new GUIContent(label));

            if (IsRectClick(popupRect))
                onClick?.Invoke();

            setSelectedIndex(EditorGUI.Popup(popupRect, getSelectedIndex(), options));
        }

        private static bool IsRectClick(Rect rect)
        {
            var currentEvent = Event.current;

            return currentEvent.type == EventType.MouseDown &&
                   currentEvent.button == 0 &&
                   rect.Contains(currentEvent.mousePosition);
        }

        private static void ExploreLogFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                HumrLogger.Error($"Log path does not exist: {path}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private static void SelectExportedAsset(string exportPath)
        {
            EditorUtility.FocusProjectWindow();

            var createdAsset = AssetDatabase.LoadAssetAtPath<Object>(exportPath);
            if (createdAsset == null) return;

            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
        }

        private static void RestoreRootBones(
            IEnumerable<(SkinnedMeshRenderer renderer, Transform rootBone)> originalRootBones)
        {
            foreach (var (renderer, rootBone) in originalRootBones)
                renderer.rootBone = rootBone;
        }

        private static void SetHumanImportSettings(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;

            var clipAnimations = importer.clipAnimations.Length == 0
                ? importer.defaultClipAnimations
                : importer.clipAnimations;

            foreach (var clipAnimation in clipAnimations)
            {
                clipAnimation.lockRootRotation = true;
                clipAnimation.keepOriginalOrientation = true;
                clipAnimation.lockRootHeightY = true;
                clipAnimation.keepOriginalPositionY = true;
                clipAnimation.lockRootPositionXZ = true;
                clipAnimation.keepOriginalPositionXZ = true;

                if (string.IsNullOrEmpty(clipAnimation.name))
                    clipAnimation.name = DefaultAnimationClipName;
            }

            importer.clipAnimations = clipAnimations;
        }

        private static bool IsHumanoidBoneTarget(TargetType targetType)
        {
            return targetType is TargetType.BoneRotations or TargetType.Legacy;
        }

        private static string BuildTakeSummary(int takeCount)
        {
            return takeCount switch
            {
                1 => "Found 1 take.",
                _ => $"Found {takeCount} takes."
            };
        }

        private static void DrawError(string errorMessage)
        {
            if (!string.IsNullOrEmpty(errorMessage))
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
        }

        private static void SetError(ref string errorMessage, string message)
        {
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = message;
        }

        private static string GetAssetPath(string subFolder, string targetName, string fileName, string extension)
        {
            var folderPath = Path.Join(HumrPath, subFolder, PathUtils.SanitizeFileName(targetName));
            PathUtils.CreateDirectoryIfNotExist(folderPath);

            return Path.Join(folderPath, $"{fileName}.{extension}");
        }
    }
}