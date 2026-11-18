using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace UnityGameJam.Editor.Art
{
    /// <summary>
    /// 아트 적용 상태를 한눈에 보는 창(Tools > UnityGameJam > Art > 검사).
    /// 상태 판정은 ArtBinder/ArtBindingPlanner가 하고, 이 창은 보여 주기와 버튼만 맡는다.
    /// </summary>
    public sealed class ArtBindingWindow : EditorWindow
    {
        private const string MenuRoot = "Tools/UnityGameJam/Art/";
        private const string AllFilter = "전체";

        private static readonly Dictionary<EArtBindingStatus, (string Label, Color Color)> _statusStyles = new Dictionary<EArtBindingStatus, (string, Color)>
        {
            { EArtBindingStatus.Applied, ("✔ 적용됨", new Color(0.35f, 0.75f, 0.4f)) },
            { EArtBindingStatus.Ready, ("● 적용 가능", new Color(0.35f, 0.6f, 0.95f)) },
            { EArtBindingStatus.MissingModel, ("○ 모델 없음", new Color(0.6f, 0.6f, 0.6f)) },
            { EArtBindingStatus.Orphan, ("? 주인 없음", new Color(0.95f, 0.8f, 0.3f)) },
            { EArtBindingStatus.InvalidName, ("✖ 이름 오류", new Color(0.95f, 0.4f, 0.4f)) },
            { EArtBindingStatus.Duplicate, ("✖ 중복", new Color(0.95f, 0.4f, 0.4f)) },
            { EArtBindingStatus.Base, ("- 베이스", new Color(0.5f, 0.5f, 0.5f)) },
        };

        private List<ArtBindingItem> _items = new List<ArtBindingItem>();
        private List<string> _sceneViolations = new List<string>();
        private string _filter = AllFilter;
        private ScrollView _list;
        private Label _summary;

        public static void Open()
        {
            var window = GetWindow<ArtBindingWindow>("Art Binding");
            window.minSize = new Vector2(760f, 320f);
            window.Refresh();
        }

        public static void RefreshIfOpen()
        {
            if (HasOpenInstances<ArtBindingWindow>()) { GetWindow<ArtBindingWindow>(false, "Art Binding", false).Refresh(); }
        }

        [MenuItem(MenuRoot + "아트 키 목록 내보내기 (Export Key List)", priority = 20)]
        private static void ExportKeyListMenu()
        {
            string markdown = BuildKeyListMarkdown(ArtBinder.Inspect(ArtBinder.DefaultPrefabRoot, ArtBinder.DefaultModelRoot));
            EditorGUIUtility.systemCopyBuffer = markdown;
            Debug.Log("[Art] 아트 키 목록을 클립보드에 복사했습니다. docs/rules/art-asset.md 4절과 비교하세요.\n" + markdown);
        }

        /// <summary>씬 규칙(docs/rules/prefab.md 1절): 프리팹 인스턴스가 아니면서 Transform 말고 다른 컴포넌트가 있으면 위반.</summary>
        public static List<string> FindSceneViolations(Scene scene)
        {
            var violations = new List<string>();
            if (!scene.IsValid() || scene.path.EndsWith("SampleScene.unity", StringComparison.Ordinal)) { return violations; }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject target = transform.gameObject;
                    if (PrefabUtility.IsPartOfPrefabInstance(target)) { continue; }
                    if (target.GetComponents<Component>().Length <= 1) { continue; } // Transform만 있는 정리용 오브젝트
                    violations.Add(GetHierarchyPath(transform));
                }
            }
            return violations;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            toolbar.Add(new Button(Refresh) { text = "검사" });
            toolbar.Add(new Button(() => RunAndRefresh(() => ArtBinder.ApplyAll(ArtBinder.DefaultPrefabRoot, ArtBinder.DefaultModelRoot))) { text = "전체 적용" });
            toolbar.Add(new Button(() => RunAndRefresh(ApplySelected)) { text = "선택 적용" });
            toolbar.Add(new Button(() => RunAndRefresh(RevertSelected)) { text = "되돌리기" });
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var filterChoices = new List<string> { AllFilter };
            filterChoices.AddRange(_statusStyles.Values.Select(v => v.Label));
            var filterField = new PopupField<string>("필터", filterChoices, 0) { style = { minWidth = 200 } };
            filterField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue;
                Rebuild();
            });
            toolbar.Add(filterField);
            root.Add(toolbar);

            _summary = new Label { style = { marginBottom = 4, unityFontStyleAndWeight = FontStyle.Bold } };
            root.Add(_summary);
            root.Add(CreateRow("상태", "ArtKey", "프리팹", "원본 / 메시지", Color.clear, true));

            _list = new ScrollView { style = { flexGrow = 1 } };
            root.Add(_list);
            Rebuild();
        }

        private void Refresh()
        {
            _items = ArtBinder.Inspect(ArtBinder.DefaultPrefabRoot, ArtBinder.DefaultModelRoot);
            _sceneViolations = FindSceneViolations(SceneManager.GetActiveScene());
            Rebuild();
        }

        private void RunAndRefresh(Func<int> action)
        {
            int count = action();
            Debug.Log($"[Art] 프리팹 {count}개 변경");
            Refresh();
        }

        private int ApplySelected()
        {
            HashSet<string> selected = SelectedPrefabPaths();
            return _items.Count(item => item.PrefabPath != null && selected.Contains(item.PrefabPath) && ArtBinder.ApplyOrRefresh(item));
        }

        private int RevertSelected()
        {
            return SelectedPrefabPaths().Count(ArtBinder.Revert);
        }

        private void Rebuild()
        {
            if (_list == null) { return; }
            _list.Clear();

            IEnumerable<IGrouping<EArtBindingStatus, ArtBindingItem>> groups = _items.GroupBy(i => i.Status);
            _summary.text = string.Join("   ", groups.Select(g => $"{_statusStyles[g.Key].Label} {g.Count()}"))
                + (_sceneViolations.Count > 0 ? $"   ✖ 씬 규칙 위반 {_sceneViolations.Count}" : "   ✔ 씬 규칙 통과");

            foreach (ArtBindingItem item in _items)
            {
                (string label, Color color) = _statusStyles[item.Status];
                if (_filter != AllFilter && _filter != label) { continue; }

                string source = item.SourcePath ?? item.Message;
                VisualElement row = CreateRow(label, item.ArtKey, Shorten(item.PrefabPath), Shorten(source), color, false);
                row.tooltip = item.Message;
                string pingPath = item.PrefabPath ?? item.SourcePath;
                row.RegisterCallback<ClickEvent>(_ => Ping(pingPath));
                _list.Add(row);
            }

            if (_filter == AllFilter)
            {
                foreach (string violation in _sceneViolations)
                {
                    _list.Add(CreateRow("✖ 씬 규칙", "-", violation, "프리팹 인스턴스가 아닙니다 (docs/rules/prefab.md)", new Color(0.95f, 0.4f, 0.4f), false));
                }
            }
        }

        private static VisualElement CreateRow(string status, string key, string prefab, string source, Color color, bool isHeader)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 2, paddingBottom = 2 } };
            if (isHeader)
            {
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            }
            row.Add(CreateCell(status, 110, color, isHeader));
            row.Add(CreateCell(key, 180, Color.clear, isHeader));
            row.Add(CreateCell(prefab, 240, Color.clear, isHeader));
            Label last = CreateCell(source, 0, Color.clear, isHeader);
            last.style.flexGrow = 1;
            row.Add(last);
            return row;
        }

        private static Label CreateCell(string text, float width, Color color, bool isHeader)
        {
            var label = new Label(text ?? "-") { style = { overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
            if (width > 0) { label.style.width = width; }
            if (color != Color.clear) { label.style.color = color; }
            if (isHeader) { label.style.unityFontStyleAndWeight = FontStyle.Bold; }
            return label;
        }

        private static string BuildKeyListMarkdown(List<ArtBindingItem> items)
        {
            var builder = new StringBuilder();
            builder.AppendLine("| 분류 | ArtKey | 상태 | 프리팹 | 원본 |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (ArtBindingItem item in items.OrderBy(i => i.ArtKey, StringComparer.Ordinal))
            {
                string category = ArtKeyRule.TryGetCategory(item.ArtKey, out EArtCategory value) ? value.ToString() : "-";
                builder.AppendLine($"| {category} | `{item.ArtKey}` | {_statusStyles[item.Status].Label} | {Shorten(item.PrefabPath)} | {Shorten(item.SourcePath)} |");
            }
            return builder.ToString();
        }

        private static string Shorten(string path)
        {
            return string.IsNullOrEmpty(path) ? "-" : path.Replace("Assets/@UnityGameJam/", string.Empty);
        }

        private static void Ping(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains(",")) { return; }
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) { return; }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static HashSet<string> SelectedPrefabPaths()
        {
            return new HashSet<string>(Selection.objects.Select(AssetDatabase.GetAssetPath).Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) { builder.Insert(0, parent.name + "/"); }
            return builder.ToString();
        }
    }
}
