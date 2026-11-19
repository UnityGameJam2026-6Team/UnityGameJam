using System;
using System.Collections;
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
        private const int OpeningSeconds = 5;
        private const int WindowsFrameRateLimit = 60;
        private const float OpeningFadeSeconds = 0.2f;

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
        private int? _previousTargetFrameRate;
        private float _fastForwardRate;
        private bool _isCompletionChecked;
        private PlayerController _playerController;
        private PlayerInteractor _playerInteractor;

        public bool IsPaused { get; set; }
        public float? CompletionSeconds { get; private set; }
        public bool IsFastForwarding => CompletionSeconds.HasValue && Session.MatchState == EMatchState.Running;

        private readonly Dictionary<int, string> _homeSlotKinds = new Dictionary<int, string>();
        public IReadOnlyDictionary<int, string> HomeSlotKinds => _homeSlotKinds;
        public ViewSettingsSO ViewSettings => _viewSettings;
        public IMatchSession Session { get; private set; }
        internal int ItemRevision { get; private set; }
        public float MatchDuration => _factory.MatchDuration ?? _initialMatchTime;
        public FacilityLayout Layout { get; private set; }
        public PlayerBody Player { get; private set; }
        internal PlayerController PlayerController => _playerController;
        internal PlayerInteractor PlayerInteractor => _playerInteractor;
        public IReadOnlyList<MinionAgent> Minions => _minions;
        internal FloorCameraFollow ViewCamera => _camera;
        internal FloorAuthoring ArrivalFloor => Array.Find(_floors, floor => floor != null && floor.Floor == EFloor.Floor1);
        public int OpeningNumber { get; private set; }
        public float OpeningBeatProgress { get; private set; }

        internal bool CanSeeMinion(MinionAgent minion)
        {
            return minion != null && CanSeeAt(minion.Position, minion.Floor);
        }

        internal bool CanHearAt(Vector3 position, EFloor floor) => CanSeeAt(position, floor);

        internal bool CanSeeAt(Vector3 position, EFloor floor)
        {
            return Player != null && Player.Floor == floor
                && (_roomVisibility == null || !_roomVisibility.IsCovered(floor, position));
        }

        public void FocusConversation(EWorkerType? worker, Vector2 viewportY = default)
        {
            if (_camera == null) { return; }
            foreach (MinionAgent minion in _minions)
            {
                if (minion.Worker != worker) { continue; }
                _camera.SetConversationPartner(minion.transform, viewportY);
                return;
            }
            _camera.SetConversationPartner(null);
        }

        /// <summary>판이 만들어지고 시작된 직후. HUD 등 보기 전용 컴포넌트가 구독한다.</summary>
        public event Action<MatchBootstrap> MatchStarted;

        private IEnumerator Start()
        {
            var warnings = new List<string>();
            Layout = FacilityLayoutBuilder.Build(_floors, warnings);
            foreach (string warning in warnings) { Debug.LogWarning("[Bootstrap] " + warning, this); }

            if (!Layout.StartPositions.TryGetValue(EWorkerType.Player, out Vector3 start))
            {
                Debug.LogError("[Bootstrap] 플레이어 시작점이 없습니다.", this);
                yield break;
            }

            if (Application.platform == RuntimePlatform.WindowsPlayer && QualitySettings.vSyncCount == 0)
            {
                int target = Application.targetFrameRate;
                if (target <= 0 || target > WindowsFrameRateLimit)
                {
                    _previousTargetFrameRate = target;
                    Application.targetFrameRate = WindowsFrameRateLimit;
                }
            }

            foreach (FloorAuthoring floor in _floors)
            {
                if (floor == null) { continue; }
                foreach (HomeSlotAuthoring slot in floor.GetComponentsInChildren<HomeSlotAuthoring>(true))
                {
                    _homeSlotKinds[slot.SlotId] = slot.ItemKind;
                }
            }

            _camera?.SetScreenFade(1f);
            StairDirectory stairs = StairDirectory.FromFloors(_floors);
            Vector3 lineCenter = start - Vector3.forward * _viewSettings.OpeningLineDistance;
            Player = Instantiate(_playerPrefab, start, Quaternion.LookRotation(lineCenter - start));
            ApplyMotionTuning(Player, EWorkerType.Player);
            Player.Initialize(Layout.Floors);
            if (_roomCoverPrefab != null)
            {
                _roomVisibility = new RoomVisibility(Layout, _roomCoverPrefab, transform);
                _roomVisibility.RevealAt(Player.Floor, start);
            }

            var setup = new MatchSetup { Layout = Layout, Seed = _seed, PlayerBody = Player };
            yield return SpawnMinions(setup, stairs, lineCenter);
            Session = _factory.Create(setup);

            _playerController = Player.GetComponent<PlayerController>();
            _playerController.Initialize(Session, stairs, _viewSettings, _camera);
            var interactor = Player.GetComponent<PlayerInteractor>();
            _playerInteractor = interactor;
            interactor.Initialize(Session, Layout, _factory.InteractionDistance ?? _viewSettings.InteractRadius, _camera, stairs);
            foreach (MinionAgent minion in _minions)
            {
                var status = minion.GetComponent<MinionStatusPresenter>();
                status.Initialize(Session);
                interactor.RegisterMinion(minion, status);
            }
            if (_camera != null) { _camera.SetTarget(Player.transform); }

            Session.ItemChanged += OnItemChanged;
            if (TryGetComponent(out ContaminationView contaminationView) && contaminationView.isActiveAndEnabled)
            {
                yield return contaminationView.PrepareOpening();
            }

            Vector3 openingCenter = (start + lineCenter) * 0.5f;
            if (_camera != null)
            {
                // 컷에 처음 들어오는 지오메트리와 셰이더를 카운트다운 전에 그린다.
                for (int number = 1; number <= OpeningSeconds; number++)
                {
                    _camera.ShowOpeningShot(openingCenter, number, number == OpeningSeconds ? 0f : 1f);
                    yield return null;
                }
            }

            bool revealingOpening = _camera != null;
            for (float elapsed = 0f; elapsed < OpeningSeconds; elapsed += Mathf.Min(1f, Time.unscaledDeltaTime))
            {
                OpeningNumber = OpeningNumberAt(elapsed);
                OpeningBeatProgress = elapsed - Mathf.Floor(elapsed);
                _camera?.ShowOpeningShot(openingCenter, OpeningNumber, OpeningBeatProgress);
                if (revealingOpening)
                {
                    _camera.SetScreenFade(Mathf.SmoothStep(1f, 0f, elapsed / OpeningFadeSeconds));
                    revealingOpening = elapsed < OpeningFadeSeconds;
                }
                yield return null;
            }

            _camera?.SetScreenFade(0f);

            _camera?.EndOpening();
            OpeningNumber = 0;
            Session.StartMatch();
            _initialMatchTime = Session.RemainingTime;
            MatchStarted?.Invoke(this);
        }

        private void Update()
        {
            if (Session == null || IsPaused || Session.MatchState == EMatchState.Ready) { return; }
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
            ItemRevision++;
        }

        private void OnDestroy()
        {
            if (_previousTargetFrameRate.HasValue && Application.targetFrameRate == WindowsFrameRateLimit)
            {
                Application.targetFrameRate = _previousTargetFrameRate.Value;
            }

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
        private IEnumerator SpawnMinions(MatchSetup setup, StairDirectory stairs, Vector3 lineCenter)
        {
            foreach (MinionAgent prefab in _minionPrefabs)
            {
                if (prefab == null) { continue; }
                if (!Layout.StartPositions.TryGetValue(prefab.Worker, out Vector3 position))
                {
                    Debug.LogWarning($"[Bootstrap] {prefab.Worker} 시작점이 없습니다.", this);
                    continue;
                }
                Quaternion rotation = Quaternion.identity;
                if (TryGetOpeningMinionPosition(prefab.Worker, lineCenter, _viewSettings.OpeningMinionSpacing, out Vector3 openingPosition))
                {
                    position = openingPosition;
                    Vector3 facing = Vector3.ProjectOnPlane(Player.Position - position, Vector3.up);
                    rotation = Quaternion.LookRotation(facing);
                }
                MinionAgent minion = Instantiate(prefab, position, rotation);
                ApplyMotionTuning(minion, minion.Worker);
                minion.Initialize(stairs, Layout.Floors, _viewSettings.StairTransitSeconds);
                setup.MinionBodies[minion.Worker] = minion;
                _minions.Add(minion);
                yield return null;
            }
        }

        internal static int OpeningNumberAt(float elapsed) => Mathf.Clamp(OpeningSeconds - Mathf.FloorToInt(elapsed), 0, OpeningSeconds);

        internal static bool TryGetOpeningMinionPosition(EWorkerType worker, Vector3 lineCenter, float spacing, out Vector3 position)
        {
            int index = worker switch
            {
                EWorkerType.Goblin => 0,
                EWorkerType.Orc => 1,
                EWorkerType.Skeleton => 2,
                EWorkerType.Slime => 3,
                _ => -1
            };
            position = index < 0 ? default : lineCenter + Vector3.right * ((index - 1.5f) * spacing);
            return index >= 0;
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
