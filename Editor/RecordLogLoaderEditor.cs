using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HUMR
{
    [CustomEditor(typeof(RecordLogLoader))]
    public class RecordLogLoaderEditor : Editor
    {
        private bool _showAdvanced;
        
        public override void OnInspectorGUI()
        {
            
            var recordLoader = (RecordLogLoader)target;
            if (recordLoader == null) return;

            // TODO: Retrieve serialized values
            // serializedObject.Update();
            // var outputPathProp = serializedObject.FindProperty("logFileDirectory");

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
            
            if (recordLoader.recordFileNames == null) recordLoader.CollectLogFiles();

            const string label = "Record Log File";
            var controlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(controlRect, new GUIContent(label));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && popupRect.Contains(Event.current.mousePosition))
            {
                recordLoader.CollectLogFiles();
            }

            if (recordLoader.recordFileNames != null && recordLoader.recordFileNames.Length > 0)
            {
                recordLoader.logFileIndex = EditorGUI.Popup(popupRect, recordLoader.logFileIndex, recordLoader.recordFileNames);
            }
            else
            {
                var logFilesCount = recordLoader.logFilePaths.Length;
                var noRecordsMessage = logFilesCount > 0 ? $"Found {logFilesCount} log files but they don't have HUMR recordings." : "No logs found.";
                var emptyOptions = new string[] { noRecordsMessage };
                EditorGUI.Popup(popupRect, 0, emptyOptions);
                return;
            }
            var recordListStr = recordLoader.uniqueRecords
                .Select(entry => $"{entry.type}: {entry.name}")
                .ToArray();
            recordLoader.recordIndex = EditorGUILayout.Popup("Recording", recordLoader.recordIndex, recordListStr);
            
            if (GUILayout.Button("LoadLogToExportAnim"))
            {
                ExecuteEvents.Execute<OutputLogLoaderInterface>(
                    target: recordLoader.gameObject,
                    eventData: null,
                    functor: (receiveTarget, _) => receiveTarget.LoadLogToExportAnim());
            }
            // TODO: serializedObject.ApplyModifiedProperties();
        }
    }
}