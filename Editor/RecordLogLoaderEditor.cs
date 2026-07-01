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
    [CustomEditor(typeof(RecordLogLoader))]
    public class RecordLogLoaderEditor : Editor
    {
        private bool _showAdvanced;
        
        public override void OnInspectorGUI()
        {
            
            var recordLoader = (RecordLogLoader)target;
            if (recordLoader == null) return;

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Custom Log Path");
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                
                recordLoader.logFileDirectory = EditorGUILayout.TextField("OutputLogPath", recordLoader.logFileDirectory);
                if (GUILayout.Button("Explore", GUILayout.Width(100)))
                {
                    var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\AppData\LocalLow\VRChat\VRChat";
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
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            else
            {
                recordLoader.logFileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");
                recordLoader.logFileDirectory += @"\AppData\LocalLow\VRChat\VRChat";
            }
            
            if (recordLoader.recordFileNames == null)
            {
                recordLoader.CollectLogFiles();
                recordLoader.CollectRecordings();
            }

            const string recordFileLabel = "Record Log File";
            var recordFileControlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var recordFilePopupRect = EditorGUI.PrefixLabel(recordFileControlRect, new GUIContent(recordFileLabel));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && recordFilePopupRect.Contains(Event.current.mousePosition))
            {
                recordLoader.CollectLogFiles();
                recordLoader.CollectRecordings();
            }

            if (recordLoader.recordFileNames != null && recordLoader.recordFileNames.Length > 0)
            {
                recordLoader.recordFileIndex = EditorGUI.Popup(recordFilePopupRect, recordLoader.recordFileIndex, recordLoader.recordFileNames);
            }
            else
            {
                var logFilesCount = recordLoader.logFilePaths.Length;
                var noRecordsMessage = logFilesCount > 0 ? $"Found {logFilesCount} log files but they don't have HUMR recordings." : "No logs found.";
                var emptyOptions = new string[] { noRecordsMessage };
                EditorGUI.Popup(recordFilePopupRect, 0, emptyOptions);
                return;
            }
            
            if (recordLoader.recordings == null) recordLoader.CollectRecordings();
            
            const string recordingsLabel = "Recording Target";
            var recordingControlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var recordingPopupRect = EditorGUI.PrefixLabel(recordingControlRect, new GUIContent(recordingsLabel));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && recordingPopupRect.Contains(Event.current.mousePosition))
            {
                recordLoader.CollectRecordings();
            }

            var recordListStr = recordLoader.recordings
                .Select(entry => $"{entry.type}: {entry.name}")
                .ToArray();
            recordLoader.recordingIndex = EditorGUI.Popup(recordingPopupRect, recordLoader.recordingIndex, recordListStr);

            recordLoader.exportGenericAnimation = GUILayout.Toggle(recordLoader.exportGenericAnimation, "Export Generic Animation");
            
            if (!GUILayout.Button("LoadLogToExportAnim")) return;
            
            if (recordLoader.TryGetComponent<RecordLogLoaderInterface>(out var receiver))
            {
                receiver.LoadRecordingAndExportAnim();
            }
        }
    }
#endif
}