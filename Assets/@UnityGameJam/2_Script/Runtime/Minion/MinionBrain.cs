using System;
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
            Digesting, AwaitingResponse, ReturningLobby
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
        private int? _workId;
        private int? _carriedId;
        private float _mopTime;
        private bool _isTalking;
        private bool _isMoving;
        private bool _isReassigningAfterReturn;
        private bool _areMopsUnavailable;
        private EFloor _destinationFloor;
        private Vector3 _destination;

        public EWorkerType Worker => _stats.Worker;
        public EZoneId? AssignedZone { get; private set; }
        public int AssignmentId { get; private set; }
        public MinionMemory Memory { get; } = new MinionMemory();
        public event Action<MinionBrain, bool> NeedsProposal;

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
                or EStep.Carrying or EStep.Digesting or EStep.BorrowingMop);
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

        public void Assign(EZoneId zone)
        {
            _isTalking = false;
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
            if (TryReturnMop(true)) { return; }
            EnterRoom();
        }

        public void AnswerProposal(int assignmentId, EZoneId zone, bool isApproved)
        {
            if (AssignmentId != assignmentId || _step != EStep.AwaitingResponse) { return; }
            if (isApproved) { Assign(zone); }
            else { ReturnToLobby(); }
        }

        public void StopForEnd()
        {
            ReleaseWork();
            _carriedId = null;
            _isTalking = false;
            StopMoving();
        }

        public void Tick(float deltaTime)
        {
            if (_isTalking) { return; }
            switch (_step)
            {
                case EStep.Entering:
                    if (HasArrived()) { SearchRoom(); }
                    break;
                case EStep.Patrolling:
                    if (!ObserveAndChoose() && HasArrived()) { ContinuePatrol(); }
                    break;
                case EStep.Approaching:
                    Approach();
                    break;
                case EStep.Carrying:
                    if (HasArrived()) { Deliver(); }
                    break;
                case EStep.BorrowingMop:
                    if (HasArrived())
                    {
                        if (_mops.TryBorrow(Worker, _body)) { EnterRoom(); }
                        else { BorrowMop(); }
                    }
                    break;
                case EStep.ReturningMop:
                    if (HasArrived() && _mops.TryReturn(Worker, _body))
                    {
                        if (_isReassigningAfterReturn) { EnterRoom(); }
                        else { RequestProposal(); }
                    }
                    break;
                case EStep.Mopping:
                    Mop(deltaTime);
                    break;
                case EStep.Digesting:
                    if (!_digestion.IsDigesting) { SearchRoom(); }
                    break;
                case EStep.ReturningLobby:
                    if (HasArrived()) { StopMoving(); _step = EStep.Lobby; }
                    break;
            }
        }

        private void EnterRoom()
        {
            EZoneId zone = AssignedZone.Value;
            _patrol = _layout.PatrolRoutes.Find(route => route.Zone == zone);
            _nextPoint = 1;
            Vector3 entrance = _patrol != null && _patrol.Points.Count > 0
                ? _patrol.Points[0] : _layout.Zones.Find(area => area.Zone == zone).Areas[0].center;
            Move(EStep.Entering, ZoneDisplayName.GetFloor(zone), entrance);
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
            Memory.Observe(zone, _body.Position, _rules.ObservationRadius, _registry, _layout);
            if (_digestion != null && _digestion.IsDigesting)
            {
                StopMoving();
                _step = EStep.Digesting;
                return true;
            }
            bool hasMop = _mops.GetBorrowedFrom(Worker) != null;
            ContaminationItem item = _selector.FindTask(Worker, zone, _body.Position, hasMop, _rules.ObservationRadius);
            if (item == null) { return false; }
            if (item.Type == EContaminationType.FloorStain && !hasMop)
            {
                BorrowMop();
                return true;
            }

            _workId = item.Id;
            Move(EStep.Approaching, _body.Floor, item.Position);
            return true;
        }

        private void ContinuePatrol()
        {
            if (_patrol != null && _patrol.Points.Count > 0 && _nextPoint <= _patrol.Points.Count)
            {
                Vector3 point = _patrol.Points[_nextPoint++ % _patrol.Points.Count];
                Move(EStep.Patrolling, ZoneDisplayName.GetFloor(AssignedZone.Value), point);
                return;
            }

            Memory.FinishPatrol(AssignedZone.Value, _layout);
            FinishRoom(false);
        }

        private void Approach()
        {
            ContaminationItem item = _registry.FindItem(_workId.Value);
            bool hasMop = _mops.GetBorrowedFrom(Worker) != null;
            if (item == null || !_selector.IsCandidate(item, Worker, AssignedZone.Value, hasMop))
            {
                ReleaseWork();
                SearchRoom();
                return;
            }
            if (!HasArrived()) { return; }
            if (!IsNear(item.Position) || _body.Floor != ZoneDisplayName.GetFloor(item.Zone))
            {
                ReleaseWork();
                SearchRoom();
                return;
            }

            if (item.Type == EContaminationType.FloorStain && _registry.TryClaim(item.Id, Worker))
            {
                StopMoving();
                _mopTime = _stats.MopSeconds;
                _step = EStep.Mopping;
            }
            else if (_digestion != null && _digestion.TryEat(item.Id))
            {
                ReleaseWork();
                StopMoving();
                _step = EStep.Digesting;
            }
            else if (_digestion == null && item.Type != EContaminationType.FloorStain && _registry.TryPickUp(item.Id, Worker))
            {
                _workId = null;
                _carriedId = item.Id;
                UpdateMoveSpeed();
                HomeSlot home = item.HomeSlotId.HasValue ? _zoneMap.FindHomeSlot(item.HomeSlotId.Value) : null;
                if (home != null)
                {
                    Move(EStep.Carrying, ZoneDisplayName.GetFloor(home.Zone), home.Position);
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
            if (item.HomeSlotId.HasValue && IsNear(_destination))
            {
                _registry.TryPlaceAtHome(id, Worker, item.HomeSlotId.Value);
            }
            if (item.State == EItemState.Carried) { _registry.TryDrop(id, Worker, _body); }
            UpdateMoveSpeed();
            EnterRoom();
        }

        private void Mop(float deltaTime)
        {
            ContaminationItem target = _registry.FindItem(_workId.Value);
            if (target.ClaimedBy == Worker && IsNear(target.Position))
            {
                _mopTime -= deltaTime;
                if (_mopTime > 0f) { return; }
                _registry.TryCompleteMop(target.Id, Worker);
            }
            ReleaseWork();
            SearchRoom();
        }

        private void BorrowMop()
        {
            ToiletStation station = _mops.FindAvailableStation(_body.Floor);
            if (station == null) { FinishRoom(true); return; }
            Move(EStep.BorrowingMop, station.Floor, station.MopPickupPosition);
        }

        private bool TryReturnMop(bool isReassigning)
        {
            ToiletStation station = _mops.GetBorrowedFrom(Worker);
            if (station == null) { return false; }
            _isReassigningAfterReturn = isReassigning;
            Move(EStep.ReturningMop, station.Floor, station.MopPickupPosition);
            return true;
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

        private void ReleaseWork()
        {
            if (_workId.HasValue) { _registry.TryRelease(_workId.Value, Worker); }
            _workId = null;
            _mopTime = 0f;
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
            _body.SetMoveSpeed(_stats.MoveSpeed * multiplier);
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
                _ => EMinionActivity.LobbyIdle
            };
        }
    }
}
