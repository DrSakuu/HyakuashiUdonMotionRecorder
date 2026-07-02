// Editor animators prevent world build
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace HUMR
{
    internal static class AnimationAssetExporter
    {
        public static void SaveGenericAnimationAsset(AnimationClip clip, string humrPath, string targetName, string baseName, int takeIndex, AnimationControllerBuilder controllerBuilder)
        {
            var animFolderPath = $"{humrPath}/GenericAnimations/{PathUtils.SanitizeFileName(targetName)}";
            PathUtils.CreateDirectoryIfNotExist(animFolderPath);
        
            var animAssetPath = $"{animFolderPath}/{baseName}_Take{takeIndex+1}.anim";
        
            if (File.Exists(animAssetPath))
            {
                AssetDatabase.DeleteAsset(animAssetPath);
                HumrLogger.Warning($"Overwrite target collision detected: Existing asset deleted at {animAssetPath}");
                controllerBuilder.CleanControllerStates(false);
            }
        
            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animAssetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        public static void ExportFBX(Animator animator, AnimatorController controller, string humrPath, string targetName, string fileName, GameObject targetObject)
        {
            animator.runtimeAnimatorController = controller;
        
            var exportFolderPath = $"{humrPath}/FBXs/{PathUtils.SanitizeFileName(targetName)}";
            PathUtils.CreateDirectoryIfNotExist(exportFolderPath);
        
            var finalPath = $"{exportFolderPath}/{fileName}";
            ModelExporter.ExportObject(finalPath, targetObject);
        }
    }
}
#endif