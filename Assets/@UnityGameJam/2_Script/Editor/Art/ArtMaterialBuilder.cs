using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityGameJam.Editor.Art
{
    /// <summary>
    /// `<ArtKey>[_재질]_<맵>` 텍스처로 `M_<ArtKey>[_재질]` URP/Lit 머티리얼을 만들고 FBX 머티리얼을 그것으로 바꾼다.
    /// 값이 이미 같으면 아무것도 쓰지 않는다(다시 실행해도 diff가 생기지 않게).
    /// </summary>
    public static class ArtMaterialBuilder
    {
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private static readonly string[] _textureExtensions = { ".png", ".tga", ".jpg", ".jpeg" };

        /// <summary>FBX 모델의 머티리얼을 만들고 remap한다. FBX가 아니면(모델 프리팹) 건너뛴다. 무엇인가 바뀌었으면 true.</summary>
        public static bool BuildForModel(string modelPath, string artKey)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) { return false; }

            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            Dictionary<string, Dictionary<ETextureMap, string>> textures = FindTextures(folder);
            if (textures.Count == 0) { return false; }

            List<string> sourceNames = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Select(m => m.name).Distinct().ToList();
            if (sourceNames.Count == 0) { return false; }

            bool isChanged = false;
            bool needsReimport = false;
            Dictionary<AssetImporter.SourceAssetIdentifier, Object> remaps = importer.GetExternalObjectMap();
            foreach (string sourceName in sourceNames)
            {
                // 머티리얼이 하나면 `<ArtKey>_<맵>`, 여러 개면 `<ArtKey>_<재질>_<맵>`
                string baseName = sourceNames.Count == 1 ? artKey : artKey + "_" + sourceName;
                if (!textures.TryGetValue(baseName, out Dictionary<ETextureMap, string> maps) || !maps.ContainsKey(ETextureMap.BaseMap)) { continue; }

                Material material = GetOrCreateMaterial(folder + "/M_" + baseName + ".mat", ref isChanged);
                isChanged |= ApplyMaps(material, maps, false);

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
                if (!remaps.TryGetValue(id, out Object current) || current != material)
                {
                    importer.AddRemap(id, material);
                    needsReimport = true;
                }
            }

            if (isChanged) { AssetDatabase.SaveAssets(); }
            if (needsReimport) { importer.SaveAndReimport(); }
            return isChanged || needsReimport;
        }

        /// <summary>텍스처 전용(얼룩 등) 머티리얼. 알파로 모양을 자른다.</summary>
        public static Material BuildTextureOnly(string artKey, string baseMapPath)
        {
            string folder = Path.GetDirectoryName(baseMapPath).Replace('\\', '/');
            Dictionary<ETextureMap, string> maps = FindTextures(folder).TryGetValue(artKey, out var found)
                ? found
                : new Dictionary<ETextureMap, string> { { ETextureMap.BaseMap, baseMapPath } };

            bool isChanged = false;
            Material material = GetOrCreateMaterial(folder + "/M_" + artKey + ".mat", ref isChanged);
            isChanged |= ApplyMaps(material, maps, true);
            if (isChanged) { AssetDatabase.SaveAssets(); }
            return material;
        }

        public static string GetBaseMapPath(Material material)
        {
            if (material == null || !material.HasProperty("_BaseMap")) { return null; }
            Texture texture = material.GetTexture("_BaseMap");
            return texture == null ? null : AssetDatabase.GetAssetPath(texture);
        }

        private static Dictionary<string, Dictionary<ETextureMap, string>> FindTextures(string folder)
        {
            var result = new Dictionary<string, Dictionary<ETextureMap, string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) { continue; } // 하위 폴더는 다른 ArtKey 소유
                if (!_textureExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) { continue; }
                if (!ArtKeyRule.TrySplitTexture(Path.GetFileNameWithoutExtension(path), out string baseName, out ETextureMap map)) { continue; }

                if (!result.TryGetValue(baseName, out Dictionary<ETextureMap, string> maps))
                {
                    maps = new Dictionary<ETextureMap, string>();
                    result[baseName] = maps;
                }
                maps[map] = path;
            }
            return result;
        }

        private static Material GetOrCreateMaterial(string path, ref bool isChanged)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { return material; }

            material = new Material(Shader.Find(LitShaderName));
            AssetDatabase.CreateAsset(material, path);
            isChanged = true;
            return material;
        }

        private static bool ApplyMaps(Material material, Dictionary<ETextureMap, string> maps, bool useAlphaClip)
        {
            bool isChanged = false;
            Shader lit = Shader.Find(LitShaderName);
            if (material.shader != lit)
            {
                material.shader = lit;
                isChanged = true;
            }

            isChanged |= SetTexture(material, "_BaseMap", maps, ETextureMap.BaseMap);
            isChanged |= SetTexture(material, "_MainTex", maps, ETextureMap.BaseMap);
            isChanged |= SetTexture(material, "_BumpMap", maps, ETextureMap.Normal);
            isChanged |= SetTexture(material, "_MetallicGlossMap", maps, ETextureMap.Mask);
            isChanged |= SetTexture(material, "_EmissionMap", maps, ETextureMap.Emission);

            isChanged |= SetKeyword(material, "_NORMALMAP", maps.ContainsKey(ETextureMap.Normal));
            isChanged |= SetKeyword(material, "_METALLICSPECGLOSSMAP", maps.ContainsKey(ETextureMap.Mask));
            bool hasEmission = maps.ContainsKey(ETextureMap.Emission);
            isChanged |= SetKeyword(material, "_EMISSION", hasEmission);
            if (hasEmission && material.GetColor("_EmissionColor") != Color.white)
            {
                material.SetColor("_EmissionColor", Color.white);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                isChanged = true;
            }

            isChanged |= SetFloat(material, "_AlphaClip", useAlphaClip ? 1f : 0f);
            isChanged |= SetKeyword(material, "_ALPHATEST_ON", useAlphaClip);

            if (isChanged) { EditorUtility.SetDirty(material); }
            return isChanged;
        }

        private static bool SetTexture(Material material, string property, Dictionary<ETextureMap, string> maps, ETextureMap map)
        {
            if (!material.HasProperty(property)) { return false; }
            Texture texture = maps.TryGetValue(map, out string path) ? AssetDatabase.LoadAssetAtPath<Texture>(path) : null;
            if (material.GetTexture(property) == texture) { return false; }
            material.SetTexture(property, texture);
            return true;
        }

        private static bool SetKeyword(Material material, string keyword, bool isEnabled)
        {
            if (material.IsKeywordEnabled(keyword) == isEnabled) { return false; }
            if (isEnabled) { material.EnableKeyword(keyword); }
            else { material.DisableKeyword(keyword); }
            return true;
        }

        private static bool SetFloat(Material material, string property, float value)
        {
            if (!material.HasProperty(property) || Mathf.Approximately(material.GetFloat(property), value)) { return false; }
            material.SetFloat(property, value);
            return true;
        }
    }
}
