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
                _path = System.Environment.GetEnvironmentVariable("USERPROFILE");
                _path += @"\AppData\LocalLow\VRChat\VRChat";
            }

            if (targetScript == null) return;
            targetScript.logFilePath = _path;

            var files = Directory.GetFiles(_path, "*.txt");
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Substring(files[i].Length - 23).Remove(19);
            }


            // ラベルの作成
            const string label = "LoadOutputLog";
            // 初期値として表示する項目のインデックス番号
            var selectedIndex = targetScript.index;
            // プルダウンメニューの作成
            var index = files.Length > 0
                ? EditorGUILayout.Popup(label, selectedIndex, files)
                : -1;

            if (EditorGUI.EndChangeCheck())
            {
                // 操作を Undo に登録
                // インデックス番号を登録
                targetScript.index = index;
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
