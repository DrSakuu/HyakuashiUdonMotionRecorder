using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HUMR
{
    // Editor components prevent world build 
#if UNITY_EDITOR
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : Editor
    {
        private HumrRecordingLoader _recordLoader;
        private bool _showAdvanced;
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";
        private string _userProfile;
        
        public override void OnInspectorGUI()
        {
            _recordLoader = (HumrRecordingLoader)target;
            if (_recordLoader == null) return;
        
            UpdateLogDirectory();
            DrawAdvancedPathSection();
            _recordLoader.UpdateRecordingFiles();
            DrawLogFileDropdown();
            _recordLoader.targetIndex = EditorGUILayout.Popup(
                "Recording Target", _recordLoader.targetIndex, _recordLoader.currentFile.targetNames);
            if (_recordLoader.currentFile.type != LogType.Humr && _recordLoader.currentFile.type != LogType.Legacy) return;

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label(_recordLoader.currentFile.foundTakesStr);
            _recordLoader.exportGenericAnimation = GUILayout.Toggle(_recordLoader.exportGenericAnimation, "Export Generic Animation");
            DrawExportButton();
        }
        
        private void UpdateLogDirectory()
        {
            if (_showAdvanced) return;
            
            if (_userProfile == null) _userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            _recordLoader.logFileDirectory = $"{_userProfile}{VrcLogPathSuffix}";
        }
        
        private void DrawAdvancedPathSection()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Custom Log Path");
            if (!_showAdvanced) return;
        
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            
            _recordLoader.logFileDirectory = EditorGUILayout.TextField("Output Log Path (resets when closed)", 
                _recordLoader.logFileDirectory);
            
            if (GUILayout.Button("Explore", GUILayout.Width(100)))
            {
                OpenLogFolder(_recordLoader.logFileDirectory);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
        
        private static void OpenLogFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                HumrLogger.Error($"Log path does not exist: {path}");
            }
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
            {
                onClick?.Invoke();
            }

            setSelectedIndex(EditorGUI.Popup(popupRect, getSelectedIndex(), options));
        }

        private void DrawLogFileDropdown()
        {
            EditorGUI.BeginChangeCheck();
            DrawClickableDropdown(
                "Recording Log File",
                _recordLoader.UpdateRecordingFiles,
                () => _recordLoader.fileIndex,
                value => _recordLoader.fileIndex = value,
                _recordLoader.recordingFileNames);
            if (EditorGUI.EndChangeCheck()) _recordLoader.SetCurrentRecordingFile();
        }

        private void DrawExportButton()
        {
            if (!GUILayout.Button("Load recording and export .fbx")) return;
            
            if (_recordLoader.TryGetComponent<RecordLogLoaderInterface>(out var receiver))
            {
                receiver.LoadRecordingAndExportAnim();
            }
        }

        private static bool IsRectClick(Rect rect)
        {
            return Event.current.type == EventType.MouseDown && 
                   Event.current.button == 0 && 
                   rect.Contains(Event.current.mousePosition);
        }
    }
#endif
}