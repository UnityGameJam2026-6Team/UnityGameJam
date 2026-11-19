using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// 몽환적이고 위험한 분위기(보라·분홍 톤, 빛줄기, 떠다니는 빛, 가장자리 흐림)를 한 번에 설치한다.
    ///  - PC_Renderer에 FX_DreamHaze(Full Screen Pass)를 붙인다.
    ///  - 후처리 볼륨 프로필 VP_DreamDanger를 만들고 SYS_GlobalVolume 아래 FX_DreamAtmosphere 볼륨으로 건다.
    ///  - SYS_MainCamera의 후처리를 켜고, 주광·주변광과 기존 안개·틴트 색을 보라 톤으로 맞춘다.
    /// 다시 실행하면 이 스크립트의 기본값으로 덮어쓴다. 세부 조정은 머티리얼·프로필 인스펙터에서 한다.
    /// 에디터 어셈블리가 URP를 참조하지 않으므로(asmdef는 공동 설정) URP 타입은 이름으로 찾는다.
    /// </summary>
    internal static class DreamAtmosphereInstaller
    {
        private const string MenuPath = "Tools/UnityGameJam/Screen Effect/Install Dream Atmosphere";
        private const string MaterialFolder = "Assets/@UnityGameJam/5_Art/Material";
        private const string ProfileFolder = "Assets/@UnityGameJam/5_Art/PostProcess";
        private const string ProfilePath = ProfileFolder + "/VP_DreamDanger.asset";
        private const string GlobalVolumePrefabPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_GlobalVolume.prefab";
        private const string CameraPrefabPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_MainCamera.prefab";
        private const string LightPrefabPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_DirectionalLight.prefab";
        private const string VolumeObjectName = "FX_DreamAtmosphere";
        private const string UrpAssembly = ", Unity.RenderPipelines.Universal.Runtime";
        private const string CoreAssembly = ", Unity.RenderPipelines.Core.Runtime";

        [MenuItem(MenuPath)]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[DreamAtmosphere] 플레이를 멈춘 뒤 다시 실행하세요.");
                return;
            }

            InstallHazePass();
            RetuneScreenMaterials();
            ScriptableObject profile = BuildProfile();
            if (profile != null) { AttachVolume(profile); }
            EnablePostProcessing();
            TintSunlight();
            TintAmbient();

            AssetDatabase.SaveAssets();
            Debug.Log("[DreamAtmosphere] 설치 완료. 빛줄기·알갱이·흐림은 M_FX_DreamHaze, 색감·번짐은 VP_DreamDanger에서 조정합니다.");
        }

        private static void InstallHazePass()
        {
            Type featureType = Type.GetType(ScreenEffectInstaller.FeatureTypeName);
            Type passInputType = Type.GetType(ScreenEffectInstaller.PassInputTypeName);
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ScreenEffectInstaller.RendererPath);
            if (featureType == null || passInputType == null || rendererData == null)
            {
                Debug.LogError("[DreamAtmosphere] PC_Renderer 또는 URP Full Screen Pass를 찾지 못했습니다.");
                return;
            }

            Material material = ScreenEffectInstaller.GetOrCreateMaterial("UnityGameJam/Screen/Dream Haze", "M_FX_DreamHaze");
            if (material == null) { return; }

            // 다시 실행하면 셰이더에 적힌 기본값으로 되돌린다.
            var defaults = new Material(material.shader);
            material.CopyPropertiesFromMaterial(defaults);
            UnityEngine.Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(material);

            // 후처리 직전에 그려서 빛줄기와 알갱이가 Bloom으로 번지고 톤매핑을 함께 받게 한다.
            ScreenEffectInstaller.AddFeature(rendererData, featureType, "FX_DreamHaze", material,
                ScreenEffectInstaller.ParseEnum(featureType.GetNestedType("InjectionPoint"), "BeforeRenderingPostProcessing"),
                ScreenEffectInstaller.ParseEnum(passInputType, "Depth"));
        }

        private static void RetuneScreenMaterials()
        {
            var fog = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_FX_DistanceFog.mat");
            if (fog != null)
            {
                SetMaterialColor(fog, "_FogColor", new Color(0.07f, 0.025f, 0.12f));
                SetMaterialColor(fog, "_GroundColor", new Color(0.78f, 0.1f, 0.55f));
                SetMaterialFloat(fog, "_GroundStrength", 0.5f);
                SetMaterialFloat(fog, "_Darken", 0.45f);
                EditorUtility.SetDirty(fog);
            }

            var tint = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_FX_RedTint.mat");
            if (tint != null)
            {
                SetMaterialColor(tint, "_TintColor", new Color(0.98f, 0.9f, 1f));
                SetMaterialFloat(tint, "_TintStrength", 0.5f);
                EditorUtility.SetDirty(tint);
            }
        }

        private static ScriptableObject BuildProfile()
        {
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile" + CoreAssembly);
            if (profileType == null)
            {
                Debug.LogError("[DreamAtmosphere] VolumeProfile 타입을 찾지 못했습니다.");
                return null;
            }

            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder(ProfileFolder))
                {
                    AssetDatabase.CreateFolder("Assets/@UnityGameJam/5_Art", "PostProcess");
                }
                profile = ScriptableObject.CreateInstance(profileType);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Configure(profile, "Tonemapping", component => SetInt(component, "mode", 2)); // ACES
            Configure(profile, "Bloom", component =>
            {
                SetFloat(component, "threshold", 1.05f);
                SetFloat(component, "intensity", 0.85f);
                SetFloat(component, "scatter", 0.7f);
                SetColor(component, "tint", new Color(1f, 0.78f, 0.95f));
                SetBool(component, "highQualityFiltering", true);
            });
            Configure(profile, "ColorAdjustments", component =>
            {
                SetFloat(component, "postExposure", 0.2f);
                SetFloat(component, "contrast", 25f);
                SetColor(component, "colorFilter", new Color(1f, 0.94f, 1f));
                SetFloat(component, "saturation", 12f);
            });
            Configure(profile, "SplitToning", component =>
            {
                // 어두운 곳은 보라, 밝은 곳은 따뜻한 주황·분홍으로 갈라 레퍼런스처럼 대비를 만든다.
                SetColor(component, "shadows", new Color(0.4f, 0.22f, 0.7f));
                SetColor(component, "highlights", new Color(1f, 0.72f, 0.5f));
                SetFloat(component, "balance", 0f);
            });
            Configure(profile, "WhiteBalance", component =>
            {
                SetFloat(component, "temperature", 0f);
                SetFloat(component, "tint", 10f);
            });
            Configure(profile, "Vignette", component =>
            {
                SetColor(component, "color", new Color(0.06f, 0f, 0.1f));
                SetFloat(component, "intensity", 0.3f);
                SetFloat(component, "smoothness", 0.5f);
            });
            Configure(profile, "ChromaticAberration", component => SetFloat(component, "intensity", 0.22f));
            Configure(profile, "FilmGrain", component =>
            {
                SetInt(component, "type", 2); // Medium1
                SetFloat(component, "intensity", 0.2f);
                SetFloat(component, "response", 0.75f);
            });
            Configure(profile, "DepthOfField", component =>
            {
                SetInt(component, "mode", 1); // Gaussian: 플레이어보다 먼 곳만 살짝 흐리게
                SetFloat(component, "gaussianStart", 15f);
                SetFloat(component, "gaussianEnd", 34f);
                SetFloat(component, "gaussianMaxRadius", 1f);
                SetBool(component, "highQualitySampling", true);
            });

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void Configure(ScriptableObject profile, string typeName, Action<SerializedObject> setup)
        {
            Type type = Type.GetType($"UnityEngine.Rendering.Universal.{typeName}{UrpAssembly}");
            if (type == null)
            {
                Debug.LogWarning($"[DreamAtmosphere] URP 후처리 '{typeName}'을 찾지 못해 건너뜁니다.");
                return;
            }

            ScriptableObject component = FindComponent(profile, type);
            if (component == null)
            {
                MethodInfo add = profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
                component = (ScriptableObject)add.Invoke(profile, new object[] { type, false });
                component.name = type.Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            var serialized = new SerializedObject(component);
            SerializedProperty active = serialized.FindProperty("active");
            if (active != null) { active.boolValue = true; }
            setup(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static ScriptableObject FindComponent(ScriptableObject profile, Type type)
        {
            FieldInfo field = profile.GetType().GetField("components", BindingFlags.Public | BindingFlags.Instance);
            if (field?.GetValue(profile) is not IList components) { return null; }
            foreach (object item in components)
            {
                if (item is ScriptableObject component && component != null && component.GetType() == type) { return component; }
            }
            return null;
        }

        private static void AttachVolume(ScriptableObject profile)
        {
            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume" + CoreAssembly);
            if (volumeType == null) { return; }

            GameObject root = PrefabUtility.LoadPrefabContents(GlobalVolumePrefabPath);
            try
            {
                Transform existing = root.transform.Find(VolumeObjectName);
                GameObject holder = existing != null ? existing.gameObject : new GameObject(VolumeObjectName);
                holder.transform.SetParent(root.transform, false);

                Component volume = holder.GetComponent(volumeType);
                if (volume == null) { volume = holder.AddComponent(volumeType); }

                // 기본 볼륨(공동 설정 프로필)은 그대로 두고, 우선순위가 높은 볼륨을 위에 얹는다.
                var serialized = new SerializedObject(volume);
                serialized.FindProperty("m_IsGlobal").boolValue = true;
                serialized.FindProperty("priority").floatValue = 10f;
                serialized.FindProperty("weight").floatValue = 1f;
                serialized.FindProperty("sharedProfile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GlobalVolumePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnablePostProcessing()
        {
            Type dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData" + UrpAssembly);
            if (dataType == null) { return; }

            GameObject root = PrefabUtility.LoadPrefabContents(CameraPrefabPath);
            try
            {
                Component data = root.GetComponent(dataType);
                if (data == null) { return; }
                var serialized = new SerializedObject(data);
                serialized.FindProperty("m_RenderPostProcessing").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void TintSunlight()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LightPrefabPath);
            try
            {
                Light sun = root.GetComponentInChildren<Light>();
                if (sun == null) { return; }
                sun.color = new Color(1f, 0.8f, 0.88f);
                sun.intensity = 1.2f;
                PrefabUtility.SaveAsPrefabAsset(root, LightPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void TintAmbient()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("/Facility.unity", StringComparison.Ordinal))
            {
                Debug.LogWarning("[DreamAtmosphere] Facility 씬을 연 상태에서 다시 실행하면 주변광도 보라색으로 바뀝니다.");
                return;
            }

            // 그림자 쪽이 보라로 물들도록 하늘·옆·바닥 세 색 주변광을 쓴다.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.26f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.15f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.06f, 0.12f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static SerializedProperty Parameter(SerializedObject component, string name)
        {
            SerializedProperty parameter = component.FindProperty(name);
            if (parameter == null)
            {
                Debug.LogWarning($"[DreamAtmosphere] {component.targetObject.GetType().Name}.{name} 값을 찾지 못했습니다.");
                return null;
            }
            parameter.FindPropertyRelative("m_OverrideState").boolValue = true;
            return parameter.FindPropertyRelative("m_Value");
        }

        private static void SetFloat(SerializedObject component, string name, float value)
        {
            SerializedProperty property = Parameter(component, name);
            if (property != null) { property.floatValue = value; }
        }

        private static void SetInt(SerializedObject component, string name, int value)
        {
            SerializedProperty property = Parameter(component, name);
            if (property != null) { property.intValue = value; }
        }

        private static void SetBool(SerializedObject component, string name, bool value)
        {
            SerializedProperty property = Parameter(component, name);
            if (property != null) { property.boolValue = value; }
        }

        private static void SetColor(SerializedObject component, string name, Color value)
        {
            SerializedProperty property = Parameter(component, name);
            if (property != null) { property.colorValue = value; }
        }

        private static void SetMaterialColor(Material material, string name, Color value)
        {
            if (material.HasProperty(name)) { material.SetColor(name, value); }
        }

        private static void SetMaterialFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name)) { material.SetFloat(name, value); }
        }
    }
}
