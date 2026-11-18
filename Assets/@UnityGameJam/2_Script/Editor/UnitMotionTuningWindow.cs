using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityGameJam.View;

namespace UnityGameJam.Editor
{
    public sealed class UnitMotionTuningWindow : EditorWindow
    {
        private const string SettingsPath = "Assets/@UnityGameJam/3_Data/ScriptableObject/View/ViewSettings.asset";
        private readonly List<(string Group, string Property, FloatField Field)> _fields = new();
        private ViewSettingsSO _settings;
        private Label _status;

        [MenuItem("Tools/UnityGameJam/레벨 디자인")]
        public static void Open()
        {
            var window = GetWindow<UnitMotionTuningWindow>();
            window.titleContent = new GUIContent("레벨 디자인");
            window.minSize = new Vector2(510f, 400f);
        }

        private void OnEnable() => Undo.undoRedoPerformed += Reload;
        private void OnDisable() => Undo.undoRedoPerformed -= Reload;

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _fields.Clear();
            _settings = AssetDatabase.LoadAssetAtPath<ViewSettingsSO>(SettingsPath);
            if (_settings == null)
            {
                rootVisualElement.Add(new HelpBox("ViewSettings 에셋을 찾을 수 없습니다.", HelpBoxMessageType.Error));
                return;
            }

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;
            scroll.Add(new Label("1은 기본 배율입니다. 애니메이션 배속은 재생 속도에 적용됩니다."));
            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.Add(CreateUnit("플레이어", "_playerMotion", true));
            columns.Add(CreateUnit("고블린", "_goblinMotion", false));
            scroll.Add(columns);
            rootVisualElement.Add(scroll);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginLeft = 12;
            footer.style.marginBottom = 8;
            footer.Add(new Button(Save) { text = "저장" });
            footer.Add(new Button(Reload) { text = "저장값 불러오기" });
            _status = new Label();
            _status.style.marginLeft = 8;
            _status.style.alignSelf = Align.Center;
            footer.Add(_status);
            rootVisualElement.Add(footer);
            Reload();
        }

        private VisualElement CreateUnit(string title, string group, bool canCarryHeavy)
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            column.style.marginRight = 10;
            var heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = 10;
            heading.style.marginBottom = 4;
            column.Add(heading);
            AddSection("이동 속도 (배)");
            Add("걷기", "_walkRate");
            Add("달리기", "_runRate");
            Add("한 손 운반", "_carryRate");
            if (canCarryHeavy) { Add("두 손 운반", "_heavyCarryRate"); }
            Add("걸레 이동", "_mopWalkRate");

            AddSection("애니메이션 배속 (배)");
            Add("대기", "_idleAnimationRate");
            Add("걷기", "_walkAnimationRate");
            Add("달리기", "_runAnimationRate");
            Add("한 손 운반", "_carryAnimationRate");
            if (canCarryHeavy) { Add("두 손 운반", "_heavyCarryAnimationRate"); }
            Add("걸레 이동", "_mopWalkAnimationRate");
            Add("물걸레질", "_mopAnimationRate");
            Add("집기", "_pickupRate");
            Add("내려놓기", "_dropRate");
            return column;

            void AddSection(string label)
            {
                var section = new Label(label);
                section.style.unityFontStyleAndWeight = FontStyle.Bold;
                section.style.marginTop = 6;
                column.Add(section);
            }

            void Add(string label, string property)
            {
                var field = new FloatField(label) { name = group + property };
                field.labelElement.style.minWidth = 85;
                _fields.Add((group, property, field));
                column.Add(field);
            }
        }

        private void Save()
        {
            var serialized = new SerializedObject(_settings);
            foreach (var (group, property, field) in _fields)
            {
                float rate = UnitMotionTuning.NormalizeRate(field.value);
                SerializedProperty value = serialized.FindProperty(group).FindPropertyRelative(property);
                if (rate != UnitMotionTuning.NormalizeRate(value.floatValue)) { value.floatValue = rate; }
                field.SetValueWithoutNotify(rate);
            }
            if (serialized.ApplyModifiedProperties()) { AssetDatabase.SaveAssetIfDirty(_settings); }
            _status.text = "저장 완료 · 다음 판부터 적용";
        }

        private void Reload()
        {
            if (_settings == null || _status == null) { return; }
            var serialized = new SerializedObject(_settings);
            foreach (var (group, property, field) in _fields)
            {
                var value = serialized.FindProperty(group).FindPropertyRelative(property);
                field.SetValueWithoutNotify(UnitMotionTuning.NormalizeRate(value.floatValue));
            }
            _status.text = string.Empty;
        }
    }
}
