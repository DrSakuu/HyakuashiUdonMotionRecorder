using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HUMR
{
    // Editor components prevent world build 
#if UNITY_EDITOR
    [CustomEditor(typeof(HumrRecordingLoader))]
    public class HumrRecordingLoaderEditor : Editor
    {
        private bool _showAdvanced;
        
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
            
            recordLoader.exportGenericAnimation = GUILayout.Toggle(recordLoader.exportGenericAnimation, "Export Generic Animation");
            
            DrawExportButton(recordLoader);
        }
        
        private void UpdateLogDirectory(HumrRecordingLoader recordLoader)
        {
            if (_showAdvanced) return;
            
            var defaultPath = Environment.GetEnvironmentVariable("USERPROFILE") + @"\AppData\LocalLow\VRChat\VRChat";
            if (recordLoader != null && recordLoader.logFileDirectory != defaultPath)
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
            if (recordLoader.recordingFileNames == null)
            {
                recordLoader.CollectLogFiles();
                recordLoader.CollectRecordings();
            }
        
            if (recordLoader.recordings == null)
            {
                recordLoader.CollectRecordings();
            }
        }
        
        private static void DrawLogFileDropdown(HumrRecordingLoader recordLoader)
        {
            var controlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(controlRect, new GUIContent("Recording Log File"));
            
            if (IsContextClick(popupRect))
            {
                recordLoader.CollectLogFiles();
                recordLoader.CollectRecordings();
            }
        
            if (recordLoader.recordingFileNames != null && recordLoader.recordingFileNames.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                var selectedIndex = EditorGUI.Popup(popupRect, recordLoader.recordingFileIndex, recordLoader.recordingFileNames);
                if (!EditorGUI.EndChangeCheck()) return;
                
                recordLoader.recordingFileIndex = selectedIndex;
                recordLoader.CollectRecordings();
                recordLoader.recordingIndex = 0;
            }
            else
            {
                var logFilesCount = recordLoader.logFilePaths?.Length ?? 0;
                var noRecordsMessage = logFilesCount > 0 
                    ? $"Found {logFilesCount} log files but they don't have HUMR recordings." 
                    : "No logs found.";
                
                EditorGUI.Popup(popupRect, 0, new string[] { noRecordsMessage });
            }
        }
        
        private static bool DrawRecordingTargetDropdown(HumrRecordingLoader recordLoader)
        {
            var controlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(controlRect, new GUIContent("Recording Target"));
            
            if (IsContextClick(popupRect))
            {
                recordLoader.CollectRecordings();
            }
        
            if (recordLoader.recordings != null && recordLoader.recordings.Count > 0)
            {
                var recordListStr = recordLoader.recordings
                    .Select(entry => $"{entry.type}: {entry.target}")
                    .ToArray();
        
                recordLoader.recordingIndex = Mathf.Clamp(recordLoader.recordingIndex, 0, recordListStr.Length - 1);
                recordLoader.recordingIndex = EditorGUI.Popup(popupRect, recordLoader.recordingIndex, recordListStr);
                return true;
            }
        
            EditorGUI.Popup(popupRect, 0, new string[] { "Recording data is corrupted." });
            return false;
        
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