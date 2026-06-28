
/*******
 * HUMRImportFBXSettings.cs
 * 
 * Editor拡張。Editorフォルダの下に配置すること
 * 
 * FBXを読み込んだ際に自動で実行される
 * RigのAnimationTypeをHumanoidにして
 * AnimationのRotationとPosition(Y)をBake into PoseにしUponをOriginalとしている
 * 
 * 上記の設定に関して不具合等が発生したらこのスクリプトを改造するか削除して対応のこと
 * 
 * *****/
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
namespace HUMR
{
    public class HUMRImportFBXSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("HUMR/FBXs")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
        }

        private void OnPreprocessAnimation()
        {
            //このパス以下のAnimationは影響を受ける
            if (!assetPath.Contains("HUMR/FBXs")) return;
            var importer = (ModelImporter)assetImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            var importerClips = //初回ロード時
                importer.clipAnimations.Length == 0 ? importer.defaultClipAnimations : importer.clipAnimations;
            foreach (var clipAnimation in importerClips)
            {
                //回転とy軸はポーズに焼きこみ、xz座標は任意にしたほうが汎用性が高そう
                clipAnimation.lockRootRotation = true;
                clipAnimation.keepOriginalOrientation = true;
                clipAnimation.lockRootHeightY = true;
                clipAnimation.keepOriginalPositionY = true;
                //clipAnimation.lockRootPositionXZ = true;
                //clipAnimation.keepOriginalPositionXZ = true;
                    
                if (clipAnimation.name == "")
                {
                    clipAnimation.name = "HUMRAnimation";
                }
            }
            importer.clipAnimations = importerClips;
        }

    }
}
 #endif
