using System;
using System.Collections.Generic;
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
        private const string NoLogsOption = "No logs found";
        private const string DefaultAnimationClipName = "HUMRAnimation";
        private const string LogPathKey = "DrSakuu.Humr.LogPath";
        private const string ShowAdvancedKey = "DrSakuu.Humr.ShowAdvanced";
        private const string BlenderHipFixKey = "DrSakuu.Humr.BlenderHipFix";
        private const string ShowFrameInfoKey = "DrSakuu.Humr.ShowFrameInfo";

        private static readonly Dictionary<string, (DateTime, RecordingFile)> RecordingFileCache = new();

        private RecordingFile _currentFile;
        private HumrRecordingLoader _loader;
        private string[] _recordingFileNames = { NoLogsOption };
        private RecordingFile[] _recordingFiles;
        private string _userProfile;

        private static string LogPath
        {
            get => EditorPrefs.GetString(LogPathKey, string.Empty);
            set => EditorPrefs.SetString(LogPathKey, value);
        }

        private static bool ShowAdvanced
        {
            get => EditorPrefs.GetBool(ShowAdvancedKey, false);
            set => EditorPrefs.SetBool(ShowAdvancedKey, value);
        }

        private static bool BlenderHipFix
        {
            get => EditorPrefs.GetBool(BlenderHipFixKey, true);
            set => EditorPrefs.SetBool(BlenderHipFixKey, value);
        }

        private static bool ShowFrameInfo
        {
            get => EditorPrefs.GetBool(ShowFrameInfoKey, false);
            set => EditorPrefs.SetBool(ShowFrameInfoKey, value);
        }

        private bool HasRecordingFiles => _recordingFiles is { Length: > 0 };
        private TargetType CurrentTargetType => _currentFile.Targets[_loader.targetIndex].type;

        private void OnEnable()
        {
            _loader = (HumrRecordingLoader)target;
            if (_loader == null) return;

            if (string.IsNullOrEmpty(LogPath))
                ResetLogPath();
            else
                UpdateRecordingFiles();
        }

        public override void OnInspectorGUI()
        {
            DrawAdvancedSection();

            var errorMessage = string.Empty;
            if (!DrawLogFileDropdown())
                SetError(ref errorMessage, "No log files found.");

            if (!TryDrawTargetSelection(ref errorMessage))
            {
                DrawError(errorMessage);
                return;
            }

            ValidateCurrentRecording(ref errorMessage);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            var validHuman = ValidateHumanAnimator();
            if (!validHuman) SetError(ref errorMessage, "The Avatar in the Animator needs to be Humanoid.");
            DrawTakeSummary(ref errorMessage, validHuman);

            DrawError(errorMessage);
            DrawExportButton(string.IsNullOrEmpty(errorMessage));
        }

        private void ResetLogPath()
        {
            _userProfile ??= Environment.GetEnvironmentVariable("USERPROFILE");
            LogPath = $"{_userProfile}{VrcLogPathSuffix}";
            EditorUtility.SetDirty(_loader);
            UpdateRecordingFiles();
        }

        private void UpdateRecordingFiles(bool clearCache = false)
        {
            if (clearCache) RecordingFileCache.Clear();

            if (string.IsNullOrEmpty(LogPath) || !Directory.Exists(LogPath))
            {
                ClearRecordingFiles();
                return;
            }

            var logFilePaths = Directory.GetFiles(LogPath, "*.txt");
            List<RecordingFile> recordFileList = new();

            foreach (var filePath in logFilePaths)
            {
                var lastWriteTime = File.GetLastWriteTime(filePath);

                if (RecordingFileCache.TryGetValue(filePath, out var cachedFile) &&
                    cachedFile.Item1 == lastWriteTime)
                {
                    recordFileList.Add(cachedFile.Item2);
                    continue;
                }

                var recordingFile = HumrLogParser.CreateRecordingFile(filePath);
                RecordingFileCache[filePath] = (lastWriteTime, recordingFile);
                recordFileList.Add(recordingFile);
            }

            _recordingFiles = recordFileList.OrderByDescending(file => file.LastWriteTime).ToArray();

            if (_recordingFiles.Length == 0)
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

        private void ClearRecordingFiles()
        {
            _currentFile = null;
            _recordingFiles = null;
            _recordingFileNames = new[] { NoLogsOption };
        }

        private void SelectFirstHumrFile()
        {
            var humrIndex = Array.FindIndex(_recordingFiles, file => file.type == LogType.Humr);
            _loader.fileIndex = humrIndex >= 0 ? humrIndex : 0;
        }

        private void SetCurrentRecordingFile()
        {
            if (!HasRecordingFiles)
            {
                _currentFile = null;
                return;
            }

            _loader.fileIndex = Mathf.Clamp(_loader.fileIndex, 0, _recordingFiles.Length - 1);
            _currentFile = _recordingFiles[_loader.fileIndex];

            ScanTargets();
            ParseTakes();
        }

        private void ScanTargets()
        {
            if (_currentFile == null) return;

            _currentFile.Targets = HumrLogParser.ScanTargets(_currentFile);
            _loader.targetIndex = 0;
        }

        private void ParseTakes()
        {
            if (_currentFile?.Targets == null || _currentFile.Targets.Length == 0) return;

            _loader.targetIndex = Mathf.Clamp(_loader.targetIndex, 0, _currentFile.Targets.Length - 1);

            var targetTuple = _currentFile.Targets[_loader.targetIndex];
            var logLines = HumrLogParser.LoadHumrLogLines(_currentFile.path);

            _currentFile.LastWriteTime = File.GetLastWriteTime(_currentFile.path);

            if (targetTuple.type == TargetType.Legacy)
                logLines = HumrLogParser.ConvertLegacyLines(logLines, targetTuple.name);

            _currentFile.takes = HumrLogParser.ParseTakes(logLines, targetTuple);
        }

        private void DrawAdvancedSection()
        {
            ShowAdvanced = EditorGUILayout.Foldout(ShowAdvanced, "Advanced settings");
            if (!ShowAdvanced) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel("Log Path");

            if (GUILayout.Button(new GUIContent("↺", "Reset to default Log Path"), GUILayout.Width(50)))
                ResetLogPath();

            if (GUILayout.Button(new GUIContent(LogPath, "Open folder...")))
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                try
                {
                    var selectedPath = EditorUtility.OpenFolderPanel("Select Log Folder", LogPath, string.Empty);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        LogPath = selectedPath;
                        EditorUtility.SetDirty(_loader);
                        UpdateRecordingFiles();
                    }
                }
                finally
                {
                    Directory.SetCurrentDirectory(currentDirectory);
                }
            }

            EditorGUILayout.EndHorizontal();

            BlenderHipFix = EditorGUILayout.Toggle(new GUIContent("Blender hip fix",
                    "If a skinned mesh renderer's Root Bone is not set to Armature, the .fbx file will import into Blender with incorrect bone structure."),
                BlenderHipFix);

            ShowFrameInfo = EditorGUILayout.Toggle(new GUIContent("Show frame info",
                    "Show number of frames and other information"),
                ShowFrameInfo);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUI.indentLevel--;
        }

        private bool DrawLogFileDropdown()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel("Recording Log File");
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                UpdateRecordingFiles(true);

            EditorGUI.BeginChangeCheck();
            _loader.fileIndex = EditorGUILayout.Popup(_loader.fileIndex, _recordingFileNames);

            EditorGUILayout.EndHorizontal();

            if (!HasRecordingFiles) return false;

            if (EditorGUI.EndChangeCheck())
                SetCurrentRecordingFile();

            return true;
        }

        private static void SetError(ref string errorMessage, string message)
        {
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = message;
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
                .Select(targetTuple => $"{targetTuple.type}: {targetTuple.name}")
                .ToArray();

            EditorGUI.BeginChangeCheck();
            _loader.targetIndex = EditorGUILayout.Popup("Recording Target", _loader.targetIndex, targetOptions);
            if (EditorGUI.EndChangeCheck())
                ParseTakes();

            return true;
        }

        private static void DrawError(string errorMessage)
        {
            if (!string.IsNullOrEmpty(errorMessage))
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
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

        private bool ValidateHumanAnimator()
        {
            if (!IsHumanoidBoneTarget(CurrentTargetType)) return true;

            var animator = _loader.Animator;
            var isHumanoidAvatar = animator != null && animator.avatar != null && animator.avatar.isHuman;
            return isHumanoidAvatar;
        }

        private static bool IsHumanoidBoneTarget(TargetType targetType)
        {
            return targetType is TargetType.BoneRotations or TargetType.Legacy;
        }

        private void DrawTakeSummary(ref string errorMessage, bool validHuman)
        {
            if (_currentFile.takes.Length == 0)
            {
                SetError(ref errorMessage, "No takes found");
                return;
            }

            EditorGUILayout.PrefixLabel("Include in .fbx");

            using var disabledScope = new EditorGUI.DisabledScope(!validHuman);
            foreach (var take in _currentFile.takes)
            {
                EditorGUILayout.BeginHorizontal();
                var frameCount = take.frameTimes.Length;
                var simpleTakeSummary = $"{take.takeName}: {take.length:F2} seconds";
                var frameInfoSummary = $"{take.takeName}: {take.length:F2} seconds, {frameCount} frames";
                var takeContent = new GUIContent(ShowFrameInfo ? frameInfoSummary : simpleTakeSummary);
                take.includeInFbx = GUILayout.Toggle(take.includeInFbx, takeContent);
                if (GUILayout.Button(new GUIContent("Export .anim"), GUILayout.Width(100)))
                {
                    var animationTimestamp = PathUtils.GetDateTimeFromFileName(_currentFile.fileName);
                    ExportAnim(take, animationTimestamp);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void ExportAnim(RecordingTake take, string logTimestamp)
        {
            var originalLoader = _loader;
            var tempLoaderObject = Instantiate(_loader.gameObject);

            tempLoaderObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _loader = tempLoaderObject.GetComponent<HumrRecordingLoader>();
            if (_loader.Animator == null) _loader.gameObject.AddComponent<Animator>();

            AnimationClip takeClip;
            try
            {
                takeClip = CreateAnimationClip(take);
            }
            finally
            {
                _loader = originalLoader;
                DestroyImmediate(tempLoaderObject);
            }

            if (takeClip == null) return;

            var animationName = PathUtils.BuildAnimationName(take, logTimestamp);
            takeClip.name = animationName;
            var animationAssetPath = GetAssetPath(
                "Animations", take.targetName, animationName, "anim");
            AnimationClipFactory.SaveAnimationAsset(takeClip, animationAssetPath);
        }

        private AnimationClip CreateAnimationClip(RecordingTake take)
        {
            return take.targetType switch
            {
                TargetType.BoneRotations => AnimationClipFactory.PopulateHumanoidClip(take, _loader.Animator),
                TargetType.Legacy => AnimationClipFactory.PopulateHumanoidClip(take, _loader.Animator),
                TargetType.Object => AnimationClipFactory.PopulateObjectClip(take),
                _ => throw new NotImplementedException($"Unsupported target type: {take.targetType}")
            };
        }

        private static string GetAssetPath(string subFolder, string targetName, string fileName, string extension)
        {
            var folderPath = Path.Join(HumrPath, subFolder, PathUtils.SanitizeFileName(targetName));
            PathUtils.CreateDirectoryIfNotExist(folderPath);

            return Path.Join(folderPath, $"{fileName}.{extension}");
        }

        private void DrawExportButton(bool enabled)
        {
            using var disabledScope = new EditorGUI.DisabledScope(!enabled);

            if (GUILayout.Button("Export .fbx"))
                ExportFbx();
        }

        private void ExportFbx()
        {
            if (!ValidateHumanAnimator())
            {
                HumrLogger.Error("The Avatar in the Animator needs to be Humanoid.");
                return;
            }

            if (_currentFile?.takes == null || _currentFile.takes.Length == 0)
            {
                HumrLogger.Error("No takes found.");
                return;
            }

            var originalLoader = _loader;
            var tempLoaderObject = Instantiate(_loader.gameObject);

            tempLoaderObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _loader = tempLoaderObject.GetComponent<HumrRecordingLoader>();
            if (_loader.Animator == null) _loader.gameObject.AddComponent<Animator>();

            try
            {
                PrepareFbxController(_currentFile.takes, _currentFile.path);
            }
            finally
            {
                _loader = originalLoader;
                DestroyImmediate(tempLoaderObject);
            }
        }

        private void PrepareFbxController(RecordingTake[] takes, string filePath)
        {
            PathUtils.CreateDirectoryIfNotExist(HumrPath);

            var tempController = new TempControllerBuilder();
            tempController.Setup(HumrPath);

            try
            {
                var targetType = takes[0].targetType;
                var targetName = takes[0].targetName;
                foreach (var take in takes)
                    if (take.includeInFbx)
                        AddTakeToController(take, filePath, tempController);

                var logTimestamp = PathUtils.GetDateTimeFromFileName(filePath);
                ExportControllerToFbx(targetType, targetName, logTimestamp, tempController);
            }
            finally
            {
                tempController.DeleteControllerAsset();
            }
        }

        private void AddTakeToController(RecordingTake take, string filePath, TempControllerBuilder controllerBuilder)
        {
            var takeClip = CreateFbxClip(take);
            if (takeClip == null) return;

            var animationName = PathUtils.BuildAnimationName(take, filePath);
            takeClip.name = animationName;
            controllerBuilder.AddClipToController(takeClip);
        }

        private AnimationClip CreateFbxClip(RecordingTake take)
        {
            return take.targetType switch
            {
                TargetType.BoneRotations => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator),
                TargetType.Legacy => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator),
                TargetType.Object => AnimationClipFactory.PopulateObjectClip(take),
                _ => throw new NotImplementedException($"Unsupported target type: {take.targetType}")
            };
        }

        private void ExportControllerToFbx(
            TargetType targetType,
            string targetName,
            string logTimestamp,
            TempControllerBuilder tempController)
        {
            var originalRootBones = ApplyBlenderHipFix();
            var previousAnimatorController = _loader.Animator.runtimeAnimatorController;

            try
            {
                _loader.Animator.runtimeAnimatorController = tempController.Controller;

                var fileName = $"{targetName}_{logTimestamp}";
                var exportPath = GetAssetPath("FBXs", targetName, fileName, "fbx");
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

            if (!BlenderHipFix || _loader.Animator == null || !_loader.Animator.isHuman)
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

        private static void SelectExportedAsset(string exportPath)
        {
            EditorUtility.FocusProjectWindow();

            var createdAsset = AssetDatabase.LoadAssetAtPath<Object>(exportPath);
            if (createdAsset == null) return;

            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
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

        private static void RestoreRootBones(
            IEnumerable<(SkinnedMeshRenderer renderer, Transform rootBone)> originalRootBones)
        {
            foreach (var (renderer, rootBone) in originalRootBones)
                renderer.rootBone = rootBone;
        }
    }
}