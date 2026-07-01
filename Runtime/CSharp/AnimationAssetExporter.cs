using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace HUMR
{
    // Editor animators prevent world build
#if UNITY_EDITOR
    internal static class AnimationAssetExporter
    {
        public static void SaveGenericAnimationAsset(AnimationClip clip, string humrPath, string displayName, string baseName, int segmentIndex, AnimationControllerBuilder controllerBuilder)
        {
            var animFolderPath = $"{humrPath}/GenericAnimations/{displayName}";
            PathUtils.CreateDirectoryIfNotExist(animFolderPath);
        
            var animAssetPath = $"{animFolderPath}/{baseName}_{segmentIndex}.anim";
        
            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                RecorderUtils.HumrWarning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
                controllerBuilder.CleanControllerStates(false);
            }
        
            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        public static void ExportFBX(Animator animator, AnimatorController controller, string humrPath, string displayName, string fileName, GameObject targetObject)
        {
            animator.runtimeAnimatorController = controller;
        
            var exportFolderPath = $"{humrPath}/FBXs/{PathUtils.SanitizeFileName(displayName)}";
            PathUtils.CreateDirectoryIfNotExist(exportFolderPath);
        
            var finalPath = $"{exportFolderPath}/{fileName}";
            ModelExporter.ExportObject(finalPath, targetObject);
        }
    }
#endif
}