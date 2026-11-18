using System;
using System.IO;
using UnityEditor;

namespace UnityGameJam.Editor.Art
{
    /// <summary>
    /// 5_Art/Model 아래 파일을 **처음** 임포트할 때만 설정을 맞춘다(docs/rules/art-asset.md 6절).
    /// 그 뒤에 사람이 Inspector에서 바꾼 값은 덮어쓰지 않는다. 프리팹은 건드리지 않는다.
    /// </summary>
    internal sealed class ArtModelPostprocessor : AssetPostprocessor
    {
        private static bool IsArtPath(string path)
        {
            return path.StartsWith(ArtBinder.DefaultModelRoot + "/", StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (!IsArtPath(assetPath) || !assetImporter.importSettingsMissing) { return; }
            if (!(assetImporter is ModelImporter importer)) { return; }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.addCollider = false;
            importer.isReadable = false;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isAnimation = ArtKeyRule.TrySplitAnimation(fileName, out string artKey, out _);
            string key = isAnimation ? artKey : fileName;
            bool isCharacter = ArtKeyRule.TryGetCategory(key, out EArtCategory category) && category == EArtCategory.Character;

            if (isAnimation || isCharacter)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsArtPath(assetPath) || !assetImporter.importSettingsMissing) { return; }
            if (!(assetImporter is TextureImporter importer)) { return; }
            if (!ArtKeyRule.TrySplitTexture(Path.GetFileNameWithoutExtension(assetPath), out _, out ETextureMap map)) { return; }

            switch (map)
            {
                case ETextureMap.Normal:
                    importer.textureType = TextureImporterType.NormalMap;
                    break;
                case ETextureMap.Mask:
                    importer.sRGBTexture = false;
                    break;
            }
        }
    }
}
