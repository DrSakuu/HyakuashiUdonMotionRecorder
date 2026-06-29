/*******
 * OutputLogLoader.cs
 * 
 * メインの処理を行う。ログ出力時と同一のアバターをHierarchy上に置き、これをアタッチして使用することを想定している
 * PackageManagerからFBXExporterをインストールしておく必要あり
 * 
 * フォルダを構成して、OutputLog_xx_xx_xxからアニメーションを作成
 * そのアニメーションをアバターのアニメーターに入れてFBXとして出力
 * FBXをHumanoidにすることでHumanoidAnimationを得られるようにしている
 * 
 * *****/

using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using UnityEngine.EventSystems;
using System.Globalization;

namespace HUMR
{
#if UNITY_EDITOR
    public interface OutputLogLoaderInterface : IEventSystemHandler
    {
        void LoadLogToExportAnim();
    }

    [RequireComponent(typeof(Animator))]
    public class OutputLogLoader : MonoBehaviour, OutputLogLoaderInterface
    {
        [Tooltip("GenericAnimationを出力する場合はチェックを入れてください(チェックがないと複数のAnimationを出力できません)")]
        public bool exportGenericAnimation = true;
        [Tooltip("モーションを出力したいユーザーの名前を書いてください")]
        public string displayName = "";
        
        [HideInInspector]
        public string logFilePath = "";
        [HideInInspector]
        public int selectedIndex;
        
        private Animator _animator;
        private UnityEditor.Animations.AnimatorController _controller;
        
        private const int TimeStampLength = 19; //timestamp example/*2021.01.03 20:57:35*/
        private const string HumrPath = "Assets/HUMR";
        
        private string[] _files;
        private string _strKeyWord = "-  [HUMR] ";

        public void LoadLogToExportAnim()
        {
            ControllerSetUp(HumrPath);
            
            var files = GetLogFiles(logFilePath);
            if (!ValidateInputs(files)) return;

            var logLines = File.ReadAllLines(files[selectedIndex]);

            var segments = HumrUtilities.PartitionLogLinesIntoSegments(logLines, displayName);
            if (segments.Count == 0)
            {
                HumrUtilities.HumrWarning($"Motion Data with [{displayName}] does not exist (Did you enter the correct DisplayName? or select the correct log ?)");
                return;
            }
            
            var nTargetCounter = 0;
            var newTargetLines = new List<int>();//ファイルの中での新しく始まった対象の行を格納する
            newTargetLines.Add(0);
            var newLogLines = new List<int>();//抽出したログの中で新しく始まった行を格納する
            newLogLines.Add(0);
            var beforeTime = 0f;
            for (var j = 0; j < logLines.Length; j++)
            {
                //対象のログの行を抽出
                if (!logLines[j].Contains(_strKeyWord + displayName)) continue;
                if (logLines[j].Length > TimeStampLength + (_strKeyWord + displayName).Length)
                {
                    //記録終わりを検知
                    var strTmpOLL = logLines[j].Substring(TimeStampLength + 13 + (_strKeyWord + displayName).Length);
                    for (var k = 0; k < strTmpOLL.Length; k++)
                    {
                        if (strTmpOLL[k] != ';') continue;
                        var currentTime = float.Parse(strTmpOLL.Substring(0, k), CultureInfo.InvariantCulture);
                        if (currentTime < beforeTime)
                        {
                            newLogLines.Add(nTargetCounter);
                            newTargetLines.Add(j);
                        }
                        beforeTime = currentTime;
                        break;
                    }
                    nTargetCounter++;//目的の行が何行あるか。
                }
                else
                {
                    HumrUtilities.HumrWarning("Length is not correct");
                }
            }
            newLogLines.Add(nTargetCounter);
            newTargetLines.Add(logLines.Length);
            
            // Keyframeの生成
            if (nTargetCounter == 0)
            {
                HumrUtilities.HumrWarning("Motion Data with ["+ displayName + "] does not exist (Did you enter correct DisplayName ? or select correct log ?)");
                return;
            }

            for (var i =0; i<newLogLines.Count-1;i++)
            {
                var nLineNum = newLogLines[i + 1] - newLogLines[i];
                var keyframes = new Keyframe[4 * ((int)HumanBodyBones.LastBone + 1/*time + hip position*/) - 1/*time*/][];//[要素数]
                for (var j = 0; j < keyframes.Length; j++)
                {
                    keyframes[j] = new Keyframe[nLineNum];//[行数]
                }

                //Keyframeにログの値を入れていく
                {
                    var strDisplayNameOutputLogLines = new string[nLineNum];//目的の行の配列
                    var nTargetLineCounter = 0;
                    beforeTime = 0;
                    for (var j = newTargetLines[i]; j < newTargetLines[i+1]; j++)
                    {
                        //対象のログの行を抽出
                        if (!logLines[j].Contains(_strKeyWord + displayName)) continue;
                        if (logLines[j].Length > TimeStampLength + (_strKeyWord + displayName).Length)
                        {
                            strDisplayNameOutputLogLines[nTargetLineCounter] = logLines[j].Substring(TimeStampLength + 13 + (_strKeyWord + displayName).Length);//時間,position,rotation,rotation,…
                            for (var k = 0; k < strDisplayNameOutputLogLines[nTargetLineCounter].Length; k++)
                            {
                                if (strDisplayNameOutputLogLines[nTargetLineCounter][k] != ';') continue;
                                var currentTime = float.Parse(strDisplayNameOutputLogLines[nTargetLineCounter].Substring(0, k), CultureInfo.InvariantCulture);
                                if (currentTime < beforeTime)
                                {
                                    HumrUtilities.HumrAssertion("New record line is contained");
                                }
                                beforeTime = currentTime;
                                break;
                            }
                        }
                        else
                        {
                            HumrUtilities.HumrWarning("Log Length is not correct");
                        }
                        //Debug.Log(DisplayNameOutputLogLines[nTargetLineCounter]);
                        var frames = segments[0].Frames;
                        var frame = frames[nTargetLineCounter];

                        var splitLogFile = strDisplayNameOutputLogLines[nTargetLineCounter].Split(',', ';');
                        var keyTime = frame.RecordTime;
                        var rootScale = _animator.transform.localScale;
                        var armatureScale = _animator.GetBoneTransform(0).parent.localScale;
                        var hippos = frame.HipPosition;
                        transform.rotation = Quaternion.identity;//Avatarがrotation(0,0,0)でない可能性があるため
                        hippos = Quaternion.Inverse(_animator.GetBoneTransform(0).parent.localRotation) * hippos;//armatureがrotation(0,0,0)でない可能性があるため
                        hippos = new Vector3(hippos.x / rootScale.x/ armatureScale.x, hippos.y / rootScale.y/ armatureScale.y, hippos.z / rootScale.z/ armatureScale.z); //いる
                        keyframes[0][nTargetLineCounter] = new Keyframe(keyTime, hippos.x);
                        keyframes[1][nTargetLineCounter] = new Keyframe(keyTime, hippos.y);
                        keyframes[2][nTargetLineCounter] = new Keyframe(keyTime, hippos.z);
                        var boneWorldRotation = new Quaternion[(int)HumanBodyBones.LastBone];
                        for (var k = 0; k < (int)HumanBodyBones.LastBone; k++)
                        {
                            boneWorldRotation[k] = frame.BoneRotations[k];
                        }
                        for (var k = 0; k < (int)HumanBodyBones.LastBone; k++)
                        {

                            if (_animator.GetBoneTransform((HumanBodyBones)k) == null)
                            {
                                continue;
                            }
                            _animator.GetBoneTransform((HumanBodyBones)k).rotation = boneWorldRotation[k];
                        }

                        for (var k = 0; k < (int)HumanBodyBones.LastBone; k++)
                        {
                            if (_animator.GetBoneTransform((HumanBodyBones)k) == null)
                            {
                                continue;
                            }
                            var localRotation = _animator.GetBoneTransform((HumanBodyBones)k).localRotation;
                            keyframes[k * 4 + 3][nTargetLineCounter] = new Keyframe(keyTime, localRotation.x);
                            keyframes[k * 4 + 4][nTargetLineCounter] = new Keyframe(keyTime, localRotation.y);
                            keyframes[k * 4 + 5][nTargetLineCounter] = new Keyframe(keyTime, localRotation.z);
                            keyframes[k * 4 + 6][nTargetLineCounter] = new Keyframe(keyTime, localRotation.w);
                        }
                        nTargetLineCounter++;
                    }
                }

                //AnimationClipにAnimationCurveを設定
                var clip = new AnimationClip();
                {
                    // AnimationCurveの生成
                    var animCurves = new AnimationCurve[keyframes.Length];

                    for (var l = 0; l < animCurves.Length; l++)//[行数-1]
                    {
                        animCurves[l] = new AnimationCurve(keyframes[l]);
                    }
                    // AnimationCurveの追加
                    clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform(0)), typeof(Transform), "localPosition.x", animCurves[0]);
                    clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform(0)), typeof(Transform), "localPosition.y", animCurves[1]);
                    clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform(0)), typeof(Transform), "localPosition.z", animCurves[2]);
                    for (var m = 0; m < (animCurves.Length - 3) / 4; m++)//[骨数]
                    {
                        if (_animator.GetBoneTransform((HumanBodyBones)m) == null)
                        {
                            continue;
                        }
                        clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform((HumanBodyBones)m)),
                            typeof(Transform), "localRotation.x", animCurves[m * 4 + 3]);
                        clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform((HumanBodyBones)m)),
                            typeof(Transform), "localRotation.y", animCurves[m * 4 + 4]);
                        clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform((HumanBodyBones)m)),
                            typeof(Transform), "localRotation.z", animCurves[m * 4 + 5]);
                        clip.SetCurve(HumrUtilities.GetHierarchyPath(_animator.GetBoneTransform((HumanBodyBones)m)),
                            typeof(Transform), "localRotation.w", animCurves[m * 4 + 6]);
                    }
                    clip.EnsureQuaternionContinuity();//これをしないとQuaternion補間してくれない
                }

                //GenericAnimation出力
                {
                    const string animFolderPath = HumrPath + @"/GenericAnimations";
                    CreateDirectoryIfNotExist(animFolderPath);
                    var displayNameFolderPath = animFolderPath + "/" + displayName;
                    CreateDirectoryIfNotExist(displayNameFolderPath);

                    var animationName = files[selectedIndex].Substring(files[selectedIndex].Length - 23).Remove(19)+"_"+i.ToString();
                    var animPath = displayNameFolderPath + "/" + animationName + ".anim";
                    Debug.Log(animPath);

                    if (exportGenericAnimation)
                    {
                        if (File.Exists(animPath))
                        {
                            AssetDatabase.DeleteAsset(animPath);
                            HumrUtilities.HumrWarning("Same Name Generic Animation is existing. Overwritten!!");
                            foreach (var layer in _controller.layers)//アニメーションを消したことにより空のアニメーションステートが出来てたら削除
                            {
                                foreach (var state in layer.stateMachine.states)
                                {
                                    if (state.state.motion == null)
                                    {
                                        layer.stateMachine.RemoveState(state.state);
                                    }
                                }
                            }
                        }
                        AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animPath));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }

                //アニメーションをアバターのアニメーターに入れる
                {
                    _controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
                }
            }
            //FBXとして出力
            {
                _animator.runtimeAnimatorController = _controller;
                const string exportFolderPath = HumrPath + @"/FBXs";
                CreateDirectoryIfNotExist(exportFolderPath);
                var displayNameFBXFolderPath = exportFolderPath + "/" + ValidName(displayName);
                CreateDirectoryIfNotExist(displayNameFBXFolderPath);
                UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(displayNameFBXFolderPath + "/" + files[selectedIndex].Substring(files[selectedIndex].Length - 23).Remove(19), this.gameObject);
            }
        }

        private static string[] GetLogFiles(string logFilePathString)
        {
            if (string.IsNullOrEmpty(logFilePathString) || !Directory.Exists(logFilePathString)) return null;
            return Directory.GetFiles(logFilePathString, "*.txt");
        }

        //ファイル名やパスに使えない文字を‗に置換
        private static string ValidName(string str)
        {
            var strValid = str;
            var chInvalid = Path.GetInvalidFileNameChars();

            foreach (var c in chInvalid)
            {
                strValid = strValid.Replace(c, '_');
            }
            return strValid;
        }

        private void ControllerSetUp(string humrPath)
        {
            var tmpAniConPath = humrPath + @"/AnimationController";
            if (_controller == null)
            {
                CreateDirectoryIfNotExist(tmpAniConPath);
                _controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(tmpAniConPath + "/TmpAniCon.controller");
            }
            else if (AssetDatabase.GetAssetPath(_controller) == tmpAniConPath + "/TmpAniCon.controller")
            {
                foreach (var layer in _controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        layer.stateMachine.RemoveState(state.state);
                    }
                }
            }
            else
            {
                foreach (var layer in _controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.motion == null)
                        {
                            layer.stateMachine.RemoveState(state.state);
                        }
                    }
                }
            }
        }

        private static void CreateDirectoryIfNotExist(string path)
        {
            //存在するかどうか判定しなくても良いみたいだが気持ち悪いので
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
        }

        private bool ValidateInputs(string[] files)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                HumrUtilities.HumrWarning("DisplayName is null or empty.");
                return false;
            }

            if (files == null || files.Length == 0 || selectedIndex < 0 || selectedIndex >= files.Length)
            {
                HumrUtilities.HumrWarning("Target log file could not be found or index selection is out of range.");
                return false;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            return _animator != null;
        }
        
        private void SetupAnimatorController()
        {
            var controllerFolderPath = $"{HumrPath}/AnimationController";
            var controllerPath = $"{controllerFolderPath}/TmpAniCon.controller";

            if (_controller == null)
            {
                CreateDirectoryIfNotExist(controllerFolderPath);
                _controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }
            else
            {
                var clearAllStates = AssetDatabase.GetAssetPath(_controller) == controllerPath;
                CleanControllerStates(clearAllStates);
            }
        }

        private void CleanControllerStates(bool clearAll)
        {
            foreach (var layer in _controller.layers)
            {
                var states = layer.stateMachine.states;
                for (var i = states.Length - 1; i >= 0; i--)
                {
                    if (clearAll || states[i].state.motion == null)
                    {
                        layer.stateMachine.RemoveState(states[i].state);
                    }
                }
            }
        }

        private void SaveGenericAnimationAsset(AnimationClip clip, string baseName, int segmentIndex)
        {
            if (!exportGenericAnimation) return;

            string animFolderPath = $"{HumrPath}/GenericAnimations/{displayName}";
            CreateDirectoryIfNotExist(animFolderPath);

            string animAssetPath = $"{animFolderPath}/{baseName}_{segmentIndex}.anim";

            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                HumrUtilities.HumrWarning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
                CleanControllerStates(clearAll: false);
            }

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void AddClipToController(AnimationClip clip)
        {
            _controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }

        private void ExportFBX(string fileName)
        {
            _animator.runtimeAnimatorController = _controller;

            var exportFolderPath = $"{HumrPath}/FBXs/{HumrUtilities.SanitizeFileName(displayName)}";
            CreateDirectoryIfNotExist(exportFolderPath);

            var finalPath = $"{exportFolderPath}/{fileName}";
            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(finalPath, gameObject);
        }
    }
#endif
}
