using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;
using UnityGameJam.Minion;
using UnityGameJam.Proposal;

namespace UnityGameJam.Match
{
    internal sealed class MatchSimulation : IMatchSession
    {
        private readonly MatchSetup _setup;
        private readonly GameRuleSO _rules;
        private readonly ContaminationTypeSO _types;
        private readonly ZoneMap _zoneMap;
        private readonly ContaminationRegistry _registry;
        private readonly CleanlinessCalculator _cleanliness;
        private readonly WorkerCapability _capability;
        private readonly WorkerStatsSO _playerStats;
        private readonly MopStationService _mops;
        private readonly DischargeService _discharge;
        private readonly IMinionBody _hellhound;
        private readonly float _halfDispatchSeconds;
        private EMinionActivity _hellhoundActivity = EMinionActivity.LobbyIdle;
        private Vector3 _hellhoundPost;
        private readonly SlimeDigestion _digestion;
        private readonly ProposalQueue _proposals;
        private readonly Dictionary<EWorkerType, MinionBrain> _minions = new Dictionary<EWorkerType, MinionBrain>();
        private EWorkerType? _talkingWith;
        private int? _mopTarget;
        private float _mopSeconds;
        private IReadOnlyList<ItemSnapshot> _itemSnapshots;

        public EMatchState MatchState { get; private set; }
        public float RemainingTime { get; private set; }
        public MatchResult Result { get; private set; }
        public float ZoneThreshold => _rules.ZoneThreshold;
        public float FacilityThreshold => _rules.FacilityThreshold;
        public float FacilityCleanliness => _cleanliness.GetFacilityCleanliness();
        public EZoneId? PlayerZone => _zoneMap.FindZone(Player.Position, Player.Floor);
        public int? PlayerCarriedItemId => GetCarriedItem()?.Id;
        public bool IsPlayerHoldingMop => _mops.GetBorrowedFrom(EWorkerType.Player) != null;
        public float PlayerMoveSpeed => _playerStats.MoveSpeed * (GetCarriedItem()?.Type == EContaminationType.HeavyDebris
            ? _playerStats.HeavyCarryMultiplier : 1f);
        public bool IsPlayerWorking => _mopTarget.HasValue;
        public float PlayerWorkProgress => IsPlayerWorking ? 100f * _mopSeconds / _types.PlayerMopSeconds : 0f;

        public IReadOnlyList<ItemSnapshot> Items => _itemSnapshots ??= Array.AsReadOnly(_registry.Items.Select(item => item.Snapshot).ToArray());
        public IReadOnlyList<MessageEntry> ActiveMessages => _proposals.Messages;

        public IReadOnlyList<MinionStatus> Minions
        {
            get
            {
                var statuses = _minions.Values.Select(minion => minion.Status).ToList();
                if (_hellhound != null)
                {
                    EMinionActivity activity = _hellhoundActivity == EMinionActivity.LobbyIdle && _discharge.IsDispatching
                        ? EMinionActivity.Dispatching : _hellhoundActivity;
                    statuses.Add(new MinionStatus(EWorkerType.Hellhound, activity, null, null, false));
                }
                return statuses;
            }
        }

        public event Action<ItemSnapshot> ItemChanged
        {
            add { _registry.ItemChanged += value; }
            remove { _registry.ItemChanged -= value; }
        }

        public event Action MessagesChanged
        {
            add { _proposals.Changed += value; }
            remove { _proposals.Changed -= value; }
        }

        public event Action<EMatchState> MatchStateChanged;

        private IWorkerBody Player => _setup.PlayerBody;
        private bool CanAct => MatchState == EMatchState.Running && RemainingTime > 0f;
        private float ElapsedTime => _rules.MatchSeconds - RemainingTime;

        public MatchSimulation(MatchSetup setup, GameRuleSO rules, ContaminationTypeSO types, WorkerStatsSO[] workerStats,
            IReadOnlyList<ContaminationItem> items, IProposalDialogueProvider dialogue = null)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _rules = rules != null ? rules : throw new ArgumentNullException(nameof(rules));
            _types = types != null ? types : throw new ArgumentNullException(nameof(types));
            if (setup.PlayerBody == null) { throw new ArgumentException("플레이어 몸이 필요합니다.", nameof(setup)); }
            if (!float.IsFinite(rules.MatchSeconds) || rules.MatchSeconds <= 0f
                || !float.IsFinite(types.PlayerMopSeconds) || types.PlayerMopSeconds <= 0f)
            {
                throw new ArgumentException("판 시간과 물걸레질 시간을 확인해야 합니다.");
            }

            _zoneMap = new ZoneMap(setup.Layout);
            _registry = new ContaminationRegistry(_zoneMap, items);
            _registry.ItemChanged += _ => _itemSnapshots = null;
            _cleanliness = new CleanlinessCalculator(_registry);
            _capability = new WorkerCapability(workerStats);
            Dictionary<EWorkerType, WorkerStatsSO> stats = workerStats.ToDictionary(worker => worker.Worker);
            _playerStats = stats[EWorkerType.Player];
            _mops = new MopStationService(setup.Layout, rules);
            _discharge = new DischargeService(_registry, stats[EWorkerType.Hellhound]);
            setup.MinionBodies.TryGetValue(EWorkerType.Hellhound, out _hellhound);
            _hellhound?.SetMoveSpeed(stats[EWorkerType.Hellhound].MoveSpeed);
            _halfDispatchSeconds = stats[EWorkerType.Hellhound].DispatchSeconds * 0.5f;
            _digestion = new SlimeDigestion(_registry, stats[EWorkerType.Slime]);
            _proposals = new ProposalQueue(rules.ProposalSeconds, rules.NoticeSeconds, rules.ApiTimeoutSeconds,
                dialogue ?? new DefaultProposalDialogueProvider());
            _proposals.Answered += OnProposalAnswered;
            var selector = new TaskSelector(_registry, _capability);

            foreach (var entry in setup.MinionBodies.OrderBy(entry => entry.Key))
            {
                if (entry.Key is EWorkerType.Player or EWorkerType.Hellhound || entry.Value == null) { continue; }
                var minion = new MinionBrain(entry.Value, setup.Layout, _zoneMap, _registry, selector, _mops,
                    entry.Key == EWorkerType.Slime ? _digestion : null, stats[entry.Key], rules);
                minion.NeedsProposal += OnProposalNeeded;
                _minions.Add(entry.Key, minion);
            }
            RemainingTime = rules.MatchSeconds;
        }

        public float GetZoneCleanliness(EZoneId zone) => _cleanliness.GetZoneCleanliness(zone);

        public void StartMatch()
        {
            if (MatchState == EMatchState.Ready) { SetState(EMatchState.Running); }
        }

        public void Tick(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (!CanAct || deltaTime == 0f) { return; }

            float step = Math.Min(deltaTime, RemainingTime);
            RemainingTime -= step;
            TickPlayerMop(step);
            _discharge.Tick(step);
            TickHellhound();
            _digestion.Tick(step);
            foreach (MinionBrain minion in _minions.Values) { minion.Tick(step); }
            _proposals.Tick(ElapsedTime);
            if (RemainingTime == 0f) { FinishMatch(); }
        }

        public bool BeginTalk(EWorkerType minion)
        {
            if (!CanAct || _talkingWith.HasValue || !_minions.TryGetValue(minion, out MinionBrain brain)
                || !_setup.MinionBodies.TryGetValue(minion, out IMinionBody body) || !IsWithinReach(body.Position, body.Floor))
            {
                return false;
            }
            _talkingWith = minion;
            brain.BeginTalk();
            return true;
        }

        public void CancelTalk(EWorkerType minion)
        {
            if (!CanAct || _talkingWith != minion) { return; }
            _talkingWith = null;
            _minions[minion].CancelTalk();
        }

        public bool AssignRoom(EWorkerType minion, EZoneId zone)
        {
            if (!CanAct || _talkingWith != minion || !_setup.Layout.Zones.Exists(area => area.Zone == zone)) { return false; }
            _talkingWith = null;
            _proposals.Cancel(minion);
            _minions[minion].Assign(zone);
            return true;
        }

        public bool ApproveOldestProposal() => CanAct && !_talkingWith.HasValue && _proposals.RespondOldest(true);
        public bool HoldOldestProposal() => CanAct && !_talkingWith.HasValue && _proposals.RespondOldest(false);

        public bool DispatchHellhound()
        {
            if (!CanAct || _hellhound == null || !IsWithinReach(_hellhound.Position, _hellhound.Floor)) { return false; }

            switch (_hellhoundActivity == EMinionActivity.LobbyIdle ? _discharge.TryDispatch() : EDischargeResult.Busy)
            {
                case EDischargeResult.Started:
                    _hellhoundPost = _setup.Layout.StartPositions.TryGetValue(EWorkerType.Hellhound, out Vector3 post)
                        ? post : _hellhound.Position;
                    Bounds exit = _setup.Layout.DischargeArea;
                    _hellhoundActivity = EMinionActivity.Dispatching;
                    _hellhound.MoveTo(EFloor.Floor1, new Vector3(exit.center.x, exit.min.y, exit.center.z));
                    return true;
                case EDischargeResult.Busy:
                    _proposals.AddNotice(EWorkerType.Hellhound, "지금은 폐기물을 배출하고 있어.", ElapsedTime);
                    break;
                case EDischargeResult.Empty:
                    _proposals.AddNotice(EWorkerType.Hellhound, "현관에 배출할 폐기물이 없어.", ElapsedTime);
                    break;
            }
            return false;
        }

        private void TickHellhound()
        {
            if (_hellhoundActivity == EMinionActivity.Dispatching && _discharge.RemainingTime <= _halfDispatchSeconds)
            {
                _hellhoundActivity = EMinionActivity.ReturningToLobby;
                _hellhound.MoveTo(EFloor.Floor1, _hellhoundPost);
            }
            if (_hellhoundActivity == EMinionActivity.ReturningToLobby && !_discharge.IsDispatching && _hellhound.HasArrived)
            {
                _hellhound.Stop();
                _hellhoundActivity = EMinionActivity.LobbyIdle;
            }
        }

        public bool TryPickUp(int itemId)
        {
            ContaminationItem item = _registry.FindItem(itemId);
            return CanAct && !IsPlayerHoldingMop && !IsPlayerWorking && item != null
                && IsWithinReach(item.Position, ZoneDisplayName.GetFloor(item.Zone))
                && _registry.TryPickUp(itemId, EWorkerType.Player);
        }

        public bool TryDrop()
        {
            ContaminationItem item = GetCarriedItem();
            return CanAct && item != null && _registry.TryDrop(item.Id, EWorkerType.Player, Player);
        }

        public bool TryPlaceAtHome()
        {
            ContaminationItem item = GetCarriedItem();
            if (!CanAct || item?.HomeSlotId == null) { return false; }
            HomeSlot slot = _zoneMap.FindHomeSlot(item.HomeSlotId.Value);
            return slot != null && IsWithinReach(slot.Position, ZoneDisplayName.GetFloor(slot.Zone))
                && _registry.TryPlaceAtHome(item.Id, EWorkerType.Player, slot.SlotId);
        }

        public bool TryStartMop(int itemId)
        {
            ContaminationItem item = _registry.FindItem(itemId);
            if (!CanAct || !IsPlayerHoldingMop || IsPlayerWorking || item?.Type != EContaminationType.FloorStain
                || !IsWithinReach(item.Position, ZoneDisplayName.GetFloor(item.Zone))
                || !_registry.TryClaim(itemId, EWorkerType.Player)) { return false; }

            _mopTarget = itemId;
            _mopSeconds = 0f;
            return true;
        }

        public void CancelWork()
        {
            if (_mopTarget.HasValue) { _registry.TryRelease(_mopTarget.Value, EWorkerType.Player); }
            _mopTarget = null;
            _mopSeconds = 0f;
        }

        public bool TryBorrowMop(EFloor floor)
        {
            return CanAct && floor == Player.Floor && GetCarriedItem() == null && _mops.TryBorrow(EWorkerType.Player, Player);
        }

        public bool TryReturnMop() => CanAct && !IsPlayerWorking && _mops.TryReturn(EWorkerType.Player, Player);

        private ContaminationItem GetCarriedItem()
        {
            return _registry.Items.FirstOrDefault(item => item.State == EItemState.Carried && item.Carrier == EWorkerType.Player);
        }

        private bool IsWithinReach(Vector3 position, EFloor floor)
        {
            return Player.Floor == floor && MinionMemory.IsVisible(position, Player.Position, _rules.InteractionDistance);
        }

        private void TickPlayerMop(float step)
        {
            if (!_mopTarget.HasValue) { return; }
            ContaminationItem item = _registry.FindItem(_mopTarget.Value);
            if (!IsWithinReach(item.Position, ZoneDisplayName.GetFloor(item.Zone)) || item.ClaimedBy != EWorkerType.Player)
            {
                CancelWork();
                return;
            }
            _mopSeconds += step;
            if (_mopSeconds >= _types.PlayerMopSeconds)
            {
                CancelWorkProgress();
                _registry.TryCompleteMop(item.Id, EWorkerType.Player);
            }
        }

        private void CancelWorkProgress()
        {
            _mopTarget = null;
            _mopSeconds = 0f;
        }

        private void OnProposalNeeded(MinionBrain minion, bool areMopsUnavailable)
        {
            var candidate = ProposalCandidateFinder.Find(minion, _setup.Layout, _capability, _minions.Values);
            if (candidate.HasValue)
            {
                var (zone, reason) = candidate.Value;
                _proposals.Request(minion.Worker, minion.AssignmentId, zone, reason, areMopsUnavailable, ElapsedTime);
            }
            else
            {
                _proposals.AddNotice(minion.Worker, "더 갈 방을 찾지 못했어. 1층 로비로 돌아갈게.", ElapsedTime);
                minion.ReturnToLobby();
            }
        }

        private void OnProposalAnswered(ProposalRequest request, bool isApproved)
        {
            _minions[request.Worker].AnswerProposal(request.AssignmentId, request.Zone, isApproved);
        }

        private void FinishMatch()
        {
            SetState(EMatchState.DemonKingArrived);
            _talkingWith = null;
            CancelWork();
            _proposals.Close();
            foreach (MinionBrain minion in _minions.Values) { minion.StopForEnd(); }
            _hellhound?.Stop();
            var bodies = _setup.MinionBodies.ToDictionary(entry => entry.Key, entry => (IWorkerBody)entry.Value);
            bodies[EWorkerType.Player] = Player;
            Result = MatchResolver.Resolve(_registry, _cleanliness, bodies, ZoneThreshold, FacilityThreshold);
            SetState(EMatchState.Resolved);
        }

        private void SetState(EMatchState state)
        {
            MatchState = state;
            MatchStateChanged?.Invoke(state);
        }
    }
}
