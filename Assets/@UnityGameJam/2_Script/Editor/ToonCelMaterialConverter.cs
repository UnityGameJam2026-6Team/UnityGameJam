using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// 선택한 머티리얼 또는 GameObject(씬/프리팹)의 머티리얼을 "UnityGameJam/Toon Cel" 셰이더로 변환한다.
    /// URP Lit/Simple Lit/Unlit, Built-in Standard 머티리얼의 텍스처·색상·노멀·이미션·알파 클립 설정을 유지한다.
    /// </summary>
    internal static class ToonCelMaterialConverter
    {
        internal const string ToonShaderName = "UnityGameJam/Toon Cel";
        private const string MenuPath = "Tools/UnityGameJam/Convert Selection To Toon Cel";
        private const string AssetMenuPath = "Assets/UnityGameJam/Convert To Toon Cel";

        [MenuItem(MenuPath)]
        [MenuItem(AssetMenuPath)]
        private static void ConvertSelection()
        {
            var shader = Shader.Find(ToonShaderName);
            if (shader == null)
            {
                Debug.LogError($"[ToonCel] '{ToonShaderName}' 셰이더를 찾을 수 없습니다.");
                return;
            }

            var materials = CollectMaterials(Selection.objects);
            int converted = 0;
            foreach (var material in materials)
            {
                if (!AssetDatabase.IsOpenForEdit(material) || IsReadOnlyImportedMaterial(material))
                {
                    Debug.LogWarning($"[ToonCel] '{material.name}'은 모델에 포함된 읽기 전용 머티리얼입니다. 모델 Import Settings > Materials에서 Extract 후 다시 실행하세요.", material);
                    continue;
                }
                if (Convert(material, shader))
                    converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ToonCel] {converted}개 머티리얼을 변환했습니다. (대상 {materials.Count}개)");
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateConvertSelection() => Selection.objects.Length > 0;

        private static HashSet<Material> CollectMaterials(Object[] selection)
        {
            var result = new HashSet<Material>();
            foreach (var obj in selection)
            {
                if (obj is Material material)
                {
                    result.Add(material);
                }
                else if (obj is GameObject go)
                {
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var m in renderer.sharedMaterials)
                        {
                            if (m != null)
                                result.Add(m);
                        }
                    }
                }
            }
            return result;
        }

        private static bool IsReadOnlyImportedMaterial(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material);
            return !string.IsNullOrEmpty(path) && !path.EndsWith(".mat");
        }

        /// <summary>머티리얼을 툰 셰이더로 변환한다. 이미 툰 셰이더면 false.</summary>
        internal static bool Convert(Material material, Shader toonShader)
        {
            if (material.shader == toonShader)
                return false;

            // 셰이더 교체 전에 기존 값을 읽어둔다.
            Texture baseMap = GetTexture(material, "_BaseMap", "_MainTex");
            Vector2 scale = GetTextureScale(material, "_BaseMap", "_MainTex");
            Vector2 offset = GetTextureOffset(material, "_BaseMap", "_MainTex");
            Color baseColor = GetColor(material, Color.white, "_BaseColor", "_Color");
            Texture bumpMap = GetTexture(material, "_BumpMap");
            float bumpScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
            Texture emissionMap = GetTexture(material, "_EmissionMap");
            Color emissionColor = GetColor(material, Color.black, "_EmissionColor");
            bool emissionOn = material.IsKeywordEnabled("_EMISSION") && emissionColor.maxColorComponent > 0f;
            bool alphaClip = (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                             || material.IsKeywordEnabled("_ALPHATEST_ON");
            float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
            float cull = material.HasProperty("_Cull") ? material.GetFloat("_Cull") : 2f;

            Undo.RecordObject(material, "Convert To Toon Cel");
            material.shader = toonShader;
            material.shaderKeywords = new string[0];

            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Cull", cull);

            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", cutoff);
            SetKeyword(material, "_ALPHATEST_ON", alphaClip);
            material.renderQueue = alphaClip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;

            bool hasNormal = bumpMap != null;
            material.SetTexture("_BumpMap", bumpMap);
            material.SetFloat("_BumpScale", bumpScale);
            material.SetFloat("_UseNormalMap", hasNormal ? 1f : 0f);
            SetKeyword(material, "_NORMALMAP", hasNormal);

            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", emissionOn ? emissionColor : Color.black);
            material.SetFloat("_UseEmission", emissionOn ? 1f : 0f);
            SetKeyword(material, "_EMISSION", emissionOn);

            EditorUtility.SetDirty(material);
            return true;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        private static Texture GetTexture(Material material, params string[] names)
        {
            foreach (var n in names)
            {
                if (material.HasProperty(n) && material.GetTexture(n) != null)
                    return material.GetTexture(n);
            }
            return null;
        }

        private static Vector2 GetTextureScale(Material material, params string[] names)
        {
            foreach (var n in names)
            {
                if (material.HasProperty(n) && material.GetTexture(n) != null)
                    return material.GetTextureScale(n);
            }
            return Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, params string[] names)
        {
            foreach (var n in names)
            {
                if (material.HasProperty(n) && material.GetTexture(n) != null)
                    return material.GetTextureOffset(n);
            }
            return Vector2.zero;
        }

        private static Color GetColor(Material material, Color fallback, params string[] names)
        {
            foreach (var n in names)
            {
                if (material.HasProperty(n))
                    return material.GetColor(n);
            }
            return fallback;
        }
    }
}
