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
        private bool _showAdvanced;
        private const string VrcLogPathSuffix = @"\AppData\LocalLow\VRChat\VRChat";
        
        public override void OnInspectorGUI()
        {
            var recordLoader = (HumrRecordingLoader)target;
            if (recordLoader == null) return;
        
            UpdateLogDirectory(recordLoader);
            DrawAdvancedPathSection(recordLoader);
            
            InitializeLogs(recordLoader);
        
            DrawLogFileDropdown(recordLoader);
            if (!DrawRecordingTargetDropdown(recordLoader)) return;
        
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            
            GUILayout.Label($"Found ");
            
            recordLoader.exportGenericAnimation = GUILayout.Toggle(recordLoader.exportGenericAnimation, "Export Generic Animation");
            
            DrawExportButton(recordLoader);
        }
        
        private void UpdateLogDirectory(HumrRecordingLoader recordLoader)
        {
            if (_showAdvanced) return;
            
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            var defaultPath = $"{userProfile}{VrcLogPathSuffix}";
            
            if (recordLoader.logFileDirectory != null && recordLoader.logFileDirectory != defaultPath)
            {
                recordLoader.logFileDirectory = defaultPath;
            }
        }
        
        private void DrawAdvancedPathSection(HumrRecordingLoader recordLoader)
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Custom Log Path");
            if (!_showAdvanced) return;
        
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            
            recordLoader.logFileDirectory = EditorGUILayout.TextField("Output Log Path (resets when closed)", recordLoader.logFileDirectory);
            
            if (GUILayout.Button("Explore", GUILayout.Width(100)))
            {
                OpenLogFolder(recordLoader.logFileDirectory);
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
                UnityEngine.Debug.LogError($"Log path does not exist: {path}");
            }
        }
        
        private static void InitializeLogs(HumrRecordingLoader recordLoader)
        {
            if (recordLoader.recordingFiles != null) return; // Inverted if
            
            recordLoader.CollectLogFiles();
        }
        
        private static void DrawLogFileDropdown(HumrRecordingLoader recordLoader)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(rect, new GUIContent("Recording Log File"));
            
            if (IsContextClick(popupRect))
            {
                recordLoader.CollectLogFiles();
            }
        
            if (recordLoader.recordingFiles == null || recordLoader.recordingFiles.Count == 0)
            {
                EditorGUI.Popup(popupRect, 0, new string[] { "No logs found." });
                return;
            }

            EditorGUI.BeginChangeCheck();
            var fileNames = new List<string>();
            foreach (var recordingFile in recordLoader.recordingFiles)
            {
                fileNames.Add(recordingFile.fileName);
            }
            var selectedIndex = EditorGUI.Popup(popupRect, recordLoader.fileIndex, fileNames.ToArray());
            
            if (!EditorGUI.EndChangeCheck()) return; // Only update if value changed
            
            recordLoader.currentFile = recordLoader.recordingFiles[selectedIndex];
            recordLoader.CollectTargetNames();
            recordLoader.targetIndex = 0;
            recordLoader.CollectTakes();
        }
        
        private static bool DrawRecordingTargetDropdown(HumrRecordingLoader recordLoader)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(rect, new GUIContent("Recording Target"));
            
            if (IsContextClick(popupRect))
            {
                recordLoader.CollectTargetNames();
                recordLoader.CollectTakes();
            }

            var targets = recordLoader.recordingFiles[recordLoader.targetIndex].targetNames;
            if (targets == null || targets.Length == 0)
            {
                EditorGUI.Popup(popupRect, 0, new string[] { "Recording data is corrupted." });
                return false;
            }

            EditorGUI.BeginChangeCheck();
            var index = Mathf.Clamp(recordLoader.targetIndex, 0, targets.Length - 1);
            recordLoader.targetIndex = EditorGUI.Popup(popupRect, index, targets);
            
            if (EditorGUI.EndChangeCheck())
            {
                recordLoader.CollectTakes();
            }
            
            return true;
        }
        
        private static void DrawExportButton(HumrRecordingLoader recordLoader)
        {
            if (!GUILayout.Button("Load recording and export .fbx")) return;
            
            if (recordLoader.TryGetComponent<RecordLogLoaderInterface>(out var receiver))
            {
                receiver.LoadRecordingAndExportAnim();
            }
        }
        
        private static bool IsContextClick(Rect rect)
        {
            return Event.current.type == EventType.MouseDown && 
                   Event.current.button == 0 && 
                   rect.Contains(Event.current.mousePosition);
        }
    }
#endif
}