using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 졸개 몸(Spec MinionAgent 규칙 1~5). 규칙이 MoveTo(층, 위치)만 부르면 계단을 거쳐 길을 찾는다.
    /// 층은 X축으로 떨어진 별도 NavMesh라서, 계단 출입 지점에서 계단 시간만큼 기다린 뒤 다음 층 계단으로 Warp한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class MinionAgent : MonoBehaviour, IMinionBody
    {
        private const float StairReachDistance = 0.4f;
        private const float ArrivalTolerance = 0.1f;
        private const float SampleRadius = 1f;
        private const float RunDistance = 0.6f;

        private enum EPhase { Idle, WalkingToStair, InTransit, WalkingToDestination }

        [SerializeField] private EWorkerType _worker;

        private readonly Queue<EFloor> _floorsToPass = new Queue<EFloor>();
        private NavMeshAgent _agent;
        private CharacterAnimatorDriver _animatorDriver;
        private CarryAnimationState _carry;
        private float _baseSpeed;
        private float _ruleSpeed;
        private bool _pausedForHandling;
        private StairDirectory _stairs;
        private IReadOnlyList<FloorArea> _floors = new List<FloorArea>();
        private float _stairSeconds;
        private EPhase _phase = EPhase.Idle;
        private EFloor _floor;
        private EFloor _targetFloor;
        private Vector3 _destination;
        private float _transitTimer;
        private bool _isRouteChanged;
        private bool _isStopRequested;

        public EWorkerType Worker => _worker;
        public Vector3 Position => transform.position;
        public EFloor Floor => _floor;
        public float CurrentSpeed => _agent != null && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
        public bool IsInStairTransit => _phase == EPhase.InTransit;
        public EContaminationType? CarriedType => _carry.CarriedType;
        public bool IsHandling => _animatorDriver != null && _animatorDriver.IsHandling;
        public bool IsRunning
        {
            get
            {
                if (_phase != EPhase.WalkingToDestination && _phase != EPhase.WalkingToStair) { return false; }
                if (_worker == EWorkerType.Hellhound) { return true; }
                return _worker != EWorkerType.Slime && !_carry.CarriedType.HasValue && !_carry.IsHoldingMop
                    && _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance > RunDistance;
            }
        }

        public bool HasArrived
        {
            get
            {
                if (IsHandling) { return false; }
                if (_phase == EPhase.Idle) { return true; }
                if (_phase != EPhase.WalkingToDestination) { return false; }
                return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + ArrivalTolerance;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animatorDriver = GetComponent<CharacterAnimatorDriver>();
            _carry = new CarryAnimationState(_worker, BeginHandling);
            _ruleSpeed = _agent.speed;
        }

        public void Initialize(StairDirectory stairs, IReadOnlyList<FloorArea> floors, float stairSeconds)
        {
            _stairs = stairs;
            _floors = floors;
            _stairSeconds = stairSeconds;
            _floor = FindFloor(transform.position);
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas)) { _agent.Warp(hit.position); }
        }

        public void MoveTo(EFloor floor, Vector3 destination)
        {
            _targetFloor = floor;
            _destination = destination;
            _isStopRequested = false;
            if (_phase == EPhase.InTransit)
            {
                _isRouteChanged = true; // 계단 대기를 마친 뒤 새 목적지로 다시 짠다
                return;
            }
            StartRoute();
        }

        public void Stop()
        {
            if (_phase == EPhase.InTransit)
            {
                _isStopRequested = true;
                return;
            }
            ClearRoute();
        }

        public void SetMoveSpeed(float speed)
        {
            if (_baseSpeed <= 0f) { _baseSpeed = speed; }
            _ruleSpeed = speed;
            UpdateStrideSpeed();
        }

        internal void ObserveItem(ItemSnapshot item) => _carry.Observe(item);
        internal void ObserveMop(bool holding) => _carry.ObserveMop(holding);

        private void BeginHandling(bool pickingUp, bool heavy)
        {
            if (_animatorDriver == null || _animatorDriver.BeginHandling(pickingUp, heavy) <= 0f) { return; }
            _pausedForHandling = true;
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        private void UpdateStrideSpeed()
        {
            float baseSpeed = _baseSpeed > 0f ? _baseSpeed : _ruleSpeed;
            _agent.speed = _carry.GetMoveSpeed(_animatorDriver, baseSpeed, _ruleSpeed, IsRunning);
        }

        private void Update()
        {
            if (IsHandling) { return; }
            if (_pausedForHandling)
            {
                _pausedForHandling = false;
                if (_agent.isOnNavMesh && _phase != EPhase.InTransit) { _agent.isStopped = false; }
            }
            UpdateStrideSpeed();
            switch (_phase)
            {
                case EPhase.WalkingToStair:
                    if (!_agent.pathPending && _agent.remainingDistance <= StairReachDistance)
                    {
                        _agent.isStopped = true;
                        _transitTimer = _stairSeconds;
                        _phase = EPhase.InTransit;
                    }
                    break;
                case EPhase.InTransit:
                    _transitTimer -= Time.deltaTime;
                    if (_transitTimer <= 0f) { CrossStair(); }
                    break;
            }
        }

        private void StartRoute()
        {
            _floorsToPass.Clear();
            foreach (EFloor floor in StairRoutePlanner.FloorsToPass(_floor, _targetFloor)) { _floorsToPass.Enqueue(floor); }

            if (_floorsToPass.Count == 0)
            {
                GoToDestination();
                return;
            }
            if (_stairs == null || !_stairs.TryGet(_floor, out Vector3 stair))
            {
                Debug.LogWarning($"[MinionAgent] {_worker}: {_floor} 계단이 없어 층을 옮길 수 없습니다.", this);
                ClearRoute();
                return;
            }
            _agent.isStopped = IsHandling;
            _agent.SetDestination(stair);
            _phase = EPhase.WalkingToStair;
        }

        private void CrossStair()
        {
            EFloor next = _floorsToPass.Dequeue();
            if (_stairs.TryGet(next, out Vector3 entry))
            {
                _agent.Warp(entry);
                _floor = next;
            }

            if (_isStopRequested)
            {
                _isStopRequested = false;
                ClearRoute();
                return;
            }
            if (_isRouteChanged)
            {
                _isRouteChanged = false;
                StartRoute();
                return;
            }
            if (_floorsToPass.Count > 0)
            {
                _transitTimer = _stairSeconds; // 중간 층 계단에서 이어서 오른다(내려간다)
                return;
            }
            GoToDestination();
        }

        private void GoToDestination()
        {
            if (!NavMesh.SamplePosition(_destination, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[MinionAgent] {_worker}: 목적지 {_destination}이(가) NavMesh 밖이라 멈춥니다.", this);
                ClearRoute();
                return;
            }
            _agent.isStopped = IsHandling;
            _agent.SetDestination(hit.position);
            _phase = EPhase.WalkingToDestination;
        }

        private void ClearRoute()
        {
            _floorsToPass.Clear();
            _isRouteChanged = false;
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = IsHandling;
            }
            _phase = EPhase.Idle;
        }

        private EFloor FindFloor(Vector3 position)
        {
            foreach (FloorArea floor in _floors)
            {
                if (FacilityLayoutValidator.ContainsXZ(floor.Area, position)) { return floor.Floor; }
            }
            return EFloor.Floor1;
        }
    }
}
