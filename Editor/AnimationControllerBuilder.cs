using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    internal class AnimationControllerBuilder
    {
        public AnimatorController Controller { get; private set; }

        public void Setup(string humrPath)
        {
            var controllerFolderPath = $"{humrPath}/AnimationController";
            var controllerPath = $"{controllerFolderPath}/TmpAniCon.controller";

            if (Controller != null)
            {
                var clearAllStates = AssetDatabase.GetAssetPath(Controller) == controllerPath;
                CleanControllerStates(clearAllStates);
                return;
            }

            PathUtils.CreateDirectoryIfNotExist(controllerFolderPath);
            Controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        public void CleanControllerStates(bool clearAll)
        {
            if (Controller == null) return;

            foreach (var layer in Controller.layers)
            {
                var states = layer.stateMachine.states;
                for (var i = states.Length - 1; i >= 0; i--)
                {
                    if (!clearAll && states[i].state.motion != null) continue;
                    layer.stateMachine.RemoveState(states[i].state);
                }
            }
        }

        public void AddClipToController(AnimationClip clip)
        {
            if (Controller == null || Controller.layers.Length == 0) return;
            Controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }

        public static void SaveGenericAnimationAsset(AnimationClip clip, string animAssetPath)
        {
            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                HumrLogger.Warning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
            }

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            var createdAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GetAssetPath(clip));
            Selection.activeObject = createdAsset;
            EditorGUIUtility.PingObject(createdAsset);
        }
    }
}