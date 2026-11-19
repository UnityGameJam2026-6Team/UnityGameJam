using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityGameJam.UI;

namespace UnityGameJam.Editor
{
    /// <summary>
    /// 하단 졸개 현황 패널(UI_MinionStatusPanel)을 화면 오른쪽 아래에 비스듬히 겹쳐 쌓은 초상화 카드로 바꾼다.
    /// 위에서부터 슬라임 → 스켈레톤 → 오크 → 고블린 순서이고 아래 카드가 위 카드를 덮는다.
    /// 마우스를 올리면 MinionCardStack이 카드를 골라 MinionCardHover가 맨 앞으로 꺼내 위치·상태 글자를 보여 준다.
    /// MinionStatusPanel이 쓰는 Slot의 Name/Room/Activity 텍스트와 Header/Title/Body는 이름·계층을 그대로 둔다.
    /// 카드 PNG는 1080p의 2배로 그려져 있어 절반 크기를 기본으로 놓고 CardScale로 줄인다(종이는 자르지 않은 원본 전체).
    /// 에디터 어셈블리가 TextMeshPro를 참조하지 않으므로(asmdef는 공동 설정) TMP 값은 SerializedObject로 바꾼다.
    /// </summary>
    internal static class MinionCardSkinApplier
    {
        private const string MenuPath = "Tools/UnityGameJam/UI/Apply Minion Status Cards";
        private const string PanelPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_MinionStatusPanel.prefab";
        private const string HudRootPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab";
        private const string CardFolder = "Assets/@UnityGameJam/5_Art/Sprite/MinionCard";

        // 1080p 기준. 카드 위치는 카드 왼쪽 위 모서리, 화면 오른쪽 아래 모서리 기준(y는 위로 +).
        private const float SpriteToCanvas = 0.5f;
        private const float CardScale = 0.85f;
        private const float CardRotation = 9f;
        private const float HeaderHeight = 30f;
        private const float HeaderWidth = 240f;
        private const float GaugeGap = 8f;
        // 카드 묶음과 요청 카드 레인이 겹치지 않도록 오른쪽에 비워 두는 폭.
        private const float ColumnReserve = 300f;
        private const float ScreenMargin = 28f;

        // 그리는 순서(뒤 → 앞). 아래쪽 카드일수록 앞에 그린다.
        private static readonly string[] SlotNames = { "SlimeSlot", "SkeletonSlot", "OrcSlot", "GoblinSlot" };
        private static readonly string[] CardFiles = { "Slime", "Skeleton", "Orc", "Goblin" };
        private static readonly Vector2[] CardPositions =
        {
            new Vector2(-205f, 790f),
            new Vector2(-230f, 610f),
            new Vector2(-250f, 430f),
            new Vector2(-262f, 250f)
        };
        // 첫 카드(슬라임) 위에 놓는 접기/펼치기 탭. 패널 위쪽 끝 기준.
        private static readonly Vector2 HeaderPosition = new Vector2(-20f, 800f);

        // 카드 왼쪽 위 기준 글자 칸(x, y, w, h). 이름판은 초상화를 조금 덮도록 원본보다 30 올렸다.
        private static readonly Rect NameSlot = new Rect(51f, 180f, 116f, 43f);
        private static readonly Rect RoomSlot = new Rect(205f, 75f, 196f, 60f);
        private static readonly Rect ActivitySlot = new Rect(205f, 143f, 196f, 75f);

        // 종이를 바닥 돌 색(회보라)으로 물들여 두어서, 글자는 밝은 색으로 쓴다. 탭도 같은 돌 색 계열로 맞춘다.
        private static readonly Color NameColor = new Color(1f, 0.95f, 0.98f, 1f);
        private static readonly Color RoomColor = new Color(0.97f, 0.93f, 0.96f, 1f);
        private static readonly Color ActivityColor = new Color(1f, 0.62f, 0.68f, 1f);
        private static readonly Color HeaderColor = new Color(0.17f, 0.15f, 0.19f, 0.92f);
        private static readonly Color TitleColor = new Color(0.95f, 0.9f, 0.95f, 1f);

        [MenuItem(MenuPath)]
        private static void Apply()
        {
            var cards = new Sprite[CardFiles.Length];
            for (int i = 0; i < CardFiles.Length; i++)
            {
                cards[i] = ImportCardSprite($"{CardFolder}/UI_MinionCard_{CardFiles[i]}.png");
                if (cards[i] == null) { return; }
            }

            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var body = root.transform.Find("Body") as RectTransform;
                var header = root.transform.Find("Header") as RectTransform;
                if (body == null || header == null)
                {
                    Debug.LogError("[MinionCard] UI_MinionStatusPanel에서 Body/Header를 찾을 수 없습니다.");
                    return;
                }

                // 패널 자체는 화면 아래의 얇은 띠로 둔다. 요청 카드와 작업 게이지가 이 위쪽 끝을 기준으로 놓이고,
                // 카드는 이 띠를 벗어나 오른쪽 위로 쌓인다(Canvas는 잘라내지 않는다).
                SetPanelRootRect((RectTransform)root.transform);

                header.anchorMin = new Vector2(1f, 1f);
                header.anchorMax = new Vector2(1f, 1f);
                header.pivot = new Vector2(1f, 0f);
                header.anchoredPosition = HeaderPosition;
                header.sizeDelta = new Vector2(HeaderWidth, HeaderHeight);
                var headerImage = header.GetComponent<Image>();
                if (headerImage != null) { headerImage.color = HeaderColor; }
                SetTextColor(header.Find("Title"), TitleColor);

                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = Vector2.zero;
                body.offsetMax = Vector2.zero;
                var bodyImage = body.GetComponent<Image>();
                if (bodyImage != null) { bodyImage.enabled = false; }
                if (body.GetComponent<MinionCardStack>() == null) { body.gameObject.AddComponent<MinionCardStack>(); }

                var rows = body.Find("Rows");
                if (rows != null)
                {
                    rows.gameObject.SetActive(false);
                    rows.SetAsFirstSibling();
                }

                for (int i = 0; i < SlotNames.Length; i++)
                {
                    var slot = body.Find(SlotNames[i]) as RectTransform;
                    if (slot == null)
                    {
                        Debug.LogError($"[MinionCard] {SlotNames[i]}를 찾을 수 없습니다.");
                        return;
                    }
                    ApplyCard(slot, cards[i], CardPositions[i]);
                    slot.SetSiblingIndex(i + 1);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            UpdateHudRoot();
            Debug.Log("[MinionCard] 졸개 카드를 오른쪽 아래에 쌓고, 마우스를 올리면 앞으로 나오게 했습니다.");
        }

        private static void SetPanelRootRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, HeaderHeight);
        }

        private static void ApplyCard(RectTransform slot, Sprite card, Vector2 position)
        {
            slot.anchorMin = new Vector2(1f, 0f);
            slot.anchorMax = new Vector2(1f, 0f);
            slot.pivot = new Vector2(0f, 1f);
            slot.anchoredPosition = position;
            slot.sizeDelta = card.rect.size * SpriteToCanvas;
            slot.localEulerAngles = new Vector3(0f, 0f, CardRotation);
            slot.localScale = new Vector3(CardScale, CardScale, 1f);

            var image = slot.GetComponent<Image>();
            image.sprite = card;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false; // 마우스 판정은 MinionCardStack이 직접 한다

            if (slot.GetComponent<MinionCardHover>() == null) { slot.gameObject.AddComponent<MinionCardHover>(); }

            var portrait = slot.Find("PortraitPlaceholder");
            if (portrait != null) { portrait.gameObject.SetActive(false); }

            ApplyText(slot.Find("Name") as RectTransform, NameSlot, NameColor, 26f, 14f, 2);
            ApplyText(slot.Find("Room") as RectTransform, RoomSlot, RoomColor, 24f, 14f, 1);
            ApplyText(slot.Find("Activity") as RectTransform, ActivitySlot, ActivityColor, 22f, 14f, 1);
        }

        private static void SetTextColor(Transform text, Color color)
        {
            var tmp = text != null ? text.GetComponent("TextMeshProUGUI") : null;
            if (tmp == null) { return; }
            var serialized = new SerializedObject(tmp);
            serialized.FindProperty("m_fontColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // horizontalAlignment: 1 = 왼쪽, 2 = 가운데 (TMP HorizontalAlignmentOptions)
        private static void ApplyText(RectTransform text, Rect slot, Color color, float maxSize, float minSize, int horizontalAlignment)
        {
            if (text == null) { return; }
            text.anchorMin = new Vector2(0f, 1f);
            text.anchorMax = new Vector2(0f, 1f);
            text.pivot = new Vector2(0f, 1f);
            text.anchoredPosition = new Vector2(slot.x, -slot.y);
            text.sizeDelta = new Vector2(slot.width, slot.height);

            var tmp = text.GetComponent("TextMeshProUGUI");
            if (tmp == null) { return; }
            var serialized = new SerializedObject(tmp);
            serialized.FindProperty("m_fontColor").colorValue = color;
            serialized.FindProperty("m_fontSize").floatValue = maxSize;
            serialized.FindProperty("m_fontSizeBase").floatValue = maxSize;
            serialized.FindProperty("m_enableAutoSizing").boolValue = true;
            serialized.FindProperty("m_fontSizeMin").floatValue = minSize;
            serialized.FindProperty("m_fontSizeMax").floatValue = maxSize;
            serialized.FindProperty("m_fontStyle").intValue = 1; // Bold
            serialized.FindProperty("m_HorizontalAlignment").intValue = horizontalAlignment;
            serialized.FindProperty("m_VerticalAlignment").intValue = 512; // Middle
            serialized.FindProperty("m_RaycastTarget").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // UI_HudRoot가 패널·작업 게이지·요청 카드 묶음의 크기와 위치를 덮어쓰고 있어 새 배치에 맞춘다.
        private static void UpdateHudRoot()
        {
            var hudRoot = PrefabUtility.LoadPrefabContents(HudRootPath);
            try
            {
                bool hasChanged = false;
                foreach (var rect in hudRoot.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(rect.gameObject)) { continue; }
                    switch (rect.name)
                    {
                        case "UI_MinionStatusPanel":
                            SetPanelRootRect(rect);
                            hasChanged = true;
                            break;
                        case "UI_WorkGauge":
                            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, HeaderHeight + GaugeGap);
                            hasChanged = true;
                            break;
                        case "UI_ProposalStack":
                            // 레인은 이 폭을 4등분해 쓰므로, 오른쪽 카드 묶음 자리를 빼고 왼쪽 여백은 그대로 둔다.
                            rect.sizeDelta = new Vector2(-(ColumnReserve + ScreenMargin), rect.sizeDelta.y);
                            rect.anchoredPosition = new Vector2((ScreenMargin - ColumnReserve) * 0.5f, rect.anchoredPosition.y);
                            hasChanged = true;
                            break;
                    }
                }
                if (hasChanged) { PrefabUtility.SaveAsPrefabAsset(hudRoot, HudRootPath); }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hudRoot);
            }
        }

        private static Sprite ImportCardSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[MinionCard] 카드 이미지를 찾을 수 없습니다: {path}");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
