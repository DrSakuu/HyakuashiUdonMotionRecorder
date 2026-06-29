/*******
 * OutputLogLoaderEditor.cs
 * 
 * Editor拡張。Editorフォルダの下に配置すること
 * 
 * OutputLogLoader.csをアタッチした際のInspectorに表示される項目を拡張している
 * 
 * FBXExporterの有無を確認し、存在するOutputLogをプルダウンに表示する
 * "LoadLogToExportAnim"のボタンを押すとOutputLog内の処理が実行される
 * 
 * *****/

#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.EventSystems;

namespace HUMR
{
    [CustomEditor(typeof(OutputLogLoader))]
    public class OutputLogLoaderEditor : Editor
    {
        private string _path;
        private bool _foldout;
        private string[] _logFilePaths;
        private string[] _logFileNames;

        private static readonly Regex FilenameRegex = new Regex(@"^output_log_|\.txt$");

        private void CollectLogFilePaths()
        {
            _logFilePaths = Directory.GetFiles(_path, "*.txt")
                .OrderBy(file => File.GetLastWriteTime(Path.Combine(_path, file)))
                .Reverse()
                .ToArray();
            _logFileNames = _logFilePaths
                .Select(p => FilenameRegex.Replace(Path.GetFileName(p), ""))
                .ToArray();
        }

        public override void OnInspectorGUI()
        {

            //元のInspector部分を表示
            base.OnInspectorGUI();

            //targetを変換して対象を取得
            var targetScript = target as OutputLogLoader;

            EditorGUI.BeginChangeCheck();

            _foldout = EditorGUILayout.Foldout(_foldout, "Advanced : CustomOutputLogPath");
            if (_foldout)
            {
                EditorGUI.indentLevel++;
                _path = EditorGUILayout.TextField("OutputLogPath", _path);
                EditorGUI.indentLevel--;
            }
            else
            {
                _path = Environment.GetEnvironmentVariable("USERPROFILE");
                _path += @"\AppData\LocalLow\VRChat\VRChat";
            }
            
            if (targetScript == null) return;
            targetScript.logFilePath = _path;

            CollectLogFilePaths();

            // ラベルの作成
            const string label = "LoadOutputLog";
            // 初期値として表示する項目のインデックス番号
             var scriptIndex = targetScript.selectedIndex;
             // プルダウンメニューの作成
             var newIndex = _logFileNames.Length > 0 ? EditorGUILayout.Popup(label, scriptIndex, _logFileNames) : -1;
             targetScript.selectedIndex = newIndex;
            if (EditorGUI.EndChangeCheck())
            {
                // 操作を Undo に登録
                // インデックス番号を登録
                targetScript.selectedIndex = newIndex;
            }

            GUILayout.Space(15);

            //PrivateMethodを実行する用のボタン
            if (GUILayout.Button("LoadLogToExportAnim"))
            {
                ExecuteEvents.Execute<OutputLogLoaderInterface>(
                    target: targetScript.gameObject,
                    eventData: null,
                    functor: (receiveTarget, _) => receiveTarget.LoadLogToExportAnim());
            }
        }
    }
}
 #endif
