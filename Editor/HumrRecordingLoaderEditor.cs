using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrSakuu.Humr.Editor
{
    public enum ReplaceExistingAction
    {
        Ask = 0,
        Replace = 1,
        Rename = 2
    }

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
        private const string ReplaceExistingKey = "DrSakuu.Humr.ReplaceExisting";
        private const string AnimFpsKey = "DrSakuu.Humr.AnimFps";

        private sealed class RecordingCacheEntry
        {
            public (DateTime lastWriteTimeUtc, long length) Version;
            public RecordingFile File;
            public readonly Dictionary<(TargetType type, string name), RecordingTake[]> Takes = new();
        }

        private static readonly Dictionary<string, RecordingCacheEntry> RecordingFileCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _fpsOptions = { "24", "25", "30", "50", "60", "Custom" };
        private readonly float[] _fpsValues = { 24f, 25f, 30f, 50f, 60f };

        private RecordingFile _currentFile;
        private RecordingTake[] _currentTakes = Array.Empty<RecordingTake>();
        private int _fpsPopupIndex = 3;
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

        private static ReplaceExistingAction ReplaceExisting
        {
            get => (ReplaceExistingAction)EditorPrefs.GetInt(ReplaceExistingKey, 0);
            set => EditorPrefs.SetInt(ReplaceExistingKey, (int)value);
        }

        private static float AnimFps
        {
            get => EditorPrefs.GetFloat(AnimFpsKey, 30f);
            set => EditorPrefs.SetFloat(AnimFpsKey, value);
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

            var animFpsIndex = Array.IndexOf(_fpsValues, AnimFps);
            if (animFpsIndex >= 0)
                _fpsPopupIndex = animFpsIndex;
            else
                _fpsPopupIndex = _fpsOptions.Length - 1;
        }

        public override void OnInspectorGUI()
        {
            DrawAdvancedSection();

            var errorMessage = string.Empty;
            if (!DrawLogFilePopup())
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
                recordFileList.Add(GetRecordingCacheEntry(filePath).File);
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
            _currentTakes = Array.Empty<RecordingTake>();
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
            _currentTakes = Array.Empty<RecordingTake>();
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

            GetCurrentCacheEntry();
            _loader.targetIndex = 0;
        }

        private static (DateTime lastWriteTimeUtc, long length) GetFileVersion(string path)
        {
            var info = new FileInfo(path);
            return (info.LastWriteTimeUtc, info.Length);
        }

        private static RecordingCacheEntry GetRecordingCacheEntry(string path)
        {
            var version = GetFileVersion(path);
            if (RecordingFileCache.TryGetValue(path, out var entry) && entry.Version == version)
                return entry;

            RecordingFileCache.Remove(path);
            entry = new RecordingCacheEntry
            {
                Version = version,
                File = HumrLogParser.CreateRecordingFile(path)
            };

            if (GetFileVersion(path) == version)
                RecordingFileCache[path] = entry;
            return entry;
        }

        private static void DiscardChangedEntry(RecordingCacheEntry entry)
        {
            var path = entry.File.path;
            if (GetFileVersion(path) != entry.Version &&
                RecordingFileCache.TryGetValue(path, out var cached) && ReferenceEquals(cached, entry))
                RecordingFileCache.Remove(path);
        }

        private RecordingCacheEntry GetCurrentCacheEntry()
        {
            var entry = GetRecordingCacheEntry(_currentFile.path);
            _currentFile = entry.File;
            _recordingFiles[_loader.fileIndex] = _currentFile;
            _recordingFileNames[_loader.fileIndex] = _currentFile.fileName;
            if (_currentFile.Targets == null)
            {
                try
                {
                    _currentFile.Targets = HumrLogParser.ScanTargets(_currentFile);
                }
                finally
                {
                    DiscardChangedEntry(entry);
                }
            }
            return entry;
        }

        private void ParseTakes()
        {
            _currentTakes = Array.Empty<RecordingTake>();
            if (_currentFile == null) return;
            var entry = GetCurrentCacheEntry();
            if (_currentFile?.Targets == null || _currentFile.Targets.Length == 0) return;

            _loader.targetIndex = Mathf.Clamp(_loader.targetIndex, 0, _currentFile.Targets.Length - 1);

            var targetTuple = _currentFile.Targets[_loader.targetIndex];
            if (entry.Takes.TryGetValue(targetTuple, out var cachedTakes))
            {
                _currentTakes = cachedTakes;
                return;
            }

            try
            {
                var logLines = HumrLogParser.LoadHumrLogLines(_currentFile.path);

                if (targetTuple.type == TargetType.Legacy)
                    logLines = HumrLogParser.ConvertLegacyLines(logLines, targetTuple.name);

                _currentTakes = HumrLogParser.ParseTakes(logLines, targetTuple);
                entry.Takes[targetTuple] = _currentTakes;
            }
            finally
            {
                DiscardChangedEntry(entry);
            }
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

            ReplaceExisting = (ReplaceExistingAction)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Replace existing",
                    "Choose what happens when an .anim or .fbx asset already exists."),
                ReplaceExisting);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUI.indentLevel--;
        }

        private bool DrawLogFilePopup()
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
            if (_currentTakes.Length == 0)
            {
                SetError(ref errorMessage, "No takes found");
                return;
            }

            var fpsPopupContent = new GUIContent("Output FPS",
                "Set the frames per second of the exported .anims and clips in .fbx.");

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            _fpsPopupIndex = EditorGUILayout.Popup(fpsPopupContent, _fpsPopupIndex, _fpsOptions);

            if (EditorGUI.EndChangeCheck() && _fpsPopupIndex < _fpsValues.Length) AnimFps = _fpsValues[_fpsPopupIndex];

            if (_fpsPopupIndex == _fpsOptions.Length - 1)
                AnimFps = EditorGUILayout.FloatField(AnimFps);

            EditorGUILayout.EndHorizontal();

            AnimFps = Mathf.Max(1f, AnimFps);

            EditorGUILayout.PrefixLabel("Include in .fbx");

            using var disabledScope = new EditorGUI.DisabledScope(!validHuman);
            foreach (var take in _currentTakes)
            {
                EditorGUILayout.BeginHorizontal();
                var frameCount = take.FrameCount;
                var simpleTakeSummary = $"{take.takeName}: {take.length:F2} seconds";
                var frameInfoSummary =
                    $"{take.takeName}: {take.length:F2} seconds, {frameCount} frames, {frameCount / take.length:F2} fps";
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
            GameObject tempLoaderObject;
            tempLoaderObject = Instantiate(_loader.gameObject);

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
            animationAssetPath = ResolveExistingAssetPath(animationAssetPath, ReplaceExisting);

            AnimationClipFactory.SaveAnimationAsset(takeClip, animationAssetPath);
        }

        private AnimationClip CreateAnimationClip(RecordingTake take)
        {
            return take.targetType switch
            {
                TargetType.BoneRotations => AnimationClipFactory.PopulateHumanoidClip(take, _loader.Animator, AnimFps),
                TargetType.Legacy => AnimationClipFactory.PopulateHumanoidClip(take, _loader.Animator, AnimFps),
                TargetType.Object => AnimationClipFactory.PopulateObjectClip(take, AnimFps),
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

            if (_currentFile == null || _currentTakes.Length == 0)
            {
                HumrLogger.Error("No takes found.");
                return;
            }

            var originalLoader = _loader;
            GameObject tempLoaderObject;
            tempLoaderObject = Instantiate(_loader.gameObject);

            tempLoaderObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _loader = tempLoaderObject.GetComponent<HumrRecordingLoader>();
            if (_loader.Animator == null) _loader.gameObject.AddComponent<Animator>();

            try
            {
                PrepareFbxController(_currentTakes, _currentFile.path);
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
                TargetType.BoneRotations => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator,
                    AnimFps),
                TargetType.Legacy => AnimationClipFactory.PopulateBoneRotationsClip(take, _loader.Animator, AnimFps),
                TargetType.Object => AnimationClipFactory.PopulateObjectClip(take, AnimFps),
                _ => throw new NotImplementedException($"Unsupported target type: {take.targetType}")
            };
        }

        private void ExportControllerToFbx(
            TargetType targetType,
            string targetName,
            string logTimestamp,
            TempControllerBuilder tempController)
        {
            var animator = _loader.Animator;
            var originalRootBones = ApplyBlenderHipFix(animator);
            var previousAnimatorController = animator.runtimeAnimatorController;

            try
            {
                animator.runtimeAnimatorController = tempController.Controller;

                var fileName = $"{targetName}_{logTimestamp}";
                var exportPath = GetAssetPath("FBXs", targetName, fileName, "fbx");
                exportPath = ResolveExistingAssetPath(exportPath, ReplaceExisting);

                ExportFbx(exportPath, _loader.gameObject);

                SelectExportedAsset(exportPath);

                if (!IsHumanoidBoneTarget(targetType)) return;

                var importer = AssetImporter.GetAtPath(exportPath) as ModelImporter;
                if (importer == null) return;

                SetHumanImportSettings(importer);
                importer.SaveAndReimport();
            }
            finally
            {
                animator.runtimeAnimatorController = previousAnimatorController;
                RestoreRootBones(originalRootBones);
            }
        }

        private static void ExportFbx(string path, Object target)
        {
            // FBX Exporter 4.2.1 exposes custom export options only through its internal API.
            var exporterType = typeof(ModelExporter);
            var optionsType = exporterType.Assembly.GetType(
                "UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize", throwOnError: true);
            var options = Activator.CreateInstance(optionsType, nonPublic: true);
            optionsType.GetMethod("SetUseMayaCompatibleNames").Invoke(options, new object[] { false });

            var export = exporterType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "ExportObjects" && method.GetParameters().Length == 4);
            export.Invoke(null, new object[] { path, new Object[] { target }, options, null });
        }

        private static string ResolveExistingAssetPath(string assetPath, ReplaceExistingAction replaceExisting)
        {
            if (!File.Exists(assetPath))
                return assetPath;

            if (replaceExisting == ReplaceExistingAction.Ask)
            {
                var replace = EditorUtility.DisplayDialog(
                    "Asset Already Exists",
                    $"An asset already exists at:\n\n{assetPath}\n\nWhat would you like to do?",
                    "Replace",
                    "Rename");

                replaceExisting = replace
                    ? ReplaceExistingAction.Replace
                    : ReplaceExistingAction.Rename;
            }

            switch (replaceExisting)
            {
                case ReplaceExistingAction.Replace:
                    AssetDatabase.DeleteAsset(assetPath);
                    return assetPath;

                case ReplaceExistingAction.Rename:
                    return AssetDatabase.GenerateUniqueAssetPath(assetPath);

                default:
                    return assetPath;
            }
        }

        private static List<(SkinnedMeshRenderer renderer, Transform rootBone)> ApplyBlenderHipFix(Animator animator)
        {
            var originalRootBones = new List<(SkinnedMeshRenderer renderer, Transform rootBone)>();

            if (!BlenderHipFix || animator == null || !animator.isHuman)
                return originalRootBones;

            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsTransform == null || hipsTransform.parent == null)
                return originalRootBones;

            var armatureRoot = hipsTransform.parent;
            var skinnedRenderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>();

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
