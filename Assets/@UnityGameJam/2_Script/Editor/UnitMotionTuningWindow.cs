using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Editor.Art;
using UnityGameJam.Match;
using UnityGameJam.View;

namespace UnityGameJam.Editor
{
    public sealed class UnitMotionTuningWindow : EditorWindow
    {
        private const string DataPath = "Assets/@UnityGameJam/3_Data/ScriptableObject/";
        private const string BootstrapPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_Bootstrap.prefab";
        private readonly List<SerializedObject> _assets = new();
        private readonly List<Action> _readFields = new();
        private readonly List<Action> _writeFields = new();
        private readonly List<(IntegerField Minimum, IntegerField Maximum, string Label)> _ranges = new();
        private Label _status;
        private MatchFactory _factory;
        private bool _spawnSettingsEdited;
        private bool _isRollingBack;

        [MenuItem("Tools/UnityGameJam/레벨 디자인")]
        public static void Open()
        {
            var window = GetWindow<UnitMotionTuningWindow>();
            window.titleContent = new GUIContent("레벨 디자인");
            window.minSize = new Vector2(510f, 400f);
        }

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _assets.Clear();
            _readFields.Clear();
            _writeFields.Clear();
            _ranges.Clear();

            var settings = AssetDatabase.LoadAssetAtPath<ViewSettingsSO>(DataPath + "View/ViewSettings.asset");
            _factory = AssetDatabase.LoadAssetAtPath<MatchFactory>(DataPath + "MatchFactory.asset");
            if (settings == null || _factory == null)
            {
                rootVisualElement.Add(new HelpBox("ViewSettings 또는 MatchFactory 에셋을 찾을 수 없습니다.", HelpBoxMessageType.Error));
                return;
            }

            using var factoryData = new SerializedObject(_factory);
            var typeAsset = factoryData.FindProperty("_types").objectReferenceValue;
            var types = typeAsset != null ? Track(typeAsset) : null;
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;
            AddRules(scroll, factoryData.FindProperty("_rules").objectReferenceValue);
            AddContamination(scroll, factoryData.FindProperty("_spawnTable").objectReferenceValue, types);
            AddWorkerStats(scroll, factoryData.FindProperty("_workerStats"), types);
            AddMotion(scroll, Track(settings));
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

        private void AddRules(VisualElement parent, UnityEngine.Object asset)
        {
            var section = AddSection(parent, "판 규칙", true);
            if (asset == null) { AddMissingAsset(section, "GameRule"); return; }
            var rules = Track(asset);
            AddFloat(section, rules.FindProperty("_matchSeconds"), "제한 시간 (초)", min: 0.01f);
            AddFloat(section, rules.FindProperty("_zoneThreshold"), "구역 성공 기준 (%)", max: 100f);
            AddFloat(section, rules.FindProperty("_facilityThreshold"), "시설 성공 기준 (%)", max: 100f);
            AddFloat(section, rules.FindProperty("_interactionDistance"), "상호작용 거리 (m)", min: 0.01f);
        }

        private void AddContamination(VisualElement parent, UnityEngine.Object spawnAsset, SerializedObject types)
        {
            var section = AddSection(parent, "오염");
            section.tooltip = "플레이 중 초기 오염 개수, 오염량 범위 또는 가중치를 저장하면 같은 시설에서 새 판을 시작합니다.";
            if (spawnAsset == null) { AddMissingAsset(section, "ZoneSpawnTable"); }
            else
            {
                var spawn = Track(spawnAsset);
                AddRange(section, "시설 초기 오염량", spawn.FindProperty("_minimumFacilityWeight"),
                    spawn.FindProperty("_maximumFacilityWeight"), min: 1);

                var ranges = spawn.FindProperty("_ranges");
                var byZone = ArrayItems(ranges).ToLookup(row => (EZoneId)row.FindPropertyRelative("_zone").intValue);
                foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
                {
                    var room = AddSection(section, ZoneDisplayName.GetFullName(zone));
                    foreach (var row in byZone[zone].OrderBy(row => row.FindPropertyRelative("_type").intValue))
                    {
                        var type = (EContaminationType)row.FindPropertyRelative("_type").intValue;
                        string typeName = GetContaminationName(type);
                        AddRange(room, typeName, row.FindPropertyRelative("_minimum"),
                            row.FindPropertyRelative("_maximum"), description: $"판 시작 시 이 구역에 배치할 {typeName}");
                    }
                }
            }

            var weights = AddSection(section, "오염 유형별 가중치");
            if (types == null) { AddMissingAsset(weights, "ContaminationType"); return; }
            foreach (var row in ArrayItems(types.FindProperty("_entries")))
            {
                var type = (EContaminationType)row.FindPropertyRelative("_type").intValue;
                AddInt(weights, row.FindPropertyRelative("_weight"), GetContaminationName(type), min: 1,
                    tooltip: "이 유형의 오염 하나가 청결도와 시설 초기 오염량에 반영되는 비중입니다.");
            }
        }

        private void AddWorkerStats(VisualElement parent, SerializedProperty workerStats, SerializedObject types)
        {
            var section = AddSection(parent, "작업자");
            var stats = ArrayItems(workerStats).Select(row => row.objectReferenceValue as WorkerStatsSO)
                .Where(asset => asset != null).OrderBy(asset => asset.Worker);
            foreach (var asset in stats)
            {
                var worker = AddSection(section, GetWorkerName(asset.Worker));
                var data = Track(asset);
                if (asset.Worker == EWorkerType.Player && types != null)
                {
                    AddFloat(worker, types.FindProperty("_playerMopSeconds"), "물걸레질 (초)", min: 0.01f);
                }
                if (asset.CanCarryHeavyDebris)
                {
                    AddFloat(worker, data.FindProperty("_heavyCarryMultiplier"), "무거운 잔해 운반 (배)");
                }
                if (asset.Worker != EWorkerType.Player && asset.CanCarryClutter)
                {
                    AddFloat(worker, data.FindProperty("_clutterCarryMultiplier"), "작은 물건 운반 (배)");
                }
                if (asset.Worker != EWorkerType.Player && asset.CanMop)
                {
                    AddFloat(worker, data.FindProperty("_mopSeconds"), "물걸레질 (초)", min: 0.01f);
                }
                if (asset.CanDigestWaste)
                {
                    AddFloat(worker, data.FindProperty("_digestSeconds"), "폐기물 소화 (초)", min: 0.01f);
                }
                if (asset.Worker == EWorkerType.Hellhound)
                {
                    AddFloat(worker, data.FindProperty("_dispatchSeconds"), "배출 왕복 (초)", min: 0.01f);
                }
            }
        }

        private void AddMotion(VisualElement parent, SerializedObject settings)
        {
            var section = AddSection(parent, "이동·애니메이션", true);
            AddRate(section, settings.FindProperty("_globalMoveRate"), "전체 이동 속도 (배)");
            AddRate(section, settings.FindProperty("_globalAnimationRate"), "전체 애니메이션 배속 (배)");
            AddFloat(section, settings.FindProperty("_playerTurnSpeed"), "플레이어 회전 (도/초)", min: 1f);
            AddFloat(section, settings.FindProperty("_stairApproachRadius"), "계단 접근 거리 (m)", min: 0.1f);
            AddFloat(section, settings.FindProperty("_stairTransitSeconds"), "계단 이동 시간 (초)");

            var bootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath)?.GetComponent<MatchBootstrap>();
            if (bootstrap == null)
            {
                section.Add(new HelpBox("SYS_Bootstrap 프리팹을 찾을 수 없습니다.", HelpBoxMessageType.Error));
                return;
            }

            using var bootstrapData = new SerializedObject(bootstrap);
            AddUnit(section, settings, EWorkerType.Player, bootstrapData.FindProperty("_playerPrefab").objectReferenceValue as Component);
            var minions = ArrayItems(bootstrapData.FindProperty("_minionPrefabs"))
                .Select(row => row.objectReferenceValue as MinionAgent).Where(prefab => prefab != null).OrderBy(prefab => prefab.Worker);
            foreach (var minion in minions)
            {
                AddUnit(section, settings, minion.Worker, minion);
            }
        }

        private void AddUnit(VisualElement parent, SerializedObject settings, EWorkerType worker, Component prefab)
        {
            string path = worker switch
            {
                EWorkerType.Player => "_playerMotion",
                EWorkerType.Goblin => "_goblinMotion",
                EWorkerType.Orc => "_orcMotion",
                EWorkerType.Skeleton => "_skeletonMotion",
                EWorkerType.Slime => "_slimeMotion",
                EWorkerType.Hellhound => "_hellhoundMotion",
                _ => null
            };
            if (path == null || prefab == null) { return; }

            var group = settings.FindProperty(path);
            RuntimeAnimatorController controller = prefab.GetComponentInChildren<ArtSlot>(true)?.Animator?.runtimeAnimatorController;
            bool HasMotion(ECharacterAnimation state) => EmbeddedCharacterAnimationBinder.GetMotion(controller, state) != null;
            string title = GetWorkerName(worker);
            var section = AddSection(parent, title, worker == EWorkerType.Player);
            var movement = AddSection(section, "이동 속도 (배)", true);
            bool canWalk = worker != EWorkerType.Hellhound;
            bool canRun = worker != EWorkerType.Slime;
            bool isMissingSkeletonClips = worker == EWorkerType.Skeleton && !HasMotion(ECharacterAnimation.Idle);
            if (canWalk) { AddMovement(worker == EWorkerType.Slime ? "이동" : "걷기", "_walkRate"); }
            if (canRun) { AddMovement("달리기", "_runRate"); }
            if (HasMotion(ECharacterAnimation.Carry)) { AddMovement("한 손 운반", "_carryRate"); }
            if (HasMotion(ECharacterAnimation.CarryHeavy)) { AddMovement("두 손 운반", "_heavyCarryRate"); }
            if (HasMotion(ECharacterAnimation.MopWalk) || isMissingSkeletonClips) { AddMovement("걸레 이동", "_mopWalkRate"); }
            if (controller == null && !isMissingSkeletonClips) { return; }

            string animationTitle = isMissingSkeletonClips ? "애니메이션 배속 (FBX 연결 필요)" : "애니메이션 배속 (배)";
            var animation = AddSection(section, animationTitle, true);
            if (HasMotion(ECharacterAnimation.Idle) || isMissingSkeletonClips) { AddAnimation("대기", "_idleAnimationRate"); }
            if (canWalk && (HasMotion(ECharacterAnimation.Walk) || isMissingSkeletonClips))
            {
                AddAnimation(worker == EWorkerType.Slime ? "이동" : "걷기", "_walkAnimationRate");
            }
            if (canRun && (HasMotion(ECharacterAnimation.Run) || isMissingSkeletonClips)) { AddAnimation("달리기", "_runAnimationRate"); }
            if (HasMotion(ECharacterAnimation.Carry)) { AddAnimation("한 손 운반", "_carryAnimationRate"); }
            if (HasMotion(ECharacterAnimation.CarryHeavy)) { AddAnimation("두 손 운반", "_heavyCarryAnimationRate"); }
            if (HasMotion(ECharacterAnimation.MopWalk) || isMissingSkeletonClips) { AddAnimation("걸레 이동", "_mopWalkAnimationRate"); }
            if (HasMotion(ECharacterAnimation.Mop) || isMissingSkeletonClips) { AddAnimation("물걸레질", "_mopAnimationRate"); }
            if (HasMotion(ECharacterAnimation.PickUp) || HasMotion(ECharacterAnimation.PickUpHeavy) || isMissingSkeletonClips)
            {
                AddAnimation("집기", "_pickupRate");
                AddAnimation("내려놓기", "_dropRate");
            }
            if (HasMotion(ECharacterAnimation.Eat)) { AddAnimation("먹기", "_eatAnimationRate"); }
            if (HasMotion(ECharacterAnimation.Dispatch)) { AddAnimation("배출 이동", "_dispatchAnimationRate"); }

            void AddMovement(string label, string property)
            {
                Add(movement, label, property, $"{title}의 {label} 속도 배율입니다. 1은 기본 속도입니다.");
            }

            void AddAnimation(string label, string property)
            {
                Add(animation, label, property, $"{title}의 {label} 애니메이션 재생 배율입니다. 1은 기본 배속입니다.");
            }

            void Add(VisualElement container, string label, string property, string tooltip)
            {
                AddRate(container, group.FindPropertyRelative(property), label, tooltip).labelElement.style.minWidth = 100;
            }
        }

        private void Save()
        {
            foreach (var (minimum, maximum, label) in _ranges)
            {
                if (minimum.value <= maximum.value) { continue; }
                _status.text = $"{label}의 최소값이 최대값보다 큽니다.";
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("레벨 디자인 수치 저장");
            _spawnSettingsEdited = false;
            foreach (var asset in _assets) { asset.Update(); }
            foreach (var write in _writeFields) { write(); }
            var changed = new List<UnityEngine.Object>();
            foreach (var asset in _assets)
            {
                if (asset.ApplyModifiedProperties()) { changed.Add(asset.targetObject); }
            }

            MatchBootstrap bootstrap = FindLiveMatch();
            bool restart = bootstrap != null && _spawnSettingsEdited && changed.Count != 0;
            if (restart)
            {
                using var live = new SerializedObject(bootstrap);
                try
                {
                    _factory.ValidateInitialContamination(bootstrap.Layout, live.FindProperty("_seed").intValue);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException)
                {
                    _isRollingBack = true;
                    try { Undo.RevertAllDownToGroup(undoGroup); }
                    finally { _isRollingBack = false; }
                    _status.text = error.GetBaseException().Message;
                    return;
                }
            }

            foreach (var asset in changed) { AssetDatabase.SaveAssetIfDirty(asset); }
            Undo.CollapseUndoOperations(undoGroup);
            Reload();
            if (restart)
            {
                EditorSceneManager.LoadSceneAsyncInPlayMode(bootstrap.gameObject.scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                _status.text = "저장 완료 · 초기 오염을 반영해 새 판 시작";
                return;
            }
            if (changed.Count > 0) { bootstrap?.RefreshLevelDesign(); }
            _status.text = bootstrap != null ? "저장 완료 · 플레이에 반영" : "저장 완료";
        }

        private void OnUndoRedo()
        {
            if (_isRollingBack) { return; }
            Reload();
            FindLiveMatch()?.RefreshLevelDesign();
        }

        private MatchBootstrap FindLiveMatch()
        {
            if (!Application.isPlaying || _factory == null) { return null; }
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<MatchBootstrap>();
            if (bootstrap == null || bootstrap.Session == null) { return null; }
            using var data = new SerializedObject(bootstrap);
            return data.FindProperty("_factory").objectReferenceValue == _factory ? bootstrap : null;
        }

        private void Reload()
        {
            if (_status == null) { return; }
            foreach (var asset in _assets) { asset.Update(); }
            foreach (var read in _readFields) { read(); }
            _status.text = string.Empty;
        }

        private SerializedObject Track(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            _assets.Add(serialized);
            return serialized;
        }

        private static Foldout AddSection(VisualElement parent, string title, bool expanded = false)
        {
            var section = new Foldout { text = title, value = expanded };
            section.style.marginTop = 6;
            parent.Add(section);
            return section;
        }

        private static void AddMissingAsset(VisualElement parent, string name)
        {
            parent.Add(new HelpBox($"MatchFactory에 {name} 에셋이 연결되지 않았습니다.", HelpBoxMessageType.Error));
        }

        private static IEnumerable<SerializedProperty> ArrayItems(SerializedProperty array)
        {
            for (int i = 0; i < array.arraySize; i++) { yield return array.GetArrayElementAtIndex(i); }
        }

        private void AddFloat(VisualElement parent, SerializedProperty property, string title,
            float min = 0f, float max = float.MaxValue)
        {
            AddNumber(parent, property, new FloatField(title), p => p.floatValue,
                (p, value) => p.floatValue = value, value => float.IsNaN(value) ? min : Mathf.Clamp(value, min, max));
        }

        private FloatField AddRate(VisualElement parent, SerializedProperty property, string title, string tooltip = null)
        {
            var field = new FloatField(title);
            AddNumber(parent, property, field, p => p.floatValue, (p, value) => p.floatValue = value,
                UnitMotionTuning.NormalizeRate, tooltip);
            return field;
        }

        private IntegerField AddInt(VisualElement parent, SerializedProperty property, string title, int min, string tooltip = null)
        {
            var field = new IntegerField(title);
            AddNumber(parent, property, field, p => p.intValue,
                (p, value) => p.intValue = value, value => Mathf.Max(min, value), tooltip);
            return field;
        }

        private void AddRange(VisualElement parent, string title, SerializedProperty minimum,
            SerializedProperty maximum, int min = 0, string description = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var label = new Label(title);
            label.tooltip = description == null ? $"{minimum.tooltip}\n{maximum.tooltip}" : $"{description} 개수의 범위입니다.";
            label.style.width = 140;
            label.style.alignSelf = Align.Center;
            row.Add(label);
            var minField = AddInt(row, minimum, "최소", min, description == null ? null : $"{description} 최소 개수입니다.");
            var maxField = AddInt(row, maximum, "최대", min, description == null ? null : $"{description} 최대 개수입니다.");
            foreach (var field in new[] { minField, maxField })
            {
                field.style.flexGrow = 1;
                field.labelElement.style.minWidth = 30;
            }
            _ranges.Add((minField, maxField, title));
            parent.Add(row);
        }

        private void AddNumber<T>(VisualElement parent, SerializedProperty property, BaseField<T> field,
            Func<SerializedProperty, T> read, Action<SerializedProperty, T> write, Func<T, T> normalize, string tooltip = null)
        {
            var asset = property.serializedObject;
            string path = property.propertyPath;
            bool affectsInitialContamination = asset.targetObject is ZoneSpawnTableSO
                || (asset.targetObject is ContaminationTypeSO && path.StartsWith("_entries.", StringComparison.Ordinal));
            T initial = default;
            field.name = asset.targetObject.name + "." + path;
            field.tooltip = tooltip ?? property.tooltip;
            _readFields.Add(() =>
            {
                initial = normalize(read(asset.FindProperty(path)));
                field.SetValueWithoutNotify(initial);
            });
            _writeFields.Add(() =>
            {
                T value = normalize(field.value);
                if (EqualityComparer<T>.Default.Equals(value, initial)) { return; }
                var current = asset.FindProperty(path);
                if (EqualityComparer<T>.Default.Equals(value, read(current))) { return; }
                write(current, value);
                _spawnSettingsEdited |= affectsInitialContamination;
            });
            parent.Add(field);
        }

        private static string GetContaminationName(EContaminationType type) => type switch
        {
            EContaminationType.HeavyDebris => "무거운 잔해",
            EContaminationType.Trash => "쓰레기",
            EContaminationType.ClutterItem => "작은 물건",
            EContaminationType.FloorStain => "바닥 얼룩",
            _ => type.ToString()
        };

        private static string GetWorkerName(EWorkerType worker) => worker switch
        {
            EWorkerType.Player => "플레이어",
            EWorkerType.Goblin => "고블린",
            EWorkerType.Orc => "오크",
            EWorkerType.Skeleton => "스켈레톤",
            EWorkerType.Slime => "슬라임",
            EWorkerType.Hellhound => "헬하운드",
            _ => worker.ToString()
        };
    }
}
