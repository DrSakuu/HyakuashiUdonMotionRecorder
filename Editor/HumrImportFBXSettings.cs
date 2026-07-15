using UnityEditor;

namespace Humr.Editor
{
    public class HumrImportFBXSettings : AssetPostprocessor
    {
        private void OnPreprocessAnimation()
        {
            if (!assetPath.Contains("HUMR/FBXs")) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            var importerClips =
                importer.clipAnimations.Length == 0 ? importer.defaultClipAnimations : importer.clipAnimations;
            foreach (var clipAnimation in importerClips)
            {
                clipAnimation.lockRootRotation = true;
                clipAnimation.keepOriginalOrientation = true;
                clipAnimation.lockRootHeightY = true;
                clipAnimation.keepOriginalPositionY = true;
                //clipAnimation.lockRootPositionXZ = true;
                //clipAnimation.keepOriginalPositionXZ = true;

                if (clipAnimation.name == "") clipAnimation.name = "HUMRAnimation";
            }

            importer.clipAnimations = importerClips;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("HUMR/FBXs")) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
        }
    }
}