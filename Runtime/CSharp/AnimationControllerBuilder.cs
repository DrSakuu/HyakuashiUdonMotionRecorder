// Editor animators prevent world build
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HUMR
{
    internal class AnimationControllerBuilder
    {
        private AnimatorController _controller;
        public AnimatorController Controller => _controller;

        public void Setup(string humrPath)
        {
            var controllerFolderPath = $"{humrPath}/AnimationController";
            var controllerPath = $"{controllerFolderPath}/TmpAniCon.controller";
        
            if (_controller != null)
            {
                var clearAllStates = AssetDatabase.GetAssetPath(_controller) == controllerPath;
                CleanControllerStates(clearAllStates);
                return;
            }
        
            PathUtils.CreateDirectoryIfNotExist(controllerFolderPath);
            _controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        
        public void CleanControllerStates(bool clearAll)
        {
            if (_controller == null) return;

            foreach (var layer in _controller.layers)
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
            if (_controller == null || _controller.layers.Length == 0) return;
            _controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
        }
    }
}
#endif