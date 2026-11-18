using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 플레이어 이동(Spec Player 규칙 1~3)과 계단 이동(Spec MinionAgent 규칙 8). 클립의 보폭에 규칙의 운반 배율을 반영한다.
    /// 카메라가 북쪽(+Z)을 보고 있어서 입력을 월드 축 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerBody))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float InputDeadZone = 0.01f;

        [Header("입력")]
        [SerializeField] private InputActionReference _moveAction;

        [Header("표시")]
        [SerializeField] private CharacterAnimatorDriver _animatorDriver;

        private CharacterController _controller;
        private PlayerBody _body;
        private IMatchSession _session;
        private CarryAnimationState _carry;
        private InputAction _sprint;
        private float _baseSpeed;
        private EWorkerType _talkWorker;
        private Transform _talkPartner;
        private StairDirectory _stairs;
        private ViewSettingsSO _settings;
        private FloorCameraFollow _camera;
        private float _transitElapsed;
        private Vector3 _transitTarget;
        private Vector3? _landing;

        public bool IsInStairTransit { get; private set; }
        public bool IsInConversation => _talkPartner != null;
        internal bool IsTalkingToHellhound => IsInConversation && _talkWorker == EWorkerType.Hellhound;
        public bool IsHandling => _animatorDriver != null && _animatorDriver.IsHandling;


        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _body = GetComponent<PlayerBody>();
            _sprint = _moveAction?.action?.actionMap?.FindAction("Sprint");
        }

        private void OnEnable()
        {
            if (_moveAction != null) { _moveAction.action.Enable(); }
            _sprint?.Enable();
        }

        public void Initialize(IMatchSession session, StairDirectory stairs, ViewSettingsSO settings, FloorCameraFollow camera = null)
        {
            _session = session;
            _baseSpeed = session.PlayerMoveSpeed;
            _carry = new CarryAnimationState(EWorkerType.Player, BeginHandling);
            _session.ItemChanged += _carry.Observe;
            _session.MatchStateChanged += OnMatchStateChanged;
            _stairs = stairs;
            _settings = settings;
            _camera = camera;
        }

        private void Update()
        {
            if (_session == null || _session.MatchState != EMatchState.Running || _moveAction == null) { return; }
            RefreshMop();
            if (IsHandling)
            {
                _controller.SimpleMove(Vector3.zero);
                return;
            }
            if (UpdateTalk()) { return; }

            if (IsInStairTransit)
            {
                UpdateStairTransit();
                return;
            }

            Vector2 input = _moveAction.action.ReadValue<Vector2>();
            bool hasInput = input.sqrMagnitude > InputDeadZone;

            // 작업 중에는 움직이지 않고, 이동 입력이 오면 작업을 멈춘다(U205 임시).
            if (_session.IsPlayerWorking)
            {
                if (hasInput) { _session.CancelWork(); }
                _controller.SimpleMove(Vector3.zero);
                UpdatePose(0f);
                return;
            }

            Vector3 direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) { direction.Normalize(); }
            bool running = _sprint != null && _sprint.IsPressed() &&
                UnitMotionTuning.CanSprint(_carry.CarriedType, _carry.IsHoldingMop);
            float speed = _carry.GetMoveSpeed(_animatorDriver, _baseSpeed, _session.PlayerMoveSpeed, running);
            _controller.SimpleMove(direction * speed);

            if (hasInput)
            {
                Face(transform, direction);
                if (TryStartStairTransit()) { return; }
            }
            UpdatePose(new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude, running);
        }

        private bool TryStartStairTransit()
        {
            float radius = _settings.StairApproachRadius;
            if (_landing.HasValue)
            {
                if (PlayerInteractionSelector.DistanceXZ(_landing.Value, transform.position) <= radius) { return false; }
                _landing = null;
            }
            if (!_stairs.TryFindExit(_body.Floor, transform.position, radius, out _transitTarget)) { return false; }
            _transitElapsed = 0f;
            IsInStairTransit = true;
            UpdatePose(0f);
            return true;
        }

        private void UpdateStairTransit()
        {
            _transitElapsed += Time.deltaTime;
            if (_transitElapsed < _settings.StairTransitSeconds) { return; }

            _controller.enabled = false; // CharacterController는 꺼야 위치를 직접 옮길 수 있다
            transform.position = _transitTarget;
            _landing = _transitTarget;
            _controller.enabled = true;
            _camera?.SnapToTarget();
            IsInStairTransit = false;
        }

        public void RefreshMop()
        {
            _carry.ObserveMop(_session.IsPlayerHoldingMop);
        }

        public void FaceForTalk(EWorkerType worker, Transform partner)
        {
            _talkWorker = worker;
            _talkPartner = partner;
        }

        internal void EndHellhoundTalk()
        {
            if (IsTalkingToHellhound) { _talkPartner = null; }
        }

        private bool UpdateTalk()
        {
            if (_talkPartner == null) { return false; }
            foreach (MinionStatus status in _session.Minions)
            {
                if (status.Minion != _talkWorker || (status.Activity != EMinionActivity.Talking && !IsTalkingToHellhound)) { continue; }
                Vector3 direction = _talkPartner.position - transform.position;
                Face(transform, direction);
                if (status.Activity is EMinionActivity.Talking or EMinionActivity.LobbyIdle) { Face(_talkPartner, -direction); }
                _controller.SimpleMove(Vector3.zero);
                _animatorDriver?.SetPose(EPose.Talk, holdingMop: _carry.IsHoldingMop);
                return true;
            }
            _talkPartner = null;
            return false;
        }

        private void Face(Transform actor, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.000001f) { return; }
            actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(direction), _settings.PlayerTurnSpeed * Time.deltaTime);
        }

        private void BeginHandling(bool pickingUp, bool heavy)
        {
            if (_session.MatchState == EMatchState.Running) { _animatorDriver?.BeginHandling(pickingUp, heavy); }
        }

        private void OnMatchStateChanged(EMatchState state)
        {
            if (state != EMatchState.Running) { _animatorDriver?.SetPose(EPose.Idle); }
        }

        private void UpdatePose(float speed, bool running = false)
        {
            if (_animatorDriver == null) { return; }
            EMinionActivity activity = _session.IsPlayerWorking ? EMinionActivity.Working : EMinionActivity.LobbyIdle;
            EPose pose = MinionPoseResolver.Resolve(speed, activity, _carry.CarriedType, _carry.IsHoldingMop, false);
            _animatorDriver.SetPose(pose, speed, running, _carry.CarriedType, _carry.IsHoldingMop);
        }

        private void OnDestroy()
        {
            if (_session == null) { return; }
            _session.ItemChanged -= _carry.Observe;
            _session.MatchStateChanged -= OnMatchStateChanged;
        }
    }
}
