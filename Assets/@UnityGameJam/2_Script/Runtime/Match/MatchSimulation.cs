using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;
using UnityGameJam.Minion;
using UnityGameJam.Proposal;

namespace UnityGameJam.Match
{
    internal sealed class MatchSimulation : IMatchSession, IMinionSpeechSource, ICommandSkillSession, IDisposable
    {
        private const float SecondsPerSpeech = 1.8f;
        private static readonly Vector3[] RecallOffsets =
        {
            Vector3.left * .8f, Vector3.right * .8f, Vector3.forward * .8f, Vector3.back * .8f
        };
        private readonly float[] _skillReadyAt = new float[2];
        private readonly float[] _boostUntil = new float[(int)EWorkerType.Hellhound + 1];
        private readonly MatchSetup _setup;
        private readonly GameRuleSO _rules;
        private readonly ContaminationTypeSO _types;
        private readonly ZoneMap _zoneMap;
        private readonly ContaminationRegistry _registry;
        private readonly CleanlinessCalculator _cleanliness;
        private readonly WorkerCapability _capability;
        private readonly Dictionary<EWorkerType, WorkerStatsSO> _workerStats;
        private readonly WorkerStatsSO _playerStats;
        private readonly MopStationService _mops;
        private readonly DischargeService _discharge;
        private readonly IMinionBody _hellhound;
        private EMinionActivity _hellhoundActivity = EMinionActivity.LobbyIdle;
        private Vector3 _hellhoundPost;
        private readonly SlimeDigestion _digestion;
        private readonly ProposalQueue _proposals;
        private readonly TaskSelector _taskSelector;
        private readonly ITaskPreferenceProvider _taskPreferenceProvider;
        private CancellationTokenSource _taskPreferenceRequest;
        private readonly Dictionary<EWorkerType, MinionBrain> _minions = new Dictionary<EWorkerType, MinionBrain>();
        private int _observationRevision;
        private float _nextReportExchange;
        private (EZoneId Zone, EProposalReason Reason, bool AreMopsUnavailable, float StartedAt)? _orcHelp;
        private bool _hasSubmittedOrcHelp;
        private EWorkerType? _talkingWith;
        private int? _mopTarget;
        private float _mopSeconds;
        private float _elapsedTime;
        private IReadOnlyList<ItemSnapshot> _itemSnapshots;

        public EMatchState MatchState { get; private set; }
        public float RemainingTime => MatchState is EMatchState.DemonKingArrived or EMatchState.Resolved
            ? 0f : Math.Max(0f, _rules.MatchSeconds - _elapsedTime);
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
        public CommandSkillCast LastSkillCast { get; private set; }
        public float BoostDuration => _rules.SurgeSeconds;
        public float SkillCooldown(ECommandSkill skill) => skill == ECommandSkill.Shout ? _rules.ShoutCooldown : _rules.SurgeCooldown;
        public float SkillCooldownRemaining(ECommandSkill skill) => Math.Max(0f, _skillReadyAt[(int)skill] - ElapsedTime);
        public float BoostRemaining(EWorkerType worker) => CanAct ? Math.Max(0f, _boostUntil[(int)worker] - ElapsedTime) : 0f;

        public IReadOnlyList<ItemSnapshot> Items => _itemSnapshots ??= Array.AsReadOnly(_registry.Items.Select(item => item.Snapshot).ToArray());
        public IReadOnlyList<MessageEntry> ActiveMessages => _proposals.Messages;
        public (EWorkerType Speaker, string Text)? CurrentSpeech
        {
            get
            {
                if (MatchState != EMatchState.Running || !_orcHelp.HasValue || _hasSubmittedOrcHelp
                    || _talkingWith is EWorkerType.Orc or EWorkerType.Goblin) { return null; }
                var help = _orcHelp.Value;
                if (ElapsedTime - help.StartedAt < SecondsPerSpeech)
                {
                    return (EWorkerType.Orc, $"{ZoneDisplayName.GetFullName(help.Zone)}에서 정리할 걸 봤다. 좀 도와줄래?");
                }
                return (EWorkerType.Goblin, "응, 대장한테 물어볼게!");
            }
        }

        public IReadOnlyList<MinionStatus> Minions
        {
            get
            {
                var statuses = _minions.Values.Select(minion => minion.Status).ToList();
                if (_hellhound != null)
                {
                    EMinionActivity activity = _hellhoundActivity == EMinionActivity.LobbyIdle && _discharge.IsDispatching
                        ? EMinionActivity.Dispatching : _hellhoundActivity;
                    if (_talkingWith == EWorkerType.Hellhound) { activity = EMinionActivity.Talking; }
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
        private float ElapsedTime => _elapsedTime;

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
            _workerStats = workerStats.ToDictionary(worker => worker.Worker);
            _playerStats = _workerStats[EWorkerType.Player];
            _mops = new MopStationService(setup.Layout, rules);
            _discharge = new DischargeService(_registry, _workerStats[EWorkerType.Hellhound]);
            setup.MinionBodies.TryGetValue(EWorkerType.Hellhound, out _hellhound);
            _hellhound?.SetMoveSpeed(_workerStats[EWorkerType.Hellhound].MoveSpeed);
            _digestion = new SlimeDigestion(_registry, _workerStats[EWorkerType.Slime]);
            _proposals = new ProposalQueue(rules.ProposalSeconds, rules.NoticeSeconds, rules.ApiTimeoutSeconds,
                dialogue ?? new DefaultProposalDialogueProvider());
            _proposals.Answered += OnProposalAnswered;
            _taskSelector = new TaskSelector(_registry, _capability);
            _taskPreferenceProvider = dialogue as ITaskPreferenceProvider;

            foreach (var entry in setup.MinionBodies.OrderBy(entry => entry.Key))
            {
                if (entry.Key is EWorkerType.Player or EWorkerType.Hellhound || entry.Value == null) { continue; }
                var minion = new MinionBrain(entry.Value, setup.Layout, _zoneMap, _registry, _taskSelector, _mops,
                    entry.Key == EWorkerType.Slime ? _digestion : null, _workerStats[entry.Key], rules);
                minion.NeedsProposal += OnProposalNeeded;
                minion.TryOfferCollaboration += OfferCollaborationDuringWork;
                minion.ShouldYieldTask += ShouldYieldTask;
                _minions.Add(entry.Key, minion);
            }
            _registry.FloorWorkBecameAvailable += InvalidateRoomMemories;
        }

        private void InvalidateRoomMemories(EZoneId zone)
        {
            foreach (MinionBrain minion in _minions.Values) { minion.InvalidateRoomMemory(zone); }
        }

        public float GetZoneCleanliness(EZoneId zone) => _cleanliness.GetZoneCleanliness(zone);

        public void StartMatch()
        {
            if (MatchState != EMatchState.Ready) { return; }
            SetState(EMatchState.Running);
            if (_taskPreferenceProvider == null) { return; }

            var types = (EContaminationType[])Enum.GetValues(typeof(EContaminationType));
            var allowed = _minions.Keys.Select(worker => (Worker: worker,
                    Tasks: types.Where(type => _capability.CanHandle(worker, type)).ToArray()))
                .Where(entry => entry.Tasks.Length > 1).ToDictionary(entry => entry.Worker, entry => entry.Tasks);
            if (allowed.Count == 0) { return; }

            _taskPreferenceRequest = new CancellationTokenSource();
            _taskPreferenceProvider.RequestTaskPreferences(allowed, _taskPreferenceRequest.Token, _taskSelector.SetPreferences);
        }

        public void Tick(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (MatchState != EMatchState.Running || deltaTime == 0f) { return; }
            if (RemainingTime == 0f) { FinishMatch(); return; }

            float step = Math.Min(deltaTime, RemainingTime);
            _elapsedTime += step;
            TickPlayerMop(step);
            _hellhound?.SetActionRate(ActionRate(EWorkerType.Hellhound));
            _discharge.Tick(BoostedDelta(EWorkerType.Hellhound, step));
            TickHellhound();
            _digestion.Tick(BoostedDelta(EWorkerType.Slime, step));
            if (ElapsedTime >= _nextReportExchange)
            {
                _nextReportExchange = ElapsedTime + .25f;
                ShareRoomReports();
            }
            foreach (MinionBrain minion in _minions.Values)
            {
                minion.SetActionRate(ActionRate(minion.Worker));
                minion.Tick(step, ++_observationRevision, BoostedDelta(minion.Worker, step));
            }
            SubmitOrcHelp();
            _proposals.Tick(ElapsedTime);
            if (RemainingTime == 0f) { FinishMatch(); }
        }

        public bool TryCastSkill(ECommandSkill skill)
        {
            if (!CanAct || _talkingWith.HasValue || skill is not (ECommandSkill.Shout or ECommandSkill.Surge)
                || SkillCooldownRemaining(skill) > 0f) { return false; }
            bool applied = skill == ECommandSkill.Shout ? RecallMinions() : BoostMinions();
            if (!applied) { return false; }

            _skillReadyAt[(int)skill] = ElapsedTime + SkillCooldown(skill);
            LastSkillCast = new CommandSkillCast(skill, Player.Position, Player.Floor, LastSkillCast.Sequence + 1);
            return true;
        }

        private bool RecallMinions()
        {
            if (_minions.Count == 0) { return false; }
            Vector3 position = Player.Position;
            EZoneId? room = PlayerZone;
            ClearOrcHelp();
            foreach (MinionBrain minion in _minions.Values)
            {
                _proposals.Cancel(minion.Worker);
                Vector3 approach = position + RecallOffsets[(int)minion.Worker - (int)EWorkerType.Goblin];
                if (_zoneMap.FindZone(approach, Player.Floor) != room) { approach = position; }
                minion.Recall(Player.Floor, position, approach);
                AddSkillReaction(minion.Worker, ECommandSkill.Shout);
            }
            return true;
        }

        private bool BoostMinions()
        {
            if (PlayerZone is not EZoneId room) { return false; }
            bool applied = false;
            foreach (MinionBrain minion in _minions.Values)
            {
                if (minion.Floor != Player.Floor || minion.CurrentZone != room) { continue; }
                Boost(minion.Worker);
                minion.SetActionRate(_rules.SurgeRate);
                applied = true;
            }
            if (_hellhound != null && _hellhound.Floor == Player.Floor
                && _zoneMap.FindZone(_hellhound.Position, _hellhound.Floor) == room)
            {
                Boost(EWorkerType.Hellhound);
                _hellhound.SetActionRate(_rules.SurgeRate);
                applied = true;
            }
            return applied;
        }

        private void Boost(EWorkerType worker)
        {
            _boostUntil[(int)worker] = ElapsedTime + _rules.SurgeSeconds;
            AddSkillReaction(worker, ECommandSkill.Surge);
        }

        private float ActionRate(EWorkerType worker) => BoostRemaining(worker) > 0f ? _rules.SurgeRate : 1f;

        private float BoostedDelta(EWorkerType worker, float step)
        {
            float boosted = Mathf.Clamp(_boostUntil[(int)worker] - (ElapsedTime - step), 0f, step);
            return step + boosted * (_rules.SurgeRate - 1f);
        }

        private void AddSkillReaction(EWorkerType worker, ECommandSkill skill)
        {
            string line = DefaultProposalDialogueProvider.SkillReaction(worker, skill);
            _proposals.AddNotice(worker, line, ElapsedTime);
        }

        public bool BeginTalk(EWorkerType minion)
        {
            if (!CanAct || _talkingWith.HasValue
                || !_setup.MinionBodies.TryGetValue(minion, out IMinionBody body) || !IsWithinReach(body.Position, body.Floor))
            {
                return false;
            }
            if (minion == EWorkerType.Hellhound)
            {
                if (_hellhoundActivity != EMinionActivity.LobbyIdle || _discharge.IsDispatching) { return false; }
            }
            else if (_minions.TryGetValue(minion, out MinionBrain brain)) { brain.BeginTalk(); }
            else { return false; }

            _talkingWith = minion;
            return true;
        }

        public void CancelTalk(EWorkerType minion)
        {
            if (!CanAct || _talkingWith != minion) { return; }
            _talkingWith = null;
            if (_minions.TryGetValue(minion, out MinionBrain brain)) { brain.CancelTalk(); }
        }

        public bool AssignRoom(EWorkerType minion, EZoneId zone)
        {
            if (!CanAct || _talkingWith != minion || !_minions.TryGetValue(minion, out MinionBrain brain)
                || !_setup.Layout.Zones.Exists(area => area.Zone == zone)) { return false; }
            _talkingWith = null;
            _proposals.Cancel(minion);
            brain.Assign(zone);
            if (_orcHelp.HasValue && minion is EWorkerType.Orc or EWorkerType.Goblin)
            {
                bool areMopsUnavailable = _orcHelp.Value.AreMopsUnavailable;
                ClearOrcHelp();
                if (minion == EWorkerType.Orc)
                {
                    _proposals.Cancel(EWorkerType.Goblin);
                    RequestRegularProposal(_minions[EWorkerType.Goblin], areMopsUnavailable);
                }
            }
            return true;
        }

        public bool ApproveOldestProposal() => CanAct && !_talkingWith.HasValue && _proposals.RespondOldest(true, TryApproveProposal);
        public bool HoldOldestProposal() => CanAct && !_talkingWith.HasValue && _proposals.RespondOldest(false);

        public bool DispatchHellhound()
        {
            if (!CanAct || (_talkingWith.HasValue && _talkingWith != EWorkerType.Hellhound)
                || _hellhound == null || !IsWithinReach(_hellhound.Position, _hellhound.Floor)) { return false; }
            if (_talkingWith == EWorkerType.Hellhound) { _talkingWith = null; }

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
            if (_hellhoundActivity == EMinionActivity.Dispatching && _discharge.HasReachedReturnTime)
            {
                _hellhoundActivity = EMinionActivity.ReturningToLobby;
                _hellhound.MoveTo(EFloor.Floor1, _hellhoundPost);
            }
            if (_hellhoundActivity == EMinionActivity.ReturningToLobby && !_discharge.IsDispatching
                && (_hellhound.HasArrived || _hellhound.HasMoveFailed))
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
            return CanAct && item != null
                && (_registry.TryDisposeTrash(item.Id, EWorkerType.Player, Player, _rules.InteractionDistance)
                    || _registry.TryDrop(item.Id, EWorkerType.Player, Player));
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

        private bool CanAccessPlayerMopRack(EFloor floor) =>
            CanAct && floor == Player.Floor && GetCarriedItem() == null;

        public bool CanPlayerBorrowMop(EFloor floor) =>
            CanAccessPlayerMopRack(floor) && _mops.CanBorrow(EWorkerType.Player, Player);

        public bool TryBorrowMop(EFloor floor) =>
            CanAccessPlayerMopRack(floor) && _mops.TryBorrow(EWorkerType.Player, Player);

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

        private void ShareRoomReports()
        {
            foreach (MinionBrain first in _minions.Values)
            {
                foreach (MinionBrain second in _minions.Values)
                {
                    if ((int)first.Worker >= (int)second.Worker || !first.CanShareWith(second)) { continue; }
                    bool firstShared = first.Memory.ShareWith(second.Memory);
                    bool secondShared = second.Memory.ShareWith(first.Memory);
                    if (firstShared) { _proposals.AddRoomReport(first.Worker, second.Worker, ElapsedTime); }
                    else if (secondShared) { _proposals.AddRoomReport(second.Worker, first.Worker, ElapsedTime); }
                }
            }
        }

        private void OnProposalNeeded(MinionBrain minion, bool areMopsUnavailable)
        {
            if (RemainingTime <= 0f) { return; }
            ShareRoomReports();
            if (TryProposeCollaboration(minion, areMopsUnavailable)) { return; }
            var (zones, reason, coworkers) = ProposalCandidateFinder.Find(minion, _setup.Layout, _capability,
                _minions.Values, !areMopsUnavailable, _taskSelector.PreferredTask(minion.Worker));
            if (minion.Worker == EWorkerType.Goblin && _minions.TryGetValue(EWorkerType.Orc, out MinionBrain orc)
                && orc.CanShareWith(minion))
            {
                foreach (EZoneId zone in zones.OrderBy(zone => zone != orc.AssignedZone))
                {
                    if (!orc.Memory.HasDirectKnownWork(zone, minion.Worker, _capability, !areMopsUnavailable)) { continue; }
                    _orcHelp = (zone, reason, areMopsUnavailable, ElapsedTime);
                    return;
                }
            }
            QueueRegularProposal(minion, areMopsUnavailable, zones, reason, coworkers);
        }

        private bool ShouldYieldTask(MinionBrain minion, ContaminationItem item)
        {
            float? ownTime = null;
            foreach (MinionBrain other in _minions.Values)
            {
                if (other == minion || other.PeekAvailableTask(item.Zone) != item) { continue; }
                ownTime ??= EstimateTaskSeconds(minion.Worker, item);
                if (EstimateTaskSeconds(other.Worker, item) < ownTime.Value) { return true; }
            }
            return false;
        }

        private bool OfferCollaborationDuringWork(MinionBrain minion) =>
            TryProposeCollaboration(minion, _mops.AvailableCount == 0, duringWork: true);

        private bool TryProposeCollaboration(MinionBrain minion, bool areMopsUnavailable, bool duringWork = false)
        {
            if (RemainingTime <= _rules.ProposalSeconds) { return false; }

            MinionBrain[] nearby = _minions.Values.Where(other => other != minion && minion.CanShareWith(other)).ToArray();
            var knownTasks = minion.Memory.ObservedFloorTasks().Concat(nearby.SelectMany(other => other.Memory.ObservedFloorTasks()))
                .Distinct().Where(task => !minion.Memory.IsKnownClean(task.Zone) && IsAvailableTask(task)).ToArray();
            var plans = new List<CollaborationPlan>();
            var estimates = new Dictionary<(EWorkerType Worker, int ItemId), float>();

            float Estimate(EWorkerType worker, int itemId)
            {
                if (!estimates.TryGetValue((worker, itemId), out float seconds))
                {
                    seconds = EstimateTaskSeconds(worker, _registry.FindItem(itemId));
                    estimates.Add((worker, itemId), seconds);
                }
                return seconds;
            }

            void AddPlan((EZoneId Zone, int ItemId, EContaminationType Type) own,
                (EZoneId Zone, int ItemId, EContaminationType Type) other, EWorkerType partner, int assignmentId, int floors)
            {
                float seconds = Math.Max(Estimate(minion.Worker, own.ItemId), Estimate(partner, other.ItemId));
                if (seconds <= RemainingTime)
                {
                    plans.Add(new CollaborationPlan(own, other, partner, assignmentId, floors, seconds));
                }
            }

            foreach (var own in knownTasks)
            {
                if ((duringWork && own.Zone != minion.AssignedZone) || !CanTakeTask(minion.Worker, own)) { continue; }
                foreach (var other in knownTasks)
                {
                    if (own.ItemId == other.ItemId || own.Type == other.Type) { continue; }
                    bool splitCarryAndMop = (own.Type is EContaminationType.HeavyDebris or EContaminationType.Trash)
                        && other.Type == EContaminationType.FloorStain;
                    if (duringWork && ((!splitCarryAndMop && CanTakeTask(minion.Worker, other))
                        || nearby.Any(partner => partner.AssignedZone == other.Zone && partner.CurrentZone == other.Zone
                            && CanTakeTask(partner.Worker, other))))
                    {
                        continue;
                    }
                    if ((!duringWork || PlayerZone == other.Zone) && CanPlayerAssist(other))
                    {
                        AddPlan(own, other, EWorkerType.Player, 0, FloorsTo(minion.Floor, own.Zone));
                    }
                    foreach (MinionBrain partner in nearby)
                    {
                        if (own.Zone != other.Zone || !partner.CanJoinCollaboration || !CanTakeTask(partner.Worker, other)) { continue; }
                        int floors = FloorsTo(minion.Floor, own.Zone) + FloorsTo(partner.Floor, other.Zone);
                        AddPlan(own, other, partner.Worker, partner.AssignmentId, floors);
                    }
                }
            }

            EContaminationType? preferredTask = _taskSelector.PreferredTask(minion.Worker);
            CollaborationPlan[] candidates = plans.OrderBy(plan => plan.EstimatedSeconds)
                .ThenBy(plan => plan.FloorChanges)
                .ThenByDescending(plan => plan.OwnTask.Type == preferredTask)
                .ThenBy(plan => plan.OwnTask.Zone != plan.PartnerTask.Zone)
                .ThenBy(plan => plan.OwnTask.ItemId).ThenBy(plan => plan.PartnerTask.ItemId)
                .GroupBy(plan => plan.Kind).SelectMany(group => group.Take(2))
                .OrderBy(plan => plan.EstimatedSeconds).ToArray();
            if (candidates.Length == 0) { return false; }

            return _proposals.Request(minion.Worker, minion.AssignmentId, candidates[0].OwnTask.Zone, EProposalReason.KnownWork,
                areMopsUnavailable, ElapsedTime, plans: candidates, remainingSeconds: RemainingTime,
                preferredTask: preferredTask);
        }

        private float EstimateTaskSeconds(EWorkerType worker, ContaminationItem item)
        {
            IWorkerBody body = worker == EWorkerType.Player ? Player : _setup.MinionBodies[worker];
            WorkerStatsSO stats = _workerStats[worker];
            float speed = stats.MoveSpeed;
            Vector3 start = body.Position;
            EFloor startFloor = body.Floor;
            EFloor itemFloor = ZoneDisplayName.GetFloor(item.Zone);
            float toolSeconds = 0f;
            ToiletStation borrowedFrom = _mops.GetBorrowedFrom(worker);
            ToiletStation toolStop = borrowedFrom;
            if (item.Type == EContaminationType.FloorStain)
            {
                toolStop = null;
                if (borrowedFrom == null)
                {
                    toolStop = worker == EWorkerType.Player
                        ? _setup.Layout.Toilets.Find(toilet => toilet.Floor == body.Floor && _mops.GetAvailableCount(toilet.Floor) > 0)
                        : _mops.FindAvailableStation(body.Floor);
                    if (toolStop == null) { return float.PositiveInfinity; }
                }
            }
            if (toolStop != null)
            {
                toolSeconds = TravelSeconds(startFloor, start, toolStop.Floor, toolStop.MopPickupPosition, speed);
                startFloor = toolStop.Floor;
                start = toolStop.MopPickupPosition;
            }

            float approach = toolSeconds + TravelSeconds(startFloor, start, itemFloor, item.Position, speed);
            if (item.Type == EContaminationType.FloorStain)
            {
                return approach + (worker == EWorkerType.Player ? _types.PlayerMopSeconds : stats.MopSeconds);
            }
            if (_capability.CanDigest(worker, item.Type)) { return Math.Max(approach, _digestion.RemainingTime); }

            if (item.Type == EContaminationType.ClutterItem)
            {
                HomeSlot slot = item.HomeSlotId is int id ? _zoneMap.FindHomeSlot(id) : null;
                if (slot == null) { return float.PositiveInfinity; }
                float carrySpeed = worker == EWorkerType.Player ? speed : speed * stats.ClutterCarryMultiplier;
                return approach + TravelSeconds(itemFloor, item.Position, ZoneDisplayName.GetFloor(slot.Zone), slot.Position, carrySpeed);
            }
            if (item.Type == EContaminationType.Trash
                && _setup.Layout.FindNearestWasteBin(itemFloor, item.Position) is WasteBin bin)
            {
                return approach + TravelSeconds(itemFloor, item.Position, bin.Floor, bin.Position, speed);
            }

            Bounds exit = _setup.Layout.DischargeArea;
            if (_hellhound == null || exit.size.x <= 0f || exit.size.z <= 0f) { return float.PositiveInfinity; }
            float wasteSpeed = item.Type == EContaminationType.HeavyDebris ? speed * stats.HeavyCarryMultiplier : speed;
            float delivery = approach + TravelSeconds(itemFloor, item.Position, EFloor.Floor1, exit.center, wasteSpeed);
            Vector3 post = _hellhoundActivity == EMinionActivity.LobbyIdle ? _hellhound.Position : _hellhoundPost;
            float order = worker == EWorkerType.Player
                ? delivery + TravelSeconds(EFloor.Floor1, exit.center, EFloor.Floor1, post, _playerStats.MoveSpeed)
                : TravelSeconds(Player.Floor, Player.Position, EFloor.Floor1, post, _playerStats.MoveSpeed);
            float houndReady = _discharge.RemainingTime;
            if (_hellhoundActivity == EMinionActivity.ReturningToLobby)
            {
                float returnSeconds = TravelSeconds(_hellhound.Floor, _hellhound.Position, EFloor.Floor1, post,
                    _workerStats[EWorkerType.Hellhound].MoveSpeed);
                houndReady = Math.Max(houndReady, returnSeconds);
            }
            return Math.Max(Math.Max(delivery, order), houndReady);
        }

        private float TravelSeconds(EFloor floor, Vector3 start, EFloor destinationFloor, Vector3 destination, float speed)
        {
            // shortcut: 층별 직선 이동과 계단은 반영하되, 실제 경로 우회가 순위를 자주 바꾸면 NavMesh 경로 길이를 쓴다.
            if (speed <= 0f) { return float.PositiveInfinity; }
            float seconds = 0f;
            while (floor != destinationFloor)
            {
                EFloor next = floor + (destinationFloor > floor ? 1 : -1);
                if (_setup.StairEntries == null || !_setup.StairEntries.TryGetValue((floor, next), out Vector3 entry)
                    || !_setup.StairEntries.TryGetValue((next, floor), out Vector3 exit))
                {
                    return float.PositiveInfinity;
                }
                seconds += PlanarDistance(start, entry) / speed + _setup.StairTransitSeconds;
                start = exit;
                floor = next;
            }
            return seconds + PlanarDistance(start, destination) / speed;
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            Vector3 offset = first - second;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static int FloorsTo(EFloor from, EZoneId zone) => Math.Abs((int)from - (int)ZoneDisplayName.GetFloor(zone));

        private bool IsAvailableTask((EZoneId Zone, int ItemId, EContaminationType Type) task)
        {
            ContaminationItem item = _registry.FindItem(task.ItemId);
            return item != null && item.Zone == task.Zone && item.Type == task.Type
                && item.State == EItemState.OnFloor && !item.ClaimedBy.HasValue;
        }

        private bool CanTakeTask(EWorkerType worker, (EZoneId Zone, int ItemId, EContaminationType Type) task)
        {
            if (!_capability.CanHandle(worker, task.Type)) { return false; }
            return task.Type != EContaminationType.FloorStain
                || _mops.GetBorrowedFrom(worker) != null || _mops.AvailableCount > 0;
        }

        private bool CanPlayerAssist((EZoneId Zone, int ItemId, EContaminationType Type) task)
        {
            if (Player.Floor != ZoneDisplayName.GetFloor(task.Zone) || IsPlayerWorking || GetCarriedItem() != null) { return false; }
            return task.Type == EContaminationType.FloorStain
                ? IsPlayerHoldingMop || _mops.GetAvailableCount(Player.Floor) > 0
                : !IsPlayerHoldingMop;
        }

        private bool TryApproveProposal(ProposalRequest request)
        {
            MinionBrain minion = _minions[request.Worker];
            if (minion.AssignmentId != request.AssignmentId || minion.Status.Activity != EMinionActivity.AwaitingResponse) { return false; }
            if (request.SelectedPlan is not CollaborationPlan plan)
            {
                EZoneId zone = request.Zone;
                bool hasWork = minion.Memory.HasKnownWork(zone, minion.Worker, _capability, _mops.AvailableCount > 0);
                if (!minion.Memory.IsKnownClean(zone) && (hasWork || (!minion.Memory.HasCompletedSurvey(zone)
                    && !ProposalCandidateFinder.HasOtherAssignment(minion, zone, _minions.Values)))) { return true; }

                if (request.IsOrcHelp) { ClearOrcHelp(); }
                RequestReplacementProposal(minion);
                return false;
            }

            bool playerAssists = plan.Partner == EWorkerType.Player;
            MinionBrain partner = playerAssists ? null : _minions[plan.Partner];
            bool partnerReady = playerAssists ? CanPlayerAssist(plan.PartnerTask)
                : partner.AssignmentId == plan.PartnerAssignmentId && partner.CanJoinCollaboration;
            if (!partnerReady || !IsAvailableTask(plan.OwnTask) || !IsAvailableTask(plan.PartnerTask)
                || !CanTakeTask(minion.Worker, plan.OwnTask) || !CanTakeTask(plan.Partner, plan.PartnerTask)
                || EstimateTaskSeconds(minion.Worker, _registry.FindItem(plan.OwnTask.ItemId)) > RemainingTime
                || EstimateTaskSeconds(plan.Partner, _registry.FindItem(plan.PartnerTask.ItemId)) > RemainingTime
                || !_registry.TryClaim(plan.OwnTask.ItemId, minion.Worker))
            {
                RequestReplacementProposal(minion);
                return false;
            }

            if (partner != null)
            {
                if (!_registry.TryClaim(plan.PartnerTask.ItemId, partner.Worker))
                {
                    _registry.TryRelease(plan.OwnTask.ItemId, minion.Worker);
                    RequestReplacementProposal(minion);
                    return false;
                }
                _proposals.Cancel(partner.Worker);
                if (partner.Worker == EWorkerType.Goblin) { ClearOrcHelp(); }
                partner.Assign(plan.PartnerTask.Zone, plan.PartnerTask.ItemId);
            }
            minion.Assign(plan.OwnTask.Zone, plan.OwnTask.ItemId);
            if (playerAssists)
            {
                string work = DefaultProposalDialogueProvider.WorkDescription(plan.PartnerTask);
                _proposals.AddNotice(minion.Worker, $"대장, {work} 부탁해.", ElapsedTime, plan.Kind);
            }
            return true;
        }

        private void RequestReplacementProposal(MinionBrain minion)
        {
            _proposals.AddNotice(minion.Worker, "상황이 바뀌었어. 다른 일을 찾아볼게.", ElapsedTime);
            if (!minion.ResumeWorkAfterCollaboration()) { RequestRegularProposal(minion, _mops.AvailableCount == 0); }
        }

        private void RequestRegularProposal(MinionBrain minion, bool areMopsUnavailable)
        {
            var (zones, reason, coworkers) = ProposalCandidateFinder.Find(minion, _setup.Layout, _capability,
                _minions.Values, !areMopsUnavailable, _taskSelector.PreferredTask(minion.Worker));
            QueueRegularProposal(minion, areMopsUnavailable, zones, reason, coworkers);
        }

        private void QueueRegularProposal(MinionBrain minion, bool areMopsUnavailable, EZoneId[] zones,
            EProposalReason reason, IReadOnlyDictionary<EZoneId, EWorkerType> coworkers)
        {
            if (zones.Length == 0)
            {
                const string returnLine = "더 갈 방을 찾지 못했어. 1층 로비로 돌아갈게.";
                string notice = minion.AssignedZone is EZoneId zone
                    ? $"{ZoneDisplayName.GetFullName(zone)}에서 지금 할 수 있는 일이 없어.\n{returnLine}"
                    : returnLine;
                _proposals.AddNotice(minion.Worker, notice, ElapsedTime);
                minion.ReturnToLobby();
                return;
            }
            _proposals.Request(minion.Worker, minion.AssignmentId, zones[0], reason, areMopsUnavailable, ElapsedTime,
                zones, coworkers: coworkers, preferredTask: _taskSelector.PreferredTask(minion.Worker));
        }

        private void SubmitOrcHelp()
        {
            if (!_orcHelp.HasValue || _hasSubmittedOrcHelp || RemainingTime <= 0f) { return; }
            var help = _orcHelp.Value;
            MinionBrain goblin = _minions[EWorkerType.Goblin];
            if (!_minions[EWorkerType.Orc].CanShareWith(goblin))
            {
                ClearOrcHelp();
                RequestRegularProposal(goblin, help.AreMopsUnavailable);
                return;
            }
            if (ElapsedTime - help.StartedAt < SecondsPerSpeech * 2f) { return; }
            _hasSubmittedOrcHelp = true;
            if (goblin.Memory.IsKnownClean(help.Zone))
            {
                ClearOrcHelp();
                RequestRegularProposal(goblin, help.AreMopsUnavailable);
                return;
            }
            _proposals.Request(goblin.Worker, goblin.AssignmentId, help.Zone, help.Reason,
                help.AreMopsUnavailable, ElapsedTime, isOrcHelp: true);
        }

        private void ClearOrcHelp()
        {
            _orcHelp = null;
            _hasSubmittedOrcHelp = false;
        }

        private void OnProposalAnswered(ProposalRequest request, bool isApproved)
        {
            if (isApproved && request.SelectedPlan.HasValue) { return; }
            if (request.IsOrcHelp) { ClearOrcHelp(); }
            MinionBrain minion = _minions[request.Worker];
            if (minion.AssignmentId != request.AssignmentId) { return; }
            if (!isApproved && request.SelectedPlan.HasValue && minion.ResumeWorkAfterCollaboration()) { return; }
            minion.AnswerProposal(request.AssignmentId, request.Zone, isApproved);
        }

        public void Dispose()
        {
            _taskPreferenceRequest?.Cancel();
            _taskPreferenceRequest?.Dispose();
            _taskPreferenceRequest = null;
            ClearOrcHelp();
            _proposals.Close();
        }

        private void FinishMatch()
        {
            SetState(EMatchState.DemonKingArrived);
            _talkingWith = null;
            CancelWork();
            Dispose();
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
