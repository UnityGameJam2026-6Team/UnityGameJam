using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;

namespace UnityGameJam.Minion
{
    internal sealed class MinionBrain
    {
        private enum EStep
        {
            Lobby, Entering, Patrolling, Approaching, Carrying, BorrowingMop, ReturningMop, Mopping,
            Digesting, AwaitingResponse, ReturningLobby, Recalling
        }

        private readonly IMinionBody _body;
        private readonly FacilityLayout _layout;
        private readonly ZoneMap _zoneMap;
        private readonly ContaminationRegistry _registry;
        private readonly TaskSelector _selector;
        private readonly MopStationService _mops;
        private readonly SlimeDigestion _digestion;
        private readonly WorkerStatsSO _stats;
        private readonly GameRuleSO _rules;
        private EStep _step;
        private PatrolRoute _patrol;
        private int _nextPoint;
        private readonly Dictionary<int, Vector3> _unreachableTasks = new();
        private int? _workId;
        private int? _carriedId;
        private float _mopElapsed;
        private float? _appliedMoveSpeed;
        private bool _isTalking;
        private bool _isMoving;
        private bool _isReassigningAfterReturn;
        private bool _hasRetriedMopReturn;
        private bool _areMopsUnavailable;
        private bool _hasOfferedCollaboration;
        private bool _isWaitingDuringWork;
        private EFloor _destinationFloor;
        private Vector3 _destination;
        private int _observationRevision;
        private Vector3 _recallTarget;
        private Vector3 _recallLastPosition;
        private float _recallStalled;
        private bool _recallRetried;

        public EWorkerType Worker => _stats.Worker;
        public EFloor Floor => _body.Floor;
        public EZoneId? CurrentZone => _zoneMap.FindZone(_body.Position, _body.Floor);
        public EZoneId? AssignedZone { get; private set; }
        public int AssignmentId { get; private set; }
        public bool CanJoinCollaboration => !_isTalking && !_workId.HasValue && !_carriedId.HasValue
            && (_step is EStep.AwaitingResponse or EStep.Patrolling or EStep.ReturningLobby or EStep.Lobby);
        public MinionMemory Memory { get; } = new MinionMemory();
        public event Action<MinionBrain, bool> NeedsProposal;
        public event Func<MinionBrain, bool> TryOfferCollaboration;
        public event Func<MinionBrain, ContaminationItem, bool> ShouldYieldTask;

        public MinionBrain(IMinionBody body, FacilityLayout layout, ZoneMap zoneMap, ContaminationRegistry registry,
            TaskSelector selector, MopStationService mops, SlimeDigestion digestion, WorkerStatsSO stats, GameRuleSO rules)
        {
            _body = body;
            _layout = layout;
            _zoneMap = zoneMap;
            _registry = registry;
            _selector = selector;
            _mops = mops;
            _digestion = digestion;
            _stats = stats;
            _rules = rules;
            UpdateMoveSpeed();
        }

        public MinionStatus Status => new MinionStatus(Worker, GetActivity(), AssignedZone, _carriedId,
            _mops.GetBorrowedFrom(Worker) != null);

        public bool IsWorkingIn(EZoneId zone)
        {
            return AssignedZone == zone && (_step is EStep.Patrolling or EStep.Approaching or EStep.Mopping
                or EStep.Carrying or EStep.Digesting or EStep.BorrowingMop) && CurrentZone == zone;
        }

        public bool CanShareWith(MinionBrain other)
        {
            if (_isTalking || other._isTalking || Floor != other.Floor
                || !MinionMemory.IsVisible(other._body.Position, _body.Position, _rules.InteractionDistance))
            {
                return false;
            }

            return !(CurrentZone is EZoneId zone && ZoneDisplayName.IsLobby(zone))
                && !(other.CurrentZone is EZoneId otherZone && ZoneDisplayName.IsLobby(otherZone));
        }

        public void BeginTalk()
        {
            _isTalking = true;
            _body.Stop();
        }

        public void CancelTalk()
        {
            _isTalking = false;
            if (_isMoving) { _body.MoveTo(_destinationFloor, _destination); }
        }

        public void Assign(EZoneId zone, int? reservedItemId = null)
        {
            _isTalking = false;
            _isWaitingDuringWork = false;
            _hasOfferedCollaboration = reservedItemId.HasValue;
            ReleaseWork();
            if (_carriedId.HasValue)
            {
                int id = _carriedId.Value;
                _carriedId = null;
                _registry.TryDrop(id, Worker, _body);
                UpdateMoveSpeed();
            }

            AssignmentId++;
            AssignedZone = zone;
            Memory.BeginSurvey(zone);
            _patrol = _layout.PatrolRoutes.Find(route => route.Zone == zone);
            _nextPoint = 1;
            _unreachableTasks.Clear();
            _workId = reservedItemId;
            bool needsMop = reservedItemId.HasValue && _registry.FindItem(reservedItemId.Value)?.Type == EContaminationType.FloorStain;
            if (!needsMop && TryReturnMop(true)) { return; }
            EnterRoom();
        }

        public void Recall(EFloor floor, Vector3 origin, Vector3 approach)
        {
            _isTalking = false;
            _isWaitingDuringWork = false;
            ReleaseWork();
            if (_carriedId is int carriedId)
            {
                _carriedId = null;
                _registry.TryDrop(carriedId, Worker, _body);
                UpdateMoveSpeed();
            }
            _mops.Release(Worker);
            AssignmentId++;
            AssignedZone = null;
            _recallTarget = origin;
            _recallLastPosition = _body.Position;
            _recallStalled = 0f;
            _recallRetried = false;
            Move(EStep.Recalling, floor, approach);
        }

        public void SetActionRate(float rate) => _body.SetActionRate(rate);

        public void InvalidateRoomMemory(EZoneId zone)
        {
            Memory.InvalidateRoom(zone);
            if (AssignedZone == zone) { _nextPoint = 1; }
        }

        public void AnswerProposal(int assignmentId, EZoneId zone, bool isApproved)
        {
            if (AssignmentId != assignmentId || _step != EStep.AwaitingResponse) { return; }
            if (isApproved) { Assign(zone); }
            else { ReturnToLobby(); }
        }

        public bool ResumeWorkAfterCollaboration()
        {
            if (!_isWaitingDuringWork) { return false; }
            _isWaitingDuringWork = false;
            ResumeAssignedRoom();
            return true;
        }

        public void StopForEnd()
        {
            ReleaseWork();
            _carriedId = null;
            _isTalking = false;
            StopMoving();
        }

        public void Tick(float deltaTime, int observationRevision, float workDelta = -1f)
        {
            _observationRevision = observationRevision;
            UpdateMoveSpeed();
            if (_isTalking) { return; }
            switch (_step)
            {
                case EStep.Entering:
                    if (HasArrived()) { SearchRoom(); }
                    else if (_body.HasMoveFailed)
                    {
                        ReleaseWork();
                        FinishRoom(false);
                    }
                    break;
                case EStep.Patrolling:
                    if (ObserveAndChoose()) { break; }
                    if (!_isMoving || HasArrived()) { ContinuePatrol(); }
                    else if (_body.HasMoveFailed) { FinishRoom(false); }
                    break;
                case EStep.Approaching:
                    Approach();
                    break;
                case EStep.Carrying:
                    if (HasArrived() || _body.HasMoveFailed) { Deliver(); }
                    break;
                case EStep.BorrowingMop:
                    CollectMop();
                    break;
                case EStep.ReturningMop:
                    ReturnMop();
                    break;
                case EStep.Mopping:
                    Mop(workDelta >= 0f ? workDelta : deltaTime);
                    break;
                case EStep.Digesting:
                    if (!_digestion.IsDigesting) { SearchRoom(); }
                    break;
                case EStep.ReturningLobby:
                    if (HasArrived() || _body.HasMoveFailed) { StopMoving(); _step = EStep.Lobby; }
                    break;
                case EStep.Recalling:
                    TickRecall(deltaTime);
                    break;
            }
        }

        private void TickRecall(float deltaTime)
        {
            bool arrived = HasArrived();
            if (arrived && IsNear(_recallTarget))
            {
                StopMoving();
                _step = EStep.Lobby;
                return;
            }
            if ((_body.Position - _recallLastPosition).sqrMagnitude > .0025f)
            {
                _recallLastPosition = _body.Position;
                _recallStalled = 0f;
            }
            if (!arrived && !_body.HasMoveFailed && (_recallStalled += deltaTime) < 8f) { return; }
            if (!_recallRetried)
            {
                _recallRetried = true;
                _recallStalled = 0f;
                Move(EStep.Recalling, _destinationFloor, _recallTarget);
                return;
            }
            StopMoving();
            _step = EStep.Lobby;
        }

        private void EnterRoom()
        {
            EZoneId zone = AssignedZone.Value;
            Vector3 entrance = _patrol != null && _patrol.Points.Count > 0
                ? _patrol.Points[0] : _layout.Zones.Find(area => area.Zone == zone).Areas[0].center;
            Move(EStep.Entering, ZoneDisplayName.GetFloor(zone), entrance);
        }

        private void ResumeAssignedRoom()
        {
            if (CurrentZone == AssignedZone) { SearchRoom(); }
            else { EnterRoom(); }
        }

        private void SearchRoom()
        {
            if (ObserveAndChoose()) { return; }
            ContinuePatrol();
        }

        private bool ObserveAndChoose()
        {
            EZoneId zone = AssignedZone.Value;
            if (_zoneMap.FindZone(_body.Position, _body.Floor) != zone) { return false; }
            Memory.Observe(zone, _body.Position, _rules.ObservationRadius, _registry, _layout, _observationRevision);
            if (_digestion != null && _digestion.IsDigesting)
            {
                StopMoving();
                _step = EStep.Digesting;
                return true;
            }
            bool hasMop = _mops.GetBorrowedFrom(Worker) != null;
            ContaminationItem item = null;
            if (_workId is int reservedId)
            {
                item = _registry.FindItem(reservedId);
                if (item == null || !_selector.IsCandidate(item, Worker, zone, hasMop))
                {
                    ReleaseWork();
                    item = null;
                }
            }
            item ??= FindNextTask(zone, hasMop);
            if (item == null) { return false; }
            if (!_workId.HasValue && ShouldYieldTask?.Invoke(this, item) == true)
            {
                if (_step != EStep.Patrolling)
                {
                    StopMoving();
                    _step = EStep.Patrolling;
                }
                return true;
            }
            if (!_workId.HasValue && !_hasOfferedCollaboration && TryOfferCollaboration?.Invoke(this) == true)
            {
                _hasOfferedCollaboration = true;
                _isWaitingDuringWork = true;
                StopMoving();
                _step = EStep.AwaitingResponse;
                return true;
            }

            if (!TryClaimWork(item)) { return false; }
            if (item.Type == EContaminationType.FloorStain && !hasMop) { BorrowMop(); }
            else { Move(EStep.Approaching, _body.Floor, item.Position); }
            return true;
        }

        internal ContaminationItem PeekAvailableTask(EZoneId zone)
        {
            if (_isTalking || _workId.HasValue || _carriedId.HasValue || _digestion?.IsDigesting == true
                || AssignedZone != zone || CurrentZone != zone
                || (_step != EStep.Patrolling && !(_step == EStep.Entering && HasArrived())))
            {
                return null;
            }
            return FindNextTask(zone, _mops.GetBorrowedFrom(Worker) != null);
        }

        private ContaminationItem FindNextTask(EZoneId zone, bool hasMop)
        {
            ContaminationItem item = _selector.FindTask(Worker, zone, _body.Position, hasMop, _rules.ObservationRadius,
                Memory, _unreachableTasks, _mops.AvailableCount > 0);
            if (!hasMop && item?.Type == EContaminationType.FloorStain && _registry.Items.Any(other =>
                other.Zone == zone && other.Type == EContaminationType.FloorStain && other.State == EItemState.OnFloor
                && other.ClaimedBy is EWorkerType coworker && coworker != Worker && coworker != EWorkerType.Player
                && (MinionMemory.IsVisible(other.Position, _body.Position, _rules.ObservationRadius)
                    || Memory.HasObservedTask(other))))
            {
                item = _selector.FindTask(Worker, zone, _body.Position, false, _rules.ObservationRadius,
                    Memory, _unreachableTasks, canBorrowMop: false) ?? item;
            }
            return item;
        }

        private void ContinuePatrol()
        {
            EZoneId zone = AssignedZone.Value;
            bool resurvey = Memory.NeedsResurvey(zone);
            while (!Memory.HasSurveyedDirectly(zone) && _patrol != null && _patrol.Points.Count > 0 && _nextPoint <= _patrol.Points.Count)
            {
                Vector3 point = _patrol.Points[_nextPoint++ % _patrol.Points.Count];
                if (!resurvey && !Memory.HasUnseenSpawnNear(zone, point, _rules.ObservationRadius, _layout)) { continue; }
                Move(EStep.Patrolling, ZoneDisplayName.GetFloor(zone), point);
                return;
            }

            if (Memory.TryFindUnseenPoint(zone, _body.Position, _layout, out Vector3 unseen))
            {
                Move(EStep.Patrolling, ZoneDisplayName.GetFloor(zone), unseen);
                return;
            }

            Memory.FinishPatrol(zone, _layout, _observationRevision);
            if (_mops.GetBorrowedFrom(Worker) != null)
            {
                ContaminationItem item = _selector.FindTask(Worker, zone, _body.Position, false,
                    _rules.ObservationRadius, Memory, _unreachableTasks, canBorrowMop: false);
                if (item != null && TryClaimWork(item) && TryReturnMop(true)) { return; }
            }

            bool needsMop = _stats.CanMop && _mops.AvailableCount == 0
                && _mops.GetBorrowedFrom(Worker) == null && Memory.KnownTaskCount(zone, EContaminationType.FloorStain) > 0;
            FinishRoom(needsMop);
        }

        private void Approach()
        {
            ContaminationItem item = _registry.FindItem(_workId.Value);
            bool hasMop = _mops.GetBorrowedFrom(Worker) != null;
            if (item == null || !_selector.IsCandidate(item, Worker, AssignedZone.Value, hasMop))
            {
                ReleaseWork();
                ResumeAssignedRoom();
                return;
            }
            if (!HasArrived() && !_body.HasMoveFailed) { return; }
            if (_body.HasMoveFailed || !IsNear(item.Position) || _body.Floor != ZoneDisplayName.GetFloor(item.Zone))
            {
                _unreachableTasks[item.Id] = item.Position;
                ReleaseWork();
                ResumeAssignedRoom();
                return;
            }

            if (item.Type == EContaminationType.FloorStain && _registry.TryClaim(item.Id, Worker))
            {
                StopMoving();
                _mopElapsed = 0f;
                _step = EStep.Mopping;
            }
            else if (_digestion != null && _digestion.TryEat(item.Id))
            {
                Memory.NoteRemoved(AssignedZone.Value, item.Id, _observationRevision);
                ReleaseWork();
                StopMoving();
                _step = EStep.Digesting;
            }
            else if (_digestion == null && item.Type != EContaminationType.FloorStain && _registry.TryPickUp(item.Id, Worker))
            {
                Memory.NoteRemoved(AssignedZone.Value, item.Id, _observationRevision);
                _workId = null;
                _carriedId = item.Id;
                UpdateMoveSpeed();
                HomeSlot home = item.HomeSlotId.HasValue ? _zoneMap.FindHomeSlot(item.HomeSlotId.Value) : null;
                WasteBin bin = item.Type == EContaminationType.Trash
                    ? _layout.FindNearestWasteBin(_body.Floor, _body.Position) : null;
                if (home != null)
                {
                    Move(EStep.Carrying, ZoneDisplayName.GetFloor(home.Zone), home.Position);
                }
                else if (bin != null)
                {
                    Move(EStep.Carrying, bin.Floor, bin.Position);
                }
                else
                {
                    // 판정 볼륨의 중심은 바닥보다 높아 NavMesh 탐색 반경 밖일 수 있다.
                    Bounds area = _layout.DischargeArea;
                    Move(EStep.Carrying, EFloor.Floor1, new Vector3(area.center.x, area.min.y, area.center.z));
                }
            }
            else
            {
                ReleaseWork();
                SearchRoom();
            }
        }

        private void Deliver()
        {
            int id = _carriedId.Value;
            ContaminationItem item = _registry.FindItem(id);
            _carriedId = null;
            if (item.HomeSlotId.HasValue && _destinationFloor == _body.Floor && IsNear(_destination))
            {
                _registry.TryPlaceAtHome(id, Worker, item.HomeSlotId.Value);
            }
            if (item.State == EItemState.Carried && !_registry.TryDisposeTrash(id, Worker, _body, _rules.InteractionDistance))
            {
                if (_registry.TryDrop(id, Worker, _body) && _body.HasMoveFailed)
                {
                    _unreachableTasks[id] = item.Position;
                }
            }
            UpdateMoveSpeed();
            ResumeAssignedRoom();
        }

        private void Mop(float deltaTime)
        {
            ContaminationItem target = _registry.FindItem(_workId.Value);
            if (target.ClaimedBy == Worker && IsNear(target.Position))
            {
                _mopElapsed += deltaTime;
                if (_mopElapsed < _stats.MopSeconds) { return; }
                if (_registry.TryCompleteMop(target.Id, Worker))
                {
                    Memory.NoteRemoved(AssignedZone.Value, target.Id, _observationRevision);
                }
            }
            ReleaseWork();
            SearchRoom();
        }

        private void CollectMop()
        {
            ContaminationItem stain = _registry.FindItem(_workId.Value);
            if (stain == null || stain.ClaimedBy != Worker || stain.State != EItemState.OnFloor)
            {
                ReleaseWork();
                ResumeAssignedRoom();
                return;
            }

            if (!HasArrived() && !_body.HasMoveFailed) { return; }
            if (_mops.TryBorrow(Worker, _body))
            {
                Move(EStep.Approaching, ZoneDisplayName.GetFloor(stain.Zone), stain.Position);
            }
            else if (_body.HasMoveFailed)
            {
                _unreachableTasks[stain.Id] = stain.Position;
                ReleaseWork();
                ResumeAssignedRoom();
            }
            else { BorrowMop(); }
        }

        private void BorrowMop()
        {
            ToiletStation station = _mops.FindAvailableStation(_body.Floor);
            if (station == null)
            {
                ReleaseWork();
                ResumeAssignedRoom();
                return;
            }
            Move(EStep.BorrowingMop, station.Floor, station.MopPickupPosition);
        }

        private bool TryReturnMop(bool isReassigning)
        {
            ToiletStation station = _mops.GetBorrowedFrom(Worker);
            if (station == null) { return false; }
            _isReassigningAfterReturn = isReassigning;
            _hasRetriedMopReturn = false;
            Move(EStep.ReturningMop, station.Floor, station.MopPickupPosition);
            return true;
        }

        private void ReturnMop()
        {
            if (!HasArrived() && !_body.HasMoveFailed) { return; }
            if (!_mops.TryReturn(Worker, _body))
            {
                if (!_hasRetriedMopReturn)
                {
                    _hasRetriedMopReturn = true;
                    Move(EStep.ReturningMop, _destinationFloor, _destination);
                    return;
                }
                _mops.Release(Worker);
            }

            if (_isReassigningAfterReturn) { ResumeAssignedRoom(); }
            else { RequestProposal(); }
        }

        private void FinishRoom(bool areMopsUnavailable)
        {
            _areMopsUnavailable = areMopsUnavailable;
            if (!TryReturnMop(false)) { RequestProposal(); }
        }

        private void RequestProposal()
        {
            StopMoving();
            _step = EStep.AwaitingResponse;
            NeedsProposal?.Invoke(this, _areMopsUnavailable);
        }

        public void ReturnToLobby()
        {
            AssignedZone = null;
            Vector3 lobby = _layout.StartPositions.TryGetValue(Worker, out Vector3 position)
                ? position : _layout.PatrolRoutes.Find(route => route.Zone == EZoneId.Floor1Lobby).Points[0];
            Move(EStep.ReturningLobby, EFloor.Floor1, lobby);
        }

        private void Move(EStep step, EFloor floor, Vector3 position)
        {
            _step = step;
            _destination = position;
            _destinationFloor = floor;
            _isMoving = true;
            if (!_isTalking) { _body.MoveTo(floor, position); }
        }

        private bool HasArrived() => _isMoving && _body.Floor == _destinationFloor && _body.HasArrived;

        private bool IsNear(Vector3 position) => MinionMemory.IsVisible(position, _body.Position, _rules.InteractionDistance);

        private void StopMoving()
        {
            _isMoving = false;
            _body.Stop();
        }

        private bool TryClaimWork(ContaminationItem item)
        {
            if (!_registry.TryClaim(item.Id, Worker)) { return false; }
            _workId = item.Id;
            Memory.NoteClaimed(item.Zone, item.Id, _observationRevision);
            return true;
        }

        private void ReleaseWork()
        {
            if (_workId.HasValue) { _registry.TryRelease(_workId.Value, Worker); }
            _workId = null;
            _mopElapsed = 0f;
        }

        private void UpdateMoveSpeed()
        {
            ContaminationItem item = _carriedId.HasValue ? _registry.FindItem(_carriedId.Value) : null;
            float multiplier = item?.Type switch
            {
                EContaminationType.HeavyDebris => _stats.HeavyCarryMultiplier,
                EContaminationType.ClutterItem => _stats.ClutterCarryMultiplier,
                _ => 1f
            };
            float speed = _stats.MoveSpeed * multiplier;
            if (_appliedMoveSpeed == speed) { return; }
            _appliedMoveSpeed = speed;
            _body.SetMoveSpeed(speed);
        }

        private EMinionActivity GetActivity()
        {
            if (_isTalking) { return EMinionActivity.Talking; }
            return _step switch
            {
                EStep.Entering => EMinionActivity.MovingToRoom,
                EStep.Patrolling => EMinionActivity.Surveying,
                EStep.Carrying => EMinionActivity.CarryingWaste,
                EStep.BorrowingMop or EStep.ReturningMop => EMinionActivity.HandlingMop,
                EStep.Approaching or EStep.Mopping or EStep.Digesting => EMinionActivity.Working,
                EStep.AwaitingResponse => EMinionActivity.AwaitingResponse,
                EStep.ReturningLobby => EMinionActivity.ReturningToLobby,
                EStep.Recalling => EMinionActivity.Recalling,
                _ => EMinionActivity.LobbyIdle
            };
        }
    }
}
