using System;
using System.Collections.Generic;
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

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Custom Log Path");
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                recordLoader.path = EditorGUILayout.TextField("OutputLogPath", recordLoader.path);
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
                recordLoader.path = Environment.GetEnvironmentVariable("USERPROFILE");
                recordLoader.path += @"\AppData\LocalLow\VRChat\VRChat";
            }
            
            if (recordLoader.logFileNames == null) recordLoader.CollectLogFiles();

            const string label = "Record Log";
            var controlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var popupRect = EditorGUI.PrefixLabel(controlRect, new GUIContent(label));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && popupRect.Contains(Event.current.mousePosition))
            {
                recordLoader.CollectLogFiles();
            }

            if (recordLoader.logFileNames != null && recordLoader.logFileNames.Length > 0)
            {
                recordLoader.logFileIndex = EditorGUI.Popup(popupRect, recordLoader.logFileIndex, recordLoader.logFileNames);
            }
            else
            {
                //TODO: Found x logfiles, but they don't contain HUMR data
                var emptyOptions = new string[] { "Log files not Found" };
                EditorGUI.Popup(popupRect, 0, emptyOptions);
                return;
            }
            var recordListStr = recordLoader.UniqueRecords
                .Select(entry => $"{entry.Type}: {entry.Name}")
                .ToArray();
            recordLoader.recordIndex = EditorGUILayout.Popup("Recording", recordLoader.recordIndex, recordListStr);
            
            if (GUILayout.Button("LoadLogToExportAnim"))
            {
                ExecuteEvents.Execute<OutputLogLoaderInterface>(
                    target: recordLoader.gameObject,
                    eventData: null,
                    functor: (receiveTarget, _) => receiveTarget.LoadLogToExportAnim());
            }
        }
    }
}