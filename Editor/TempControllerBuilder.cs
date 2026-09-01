using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    internal class TempControllerBuilder
    {
        public AnimatorController Controller { get; private set; }
        private string _controllerPath;

        public void Setup(string humrPath)
        {
            _controllerPath = $"{humrPath}/TmpAniCon.controller";
            PathUtils.CreateDirectoryIfNotExist($"{humrPath}");
            Controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
        }

        public void AddClipToController(AnimationClip clip)
        {
            if (Controller == null || Controller.layers.Length == 0) return;
            Controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }

        public void DeleteControllerAsset()
        {
            AssetDatabase.DeleteAsset(_controllerPath);
        }
    }
}