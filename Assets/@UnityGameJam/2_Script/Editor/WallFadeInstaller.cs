using UnityEditor;
using UnityEngine;
using UnityGameJam.Player;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// 카메라와 플레이어 사이 벽을 반투명하게 만드는 WallOcclusionFader를 SYS_MainCamera 프리팹에 붙인다.
    /// 머티리얼(M_FX_WallFade)이 없으면 만들고 컴포넌트에 연결한다. 여러 번 실행해도 하나만 붙는다.
    /// </summary>
    internal static class WallFadeInstaller
    {
        private const string MenuPath = "Tools/UnityGameJam/Camera/Install Wall Fade";
        private const string CameraPrefabPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_MainCamera.prefab";

        [MenuItem(MenuPath)]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[WallFade] 플레이를 멈춘 뒤 다시 실행하세요.");
                return;
            }

            Material material = ScreenEffectInstaller.GetOrCreateMaterial("UnityGameJam/Wall Fade", "M_FX_WallFade");
            if (material == null) { return; }

            // 다시 실행하면 셰이더에 적힌 기본값으로 되돌린다.
            var defaults = new Material(material.shader);
            material.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(material);

            GameObject root = PrefabUtility.LoadPrefabContents(CameraPrefabPath);
            try
            {
                var fader = root.GetComponent<WallOcclusionFader>();
                if (fader == null) { fader = root.AddComponent<WallOcclusionFader>(); }
                var serialized = new SerializedObject(fader);
                serialized.FindProperty("_fadeMaterial").objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WallFade] 설치 완료. 구멍 크기·투명도는 SYS_MainCamera의 WallOcclusionFader에서 조정합니다.");
        }
    }
}
