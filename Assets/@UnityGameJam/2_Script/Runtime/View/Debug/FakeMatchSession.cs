using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>가짜 판 디버그 값. 실제 규칙 값은 A 레인의 SO에 있다.</summary>
    public readonly struct FakeMatchSettings
    {
        public float MatchSeconds { get; }
        public float BaseMoveSpeed { get; }
        public float HeavyCarryMultiplier { get; }
        public float MopSeconds { get; }
        public int ItemsPerTypePerZone { get; }
        public float MinionMoveSpeed { get; }
        public float HellhoundAwaySeconds { get; }

        public FakeMatchSettings(float matchSeconds, float baseMoveSpeed, float heavyCarryMultiplier, float mopSeconds, int itemsPerTypePerZone,
            float minionMoveSpeed = 3.5f, float hellhoundAwaySeconds = 3f)
        {
            MatchSeconds = matchSeconds;
            BaseMoveSpeed = baseMoveSpeed;
            HeavyCarryMultiplier = heavyCarryMultiplier;
            MopSeconds = mopSeconds;
            ItemsPerTypePerZone = itemsPerTypePerZone;
            MinionMoveSpeed = minionMoveSpeed;
            HellhoundAwaySeconds = hellhoundAwaySeconds;
        }
    }

    /// <summary>
    /// B 레인이 규칙(A 레인) 구현 전에 조작과 화면을 만들 수 있게 하는 가짜 판.
    /// 계약(IMatchSession)만 흉내 낸다. 졸개는 배정받은 구역의 순찰 첫 점까지 이동만 하고(판단·작업 없음),
    /// 헬하운드는 배출 구역에 갔다가 잠시 뒤 돌아온다. 제안은 만들지 않는다. 실제 게임에서는 쓰지 않는다.
    /// </summary>
    public sealed class FakeMatchSession : IMatchSession
    {
        private const float HomeSlotReach = 2f;

        private sealed class FakeItem
        {
            public int Id;
            public EContaminationType Type;
            public EItemState State;
            public Vector3 Position;
            public EZoneId Zone;
            public EWorkerType? Carrier;
            public int? HomeSlotId;

            public ItemSnapshot ToSnapshot()
            {
                return new ItemSnapshot(Id, Type, State, Position, Carrier, HomeSlotId);
            }
        }

        private static readonly Dictionary<EContaminationType, float> _weights = new Dictionary<EContaminationType, float>
        {
            { EContaminationType.HeavyDebris, 4f },
            { EContaminationType.Trash, 3f },
            { EContaminationType.ClutterItem, 2f },
            { EContaminationType.FloorStain, 1f },
        };

        private readonly FacilityLayout _layout;
        private readonly IWorkerBody _player;
        private readonly FakeMatchSettings _settings;
        private readonly List<FakeItem> _items = new List<FakeItem>();
        private readonly List<ItemSnapshot> _snapshots = new List<ItemSnapshot>();
        private readonly List<MinionStatus> _minions = new List<MinionStatus>();
        private readonly List<MessageEntry> _messages = new List<MessageEntry>();
        private readonly Dictionary<EZoneId, float> _startWeights = new Dictionary<EZoneId, float>();
        private readonly Dictionary<EWorkerType, IMinionBody> _bodies;
        private float _facilityStartWeight;
        private int? _carriedItemId;
        private int? _mopTargetId;
        private float _hellhoundAwayTimer = -1f;

        public FakeMatchSession(MatchSetup setup, FakeMatchSettings settings)
        {
            _layout = setup.Layout;
            _player = setup.PlayerBody;
            _settings = settings;
            _bodies = setup.MinionBodies;
            foreach (IMinionBody body in _bodies.Values) { body.SetMoveSpeed(settings.MinionMoveSpeed); }
            CreateItems();
            foreach (EWorkerType minion in new[] { EWorkerType.Goblin, EWorkerType.Orc, EWorkerType.Skeleton, EWorkerType.Slime, EWorkerType.Hellhound })
            {
                _minions.Add(new MinionStatus(minion, EMinionActivity.LobbyIdle, null, null, false));
            }
            RefreshSnapshots();
        }

        // ---------- IMatchReadModel ----------

        public EMatchState MatchState { get; private set; } = EMatchState.Ready;
        public float RemainingTime { get; private set; }
        public float ZoneThreshold => 60f;
        public float FacilityThreshold => 85f;
        public float FacilityCleanliness => ToPercent(_items.Where(IsCounted).Sum(i => _weights[i.Type]), _facilityStartWeight);
        public EZoneId? PlayerZone => FacilityLayoutBuilder.TryFindZone(_layout, _player.Position, out EZoneId zone) ? zone : (EZoneId?)null;
        public int? PlayerCarriedItemId => _carriedItemId;
        public bool IsPlayerHoldingMop { get; private set; }
        public float PlayerMoveSpeed => IsCarrying(EContaminationType.HeavyDebris) ? _settings.BaseMoveSpeed * _settings.HeavyCarryMultiplier : _settings.BaseMoveSpeed;
        public bool IsPlayerWorking => _mopTargetId.HasValue;
        public float PlayerWorkProgress { get; private set; }
        public IReadOnlyList<MinionStatus> Minions => _minions;
        public IReadOnlyList<MessageEntry> ActiveMessages => _messages;
        public IReadOnlyList<ItemSnapshot> Items => _snapshots;
        public MatchResult Result { get; private set; }

        public event Action<ItemSnapshot> ItemChanged;
        // 가짜 판은 제안·통보를 만들지 않는다.
        public event Action MessagesChanged { add { } remove { } }
        public event Action<EMatchState> MatchStateChanged;

        public float GetZoneCleanliness(EZoneId zone)
        {
            _startWeights.TryGetValue(zone, out float start);
            return ToPercent(_items.Where(i => IsCounted(i) && i.Zone == zone).Sum(i => _weights[i.Type]), start);
        }

        // ---------- IMatchSession ----------

        public void StartMatch()
        {
            _startWeights.Clear();
            foreach (IGrouping<EZoneId, FakeItem> group in _items.GroupBy(i => i.Zone)) { _startWeights[group.Key] = group.Sum(i => _weights[i.Type]); }
            _facilityStartWeight = _items.Sum(i => _weights[i.Type]);
            RemainingTime = _settings.MatchSeconds;
            SetState(EMatchState.Running);
        }

        public void Tick(float deltaTime)
        {
            if (MatchState != EMatchState.Running) { return; }

            if (_mopTargetId.HasValue)
            {
                PlayerWorkProgress = Mathf.Min(100f, PlayerWorkProgress + deltaTime / _settings.MopSeconds * 100f);
                if (PlayerWorkProgress >= 100f - 0.001f)
                {
                    FakeItem stain = Find(_mopTargetId.Value);
                    _mopTargetId = null;
                    PlayerWorkProgress = 0f;
                    SetItemState(stain, EItemState.Resolved);
                }
            }

            UpdateMinions(deltaTime);

            RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
            if (RemainingTime <= 0f) { EndMatch(); }
        }

        // ---------- IMatchCommands ----------

        public bool BeginTalk(EWorkerType minion)
        {
            return CanAct();
        }

        public void CancelTalk(EWorkerType minion) { }

        public bool AssignRoom(EWorkerType minion, EZoneId zone)
        {
            int index = _minions.FindIndex(m => m.Minion == minion);
            if (!CanAct() || index < 0 || minion == EWorkerType.Hellhound) { return false; }
            _minions[index] = new MinionStatus(minion, EMinionActivity.MovingToRoom, zone, null, false);

            PatrolRoute route = _layout.PatrolRoutes.FirstOrDefault(r => r.Zone == zone);
            if (_bodies.TryGetValue(minion, out IMinionBody body) && route != null && route.Points.Count > 0)
            {
                body.MoveTo(ZoneDisplayName.GetFloor(zone), route.Points[0]);
            }
            return true;
        }

        public bool ApproveOldestProposal()
        {
            return false;
        }

        public bool HoldOldestProposal()
        {
            return false;
        }

        public bool DispatchHellhound()
        {
            if (!CanAct() || GetStatus(EWorkerType.Hellhound).Activity != EMinionActivity.LobbyIdle) { return false; }
            List<FakeItem> batch = _items.Where(i => i.State == EItemState.InDischargeArea).ToList();
            if (batch.Count == 0) { return false; }
            if (_bodies.TryGetValue(EWorkerType.Hellhound, out IMinionBody body) && _layout.StartPositions.ContainsKey(EWorkerType.Hellhound))
            {
                _hellhoundAwayTimer = -1f;
                SetActivity(EWorkerType.Hellhound, EMinionActivity.Dispatching);
                Bounds exit = _layout.DischargeArea;
                body.MoveTo(EFloor.Floor1, new Vector3(exit.center.x, exit.min.y, exit.center.z));
            }
            foreach (FakeItem item in batch) { SetItemState(item, EItemState.Dispatched); }
            return true;
        }

        public bool TryPickUp(int itemId)
        {
            FakeItem item = Find(itemId);
            bool isOnGround = item != null && (item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea);
            if (!CanAct() || !isOnGround || item.Type == EContaminationType.FloorStain || _carriedItemId.HasValue || IsPlayerHoldingMop || IsPlayerWorking) { return false; }

            item.Carrier = EWorkerType.Player;
            _carriedItemId = itemId;
            SetItemState(item, EItemState.Carried);
            return true;
        }

        public bool TryDrop()
        {
            if (!CanAct() || !_carriedItemId.HasValue) { return false; }
            DropCarried();
            return true;
        }

        public bool TryPlaceAtHome()
        {
            FakeItem item = _carriedItemId.HasValue ? Find(_carriedItemId.Value) : null;
            if (!CanAct() || item == null || item.Type != EContaminationType.ClutterItem) { return false; }
            bool isNearSlot = _layout.HomeSlots.Any(s => s.SlotId == item.HomeSlotId && DistanceXZ(s.Position, _player.Position) <= HomeSlotReach);
            if (!isNearSlot) { return false; }

            item.Carrier = null;
            _carriedItemId = null;
            SetItemState(item, EItemState.Resolved);
            return true;
        }

        public bool TryStartMop(int itemId)
        {
            FakeItem item = Find(itemId);
            if (!CanAct() || !IsPlayerHoldingMop || IsPlayerWorking || item == null || item.Type != EContaminationType.FloorStain || item.State != EItemState.OnFloor) { return false; }
            _mopTargetId = itemId;
            PlayerWorkProgress = 0f;
            return true;
        }

        public void CancelWork()
        {
            _mopTargetId = null;
            PlayerWorkProgress = 0f;
        }

        public bool TryBorrowMop(EFloor floor)
        {
            if (!CanAct() || IsPlayerHoldingMop || _carriedItemId.HasValue) { return false; }
            IsPlayerHoldingMop = true;
            return true;
        }

        public bool TryReturnMop()
        {
            if (!CanAct() || !IsPlayerHoldingMop || IsPlayerWorking) { return false; }
            IsPlayerHoldingMop = false;
            return true;
        }

        // ---------- 내부 ----------

        private void CreateItems()
        {
            int id = 0;
            foreach (IGrouping<(EZoneId, EContaminationType), SpawnPoint> group in _layout.SpawnPoints.GroupBy(s => (s.Zone, s.Type)))
            {
                Queue<HomeSlot> slots = group.Key.Item2 == EContaminationType.ClutterItem
                    ? new Queue<HomeSlot>(_layout.HomeSlots.Where(s => s.Zone == group.Key.Item1))
                    : null;
                foreach (SpawnPoint spawn in group.Take(_settings.ItemsPerTypePerZone))
                {
                    _items.Add(new FakeItem { Id = id++, Type = spawn.Type, State = EItemState.OnFloor, Position = spawn.Position, Zone = spawn.Zone,
                        HomeSlotId = slots != null && slots.Count > 0 ? slots.Dequeue().SlotId : (int?)null });
                }
            }
        }

        private void DropCarried()
        {
            FakeItem item = Find(_carriedItemId.Value);
            item.Carrier = null;
            item.Position = _player.Position;
            item.Zone = ResolveDropZone(_player.Position);
            _carriedItemId = null;
            bool isInDischarge = FacilityLayoutValidator.ContainsXZ(_layout.DischargeArea, item.Position);
            SetItemState(item, isInDischarge ? EItemState.InDischargeArea : EItemState.OnFloor);
        }

        /// <summary>구역 밖(화장실·계단)이면 그 층 로비로 귀속한다(U8).</summary>
        private EZoneId ResolveDropZone(Vector3 position)
        {
            if (FacilityLayoutBuilder.TryFindZone(_layout, position, out EZoneId zone)) { return zone; }
            return _player.Floor switch
            {
                EFloor.Floor2 => EZoneId.Floor2Lobby,
                EFloor.Floor3 => EZoneId.Floor3Lobby,
                _ => EZoneId.Floor1Lobby,
            };
        }

        private void EndMatch()
        {
            SetState(EMatchState.DemonKingArrived);
            CancelWork();
            if (_carriedItemId.HasValue) { DropCarried(); }

            var zones = new Dictionary<EZoneId, float>();
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId))) { zones[zone] = GetZoneCleanliness(zone); }
            bool isSuccess = zones.Values.All(v => v >= ZoneThreshold) && FacilityCleanliness >= FacilityThreshold;
            Result = new MatchResult(isSuccess, FacilityCleanliness, zones);
            SetState(EMatchState.Resolved);
        }

        private bool CanAct()
        {
            return MatchState == EMatchState.Running;
        }

        /// <summary>몸이 도착하면 다음 상태로 넘긴다. 헬하운드는 배출 구역에서 잠시 머문 뒤 대기 위치로 돌아온다.</summary>
        private void UpdateMinions(float deltaTime)
        {
            foreach (MinionStatus status in _minions.ToList())
            {
                if (!_bodies.TryGetValue(status.Minion, out IMinionBody body) || !body.HasArrived) { continue; }
                switch (status.Activity)
                {
                    case EMinionActivity.MovingToRoom:
                        SetActivity(status.Minion, EMinionActivity.Working);
                        break;
                    case EMinionActivity.Dispatching:
                        if (_hellhoundAwayTimer < 0f) { _hellhoundAwayTimer = _settings.HellhoundAwaySeconds; }
                        _hellhoundAwayTimer -= deltaTime;
                        if (_hellhoundAwayTimer <= 0f && _layout.StartPositions.TryGetValue(status.Minion, out Vector3 post))
                        {
                            SetActivity(status.Minion, EMinionActivity.ReturningToLobby);
                            body.MoveTo(EFloor.Floor1, post);
                        }
                        break;
                    case EMinionActivity.ReturningToLobby:
                        SetActivity(status.Minion, EMinionActivity.LobbyIdle);
                        break;
                }
            }
        }

        private MinionStatus GetStatus(EWorkerType minion)
        {
            return _minions.FirstOrDefault(m => m.Minion == minion);
        }

        private void SetActivity(EWorkerType minion, EMinionActivity activity)
        {
            int index = _minions.FindIndex(m => m.Minion == minion);
            if (index < 0) { return; }
            MinionStatus old = _minions[index];
            _minions[index] = new MinionStatus(minion, activity, old.AssignedZone, old.CarriedItemId, old.IsHoldingMop);
        }

        private bool IsCarrying(EContaminationType type)
        {
            return _carriedItemId.HasValue && Find(_carriedItemId.Value).Type == type;
        }

        private static bool IsCounted(FakeItem item)
        {
            return item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea;
        }

        private FakeItem Find(int id)
        {
            return _items.FirstOrDefault(i => i.Id == id);
        }

        private void SetItemState(FakeItem item, EItemState state)
        {
            item.State = state;
            RefreshSnapshots();
            ItemChanged?.Invoke(item.ToSnapshot());
        }

        private void SetState(EMatchState state)
        {
            MatchState = state;
            MatchStateChanged?.Invoke(state);
        }

        private void RefreshSnapshots()
        {
            _snapshots.Clear();
            foreach (FakeItem item in _items) { _snapshots.Add(item.ToSnapshot()); }
        }

        private static float ToPercent(float current, float start)
        {
            if (start <= 0f) { return 100f; }
            return Mathf.Clamp(100f * (1f - current / start), 0f, 100f);
        }

        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }
    }
}
