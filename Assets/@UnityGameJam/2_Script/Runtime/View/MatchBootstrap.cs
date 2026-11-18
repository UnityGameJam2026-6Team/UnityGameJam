using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.View
{
    /// <summary>
    /// 씬에서 판 하나를 조립하고 진행한다. 층 3개 → FacilityLayout → 팩토리로 판 생성 → 플레이어 생성 → 매 프레임 Tick.
    /// 팩토리는 계약 타입(MatchSessionFactoryBase)으로만 알기 때문에, 가짜 판에서 A 레인의 실제 판으로 바꿔도 이 코드는 그대로다.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [Header("판")]
        [Tooltip("M1 전까지 FakeMatchSessionFactory, 이후 A 레인의 실제 팩토리")]
        [SerializeField] private MatchSessionFactoryBase _factory;
        [SerializeField] private ViewSettingsSO _viewSettings;
        [SerializeField] private int _seed;

        [Header("씬 참조 (인스턴스에서 연결)")]
        [Tooltip("FL_Floor1~3 인스턴스")]
        [SerializeField] private FloorAuthoring[] _floors = Array.Empty<FloorAuthoring>();
        [SerializeField] private FloorCameraFollow _camera;

        [Header("생성")]
        [SerializeField] private PlayerBody _playerPrefab;
        [SerializeField] private GameObject _roomCoverPrefab;
        [Tooltip("CH_Goblin, CH_Orc, CH_Skeleton, CH_Slime, CH_Hellhound")]
        [SerializeField] private MinionAgent[] _minionPrefabs = Array.Empty<MinionAgent>();

        private readonly List<MinionAgent> _minions = new List<MinionAgent>();
        private RoomVisibility _roomVisibility;
        private float _initialMatchTime;
        private float _fastForwardRate;
        private bool _isCompletionChecked;
        private PlayerController _playerController;
        private PlayerInteractor _playerInteractor;

        public float? CompletionSeconds { get; private set; }
        public bool IsFastForwarding => CompletionSeconds.HasValue && Session.MatchState == EMatchState.Running;

        private readonly Dictionary<int, string> _homeSlotKinds = new Dictionary<int, string>();
        public IReadOnlyDictionary<int, string> HomeSlotKinds => _homeSlotKinds;
        public ViewSettingsSO ViewSettings => _viewSettings;
        public IMatchSession Session { get; private set; }
        public float MatchDuration => _factory.MatchDuration ?? _initialMatchTime;
        public FacilityLayout Layout { get; private set; }
        public PlayerBody Player { get; private set; }
        internal PlayerController PlayerController => _playerController;
        public IReadOnlyList<MinionAgent> Minions => _minions;

        internal bool CanSeeMinion(MinionAgent minion)
        {
            return Player != null && minion != null && minion.Floor == Player.Floor
                && (_roomVisibility == null || !_roomVisibility.IsCovered(minion.Floor, minion.Position));
        }

        internal bool CanHearAt(Vector3 position, EFloor floor)
        {
            return Player != null && Player.Floor == floor
                && (_roomVisibility == null || !_roomVisibility.IsCovered(floor, position));
        }

        public void FocusConversation(EWorkerType? worker)
        {
            if (_camera == null) { return; }
            foreach (MinionAgent minion in _minions)
            {
                if (minion.Worker != worker) { continue; }
                _camera.SetConversationPartner(minion.transform);
                return;
            }
            _camera.SetConversationPartner(null);
        }

        /// <summary>판이 만들어지고 시작된 직후. HUD 등 보기 전용 컴포넌트가 구독한다.</summary>
        public event Action<MatchBootstrap> MatchStarted;

        private void Start()
        {
            var warnings = new List<string>();
            Layout = FacilityLayoutBuilder.Build(_floors, warnings);
            foreach (string warning in warnings) { Debug.LogWarning("[Bootstrap] " + warning, this); }

            if (!Layout.StartPositions.TryGetValue(EWorkerType.Player, out Vector3 start))
            {
                Debug.LogError("[Bootstrap] 플레이어 시작점이 없습니다.", this);
                return;
            }

            foreach (FloorAuthoring floor in _floors)
            {
                if (floor == null) { continue; }
                foreach (HomeSlotAuthoring slot in floor.GetComponentsInChildren<HomeSlotAuthoring>(true))
                {
                    _homeSlotKinds[slot.SlotId] = slot.ItemKind;
                }
            }

            StairDirectory stairs = StairDirectory.FromFloors(_floors);
            Player = Instantiate(_playerPrefab, start, Quaternion.identity);
            ApplyMotionTuning(Player, EWorkerType.Player);
            Player.Initialize(Layout.Floors);
            if (_roomCoverPrefab != null)
            {
                _roomVisibility = new RoomVisibility(Layout, _roomCoverPrefab, transform);
                _roomVisibility.RevealAt(Player.Floor, start);
            }

            var setup = new MatchSetup { Layout = Layout, Seed = _seed, PlayerBody = Player };
            SpawnMinions(setup, stairs);
            Session = _factory.Create(setup);

            _playerController = Player.GetComponent<PlayerController>();
            _playerController.Initialize(Session, stairs, _viewSettings, _camera);
            var interactor = Player.GetComponent<PlayerInteractor>();
            _playerInteractor = interactor;
            interactor.Initialize(Session, Layout, _factory.InteractionDistance ?? _viewSettings.InteractRadius);
            foreach (MinionAgent minion in _minions)
            {
                interactor.RegisterMinion(minion.Worker, minion.transform);
                minion.GetComponent<MinionStatusPresenter>().Initialize(Session);
            }
            if (_camera != null) { _camera.SetTarget(Player.transform); }

            Session.ItemChanged += OnItemChanged;
            Session.StartMatch();
            _initialMatchTime = Session.RemainingTime;
            MatchStarted?.Invoke(this);
        }

        private void Update()
        {
            if (Session == null) { return; }
            if (IsFastForwarding)
            {
                Session.Tick(Time.unscaledDeltaTime * _fastForwardRate);
                return;
            }
            Session.Tick(Time.deltaTime);
            if (_isCompletionChecked || Session.MatchState != EMatchState.Running) { return; }
            _isCompletionChecked = true;
            if (!CompletionRules.IsFullyClean(Session)) { return; }

            CompletionSeconds = Mathf.Max(0f, MatchDuration - Session.RemainingTime);
            _fastForwardRate = Mathf.Max(1f, Session.RemainingTime / _viewSettings.CompletionCountdownSeconds);
            foreach (MinionStatus minion in Session.Minions)
            {
                if (minion.Activity == EMinionActivity.Talking) { Session.CancelTalk(minion.Minion); }
            }
            _playerController.enabled = false;
            _playerInteractor.enabled = false;
        }

        private void OnItemChanged(ItemSnapshot item)
        {
            _isCompletionChecked = false;
        }

        private void OnDestroy()
        {
            if (Session != null) { Session.ItemChanged -= OnItemChanged; }
            (Session as IDisposable)?.Dispose();
        }

        public void RefreshLevelDesign()
        {
            if (Session == null || Player == null) { return; }
            ApplyMotionTuning(Player, EWorkerType.Player);
            Player.GetComponent<PlayerInteractor>().SetInteractionDistance(_factory.InteractionDistance ?? _viewSettings.InteractRadius);
            foreach (MinionAgent minion in _minions)
            {
                ApplyMotionTuning(minion, minion.Worker);
                minion.SetStairTransitSeconds(_viewSettings.StairTransitSeconds);
            }
        }

        private void LateUpdate()
        {
            _roomVisibility?.RevealAt(Player.Floor, Player.Position);
        }

        /// <summary>졸개를 시작점(1층 로비, 헬하운드는 현관)에 만들고 규칙에 몸으로 넘긴다.</summary>
        private void SpawnMinions(MatchSetup setup, StairDirectory stairs)
        {
            foreach (MinionAgent prefab in _minionPrefabs)
            {
                if (prefab == null) { continue; }
                if (!Layout.StartPositions.TryGetValue(prefab.Worker, out Vector3 position))
                {
                    Debug.LogWarning($"[Bootstrap] {prefab.Worker} 시작점이 없습니다.", this);
                    continue;
                }
                MinionAgent minion = Instantiate(prefab, position, Quaternion.identity);
                ApplyMotionTuning(minion, minion.Worker);
                minion.Initialize(stairs, Layout.Floors, _viewSettings.StairTransitSeconds);
                setup.MinionBodies[minion.Worker] = minion;
                _minions.Add(minion);
            }
        }

        private void ApplyMotionTuning(Component unit, EWorkerType worker)
        {
            if (unit.TryGetComponent(out CharacterAnimatorDriver animator))
            {
                animator.SetMotionTuning(_viewSettings.GetMotionTuning(worker));
            }
        }
    }
}
