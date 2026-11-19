using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// UI_WorkGauge의 단색 막대(Track/Fill)를 UI_WorkGaugeFrame.png 테두리 안에 들어가게 바꾼다.
    /// FacilityHud가 Fill.anchorMax.x로 진행도를 표시하므로 Track·Fill 이름과 계층은 그대로 둔다.
    /// 프리팹 YAML을 손으로 고치지 않기 위해 에디터에서 처리한다. 다시 실행해도 결과가 같다.
    /// </summary>
    internal static class WorkGaugeSkinApplier
    {
        private const string MenuPath = "Tools/UnityGameJam/UI/Apply Work Gauge Frame";
        private const string PrefabPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_WorkGauge.prefab";
        private const string HudRootPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab";
        private const string FrameSpritePath = "Assets/@UnityGameJam/5_Art/Sprite/UI_WorkGaugeFrame.png";
        private const string FrameObjectName = "Frame";

        // 원본 PNG(2226x302)에서 잰 안쪽 빈 영역 비율. 이 안에 Track을 맞춘다.
        private const float InnerLeft = 0.072f;
        private const float InnerRight = 0.072f;
        private const float InnerTop = 0.37f;
        private const float InnerBottom = 0.37f;
        private const float FrameAspect = 2226f / 302f;

        // 패널 안에서 테두리가 놓일 자리. 라벨(위쪽 35px)과 겹치지 않게 패널 높이를 늘린다.
        private const float PanelHeight = 84f;
        private const float FrameLeft = 8f;
        private const float FrameBottom = 6f;
        private const float FrameWidth = 296f;

        private static readonly Color TrackColor = new Color(0.16f, 0.07f, 0.13f, 1f);
        private static readonly Color FillColor = new Color(0.86f, 0.15f, 0.24f, 1f);

        [MenuItem(MenuPath)]
        private static void Apply()
        {
            var frameSprite = ImportFrameSprite();
            if (frameSprite == null) { return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var track = root.transform.Find("Track") as RectTransform;
                var fill = track != null ? track.Find("Fill") as RectTransform : null;
                if (track == null || fill == null)
                {
                    Debug.LogError("[WorkGauge] UI_WorkGauge에서 Track/Fill을 찾을 수 없습니다. 계층 이름을 확인하세요.");
                    return;
                }

                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, PanelHeight);

                float frameHeight = FrameWidth / FrameAspect;
                track.anchorMin = Vector2.zero;
                track.anchorMax = Vector2.zero;
                track.pivot = Vector2.zero;
                track.anchoredPosition = new Vector2(FrameLeft + FrameWidth * InnerLeft, FrameBottom + frameHeight * InnerBottom);
                track.sizeDelta = new Vector2(FrameWidth * (1f - InnerLeft - InnerRight), frameHeight * (1f - InnerTop - InnerBottom));
                track.GetComponent<Image>().color = TrackColor;
                fill.GetComponent<Image>().color = FillColor;

                // 테두리는 Fill 위에 그려야 하므로 Track의 마지막 자식으로 둔다.
                var frame = track.Find(FrameObjectName) as RectTransform;
                if (frame == null)
                {
                    var frameObject = new GameObject(FrameObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    frameObject.layer = track.gameObject.layer;
                    frame = (RectTransform)frameObject.transform;
                    frame.SetParent(track, false);
                }
                frame.SetAsLastSibling();
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.pivot = new Vector2(0.5f, 0.5f);
                frame.offsetMin = new Vector2(-FrameWidth * InnerLeft, -frameHeight * InnerBottom);
                frame.offsetMax = new Vector2(FrameWidth * InnerRight, frameHeight * InnerTop);

                var frameImage = frame.GetComponent<Image>();
                frameImage.sprite = frameSprite;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = false;
                frameImage.raycastTarget = false;
                frameImage.color = Color.white;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            RevertHudRootSizeOverride();
            Debug.Log("[WorkGauge] UI_WorkGauge에 게이지 테두리를 적용했습니다.");
        }

        // UI_HudRoot가 게이지 크기(312x68)를 덮어쓰고 있어서, 프리팹에서 늘린 높이가 게임에 반영되도록 오버라이드를 지운다.
        private static void RevertHudRootSizeOverride()
        {
            var hudRoot = PrefabUtility.LoadPrefabContents(HudRootPath);
            try
            {
                RectTransform gauge = null;
                foreach (var rect in hudRoot.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "UI_WorkGauge" && PrefabUtility.IsAnyPrefabInstanceRoot(rect.gameObject))
                    {
                        gauge = rect;
                        break;
                    }
                }
                if (gauge == null)
                {
                    Debug.LogWarning("[WorkGauge] UI_HudRoot 안에서 UI_WorkGauge 인스턴스를 찾지 못해 크기 오버라이드를 건너뜁니다.");
                    return;
                }

                var sizeProperty = new SerializedObject(gauge).FindProperty("m_SizeDelta");
                if (sizeProperty.prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(sizeProperty, InteractionMode.AutomatedAction);
                    PrefabUtility.SaveAsPrefabAsset(hudRoot, HudRootPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hudRoot);
            }
        }

        private static Sprite ImportFrameSprite()
        {
            var importer = AssetImporter.GetAtPath(FrameSpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[WorkGauge] 테두리 이미지를 찾을 수 없습니다: {FrameSpritePath}");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
        }
    }
}
