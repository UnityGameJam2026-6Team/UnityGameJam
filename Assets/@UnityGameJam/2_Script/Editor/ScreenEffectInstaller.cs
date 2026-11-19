using System;
using UnityEditor;
using UnityEngine;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// 거리 안개(FX_DistanceFog)와 붉은 화면(FX_RedTint)을 PC_Renderer에 Full Screen Pass로 붙인다.
    /// 렌더러·머티리얼 YAML을 손으로 고치지 않기 위해 에디터에서 만든다.
    /// 에디터 어셈블리가 URP를 참조하지 않으므로(asmdef는 공동 설정) URP 타입은 이름으로 찾는다.
    /// 여러 번 실행해도 같은 이름의 머티리얼·기능이 있으면 새로 만들지 않는다.
    /// </summary>
    internal static class ScreenEffectInstaller
    {
        private const string MenuPath = "Tools/UnityGameJam/Screen Effect/Install Distance Fog And Red Tint";
        internal const string RendererPath = "Assets/@UnityGameJam/7_Setting/PC_Renderer.asset";
        private const string MaterialFolder = "Assets/@UnityGameJam/5_Art/Material";

        internal const string FeatureTypeName = "UnityEngine.Rendering.Universal.FullScreenPassRendererFeature, Unity.RenderPipelines.Universal.Runtime";
        internal const string PassInputTypeName = "UnityEngine.Rendering.Universal.ScriptableRenderPassInput, Unity.RenderPipelines.Universal.Runtime";

        [MenuItem(MenuPath)]
        private static void Install()
        {
            var featureType = Type.GetType(FeatureTypeName);
            var passInputType = Type.GetType(PassInputTypeName);
            if (featureType == null || passInputType == null)
            {
                Debug.LogError("[ScreenEffect] URP의 FullScreenPassRendererFeature를 찾을 수 없습니다. URP 패키지 버전을 확인하세요.");
                return;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[ScreenEffect] 렌더러 에셋을 찾을 수 없습니다: {RendererPath}");
                return;
            }

            var fogMaterial = GetOrCreateMaterial("UnityGameJam/Screen/Distance Fog", "M_FX_DistanceFog");
            var tintMaterial = GetOrCreateMaterial("UnityGameJam/Screen/Red Tint", "M_FX_RedTint");
            if (fogMaterial == null || tintMaterial == null) { return; }

            // 안개는 깊이가 필요하고, 깊이를 쓰지 않는 반투명 오브젝트 위에 덮이지 않도록 반투명보다 먼저 그린다.
            AddFeature(rendererData, featureType, "FX_DistanceFog", fogMaterial,
                ParseEnum(featureType.GetNestedType("InjectionPoint"), "BeforeRenderingTransparents"),
                ParseEnum(passInputType, "Depth"));
            // 붉은 효과는 톤매핑 등 후처리가 끝난 최종 화면에 입힌다.
            AddFeature(rendererData, featureType, "FX_RedTint", tintMaterial,
                ParseEnum(featureType.GetNestedType("InjectionPoint"), "AfterRenderingPostProcessing"),
                ParseEnum(passInputType, "None"));

            AssetDatabase.SaveAssets();
            Selection.activeObject = fogMaterial;
            Debug.Log("[ScreenEffect] 설치 완료. M_FX_DistanceFog / M_FX_RedTint 머티리얼에서 값을 조정하고, PC_Renderer에서 기능을 켜고 끌 수 있습니다.");
        }

        internal static Material GetOrCreateMaterial(string shaderName, string materialName)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { return material; }

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[ScreenEffect] '{shaderName}' 셰이더를 찾을 수 없습니다. 셰이더 컴파일 에러가 없는지 Console을 확인하세요.");
                return null;
            }

            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static void AddFeature(ScriptableObject rendererData, Type featureType, string featureName, Material material, int injectionPoint, int requirements)
        {
            var rendererObject = new SerializedObject(rendererData);
            var features = rendererObject.FindProperty("m_RendererFeatures");
            var featureMap = rendererObject.FindProperty("m_RendererFeatureMap");

            for (int i = 0; i < features.arraySize; i++)
            {
                var existing = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null && existing.name == featureName)
                {
                    Debug.Log($"[ScreenEffect] '{featureName}'은 이미 PC_Renderer에 있어서 건너뜁니다.");
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance(featureType);
            feature.name = featureName;

            var featureObject = new SerializedObject(feature);
            featureObject.FindProperty("injectionPoint").intValue = injectionPoint;
            featureObject.FindProperty("requirements").intValue = requirements;
            featureObject.FindProperty("fetchColorBuffer").boolValue = true;
            featureObject.FindProperty("passMaterial").objectReferenceValue = material;
            featureObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            int index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            featureMap.arraySize = index + 1;
            featureMap.GetArrayElementAtIndex(index).longValue = localId;
            rendererObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
        }

        internal static int ParseEnum(Type enumType, string memberName)
        {
            return Convert.ToInt32(Enum.Parse(enumType, memberName));
        }
    }
}
