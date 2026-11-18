using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 플레이어 이동(Spec Player 규칙 1~3)과 계단 이동(Spec MinionAgent 규칙 8). 속도는 규칙이 계산한 PlayerMoveSpeed를 쓴다.
    /// 카메라가 북쪽(+Z)을 보고 있어서 입력을 월드 축 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerBody))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float InputDeadZone = 0.01f;

        [Header("입력")]
        [SerializeField] private InputActionReference _moveAction;
        [Tooltip("계단에서 아래층 (U208 임시: 1)")]
        [SerializeField] private InputActionReference _floorDownAction;
        [Tooltip("계단에서 위층 (U208 임시: 2)")]
        [SerializeField] private InputActionReference _floorUpAction;

        [Header("회전")]
        [Tooltip("이동 방향으로 도는 속도(도/초)")]
        [SerializeField] private float _turnSpeed = 720f;

        [Header("표시")]
        [SerializeField] private CharacterAnimatorDriver _animatorDriver;

        private CharacterController _controller;
        private PlayerBody _body;
        private IMatchSession _session;
        private StairDirectory _stairs;
        private ViewSettingsSO _settings;
        private float _transitTimer;
        private EFloor _transitTarget;

        public bool IsInStairTransit { get; private set; }

        public bool IsNearStair => _stairs != null && _settings != null && _stairs.TryGet(_body.Floor, out Vector3 entry)
            && PlayerInteractionSelector.DistanceXZ(entry, transform.position) <= _settings.InteractRadius;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _body = GetComponent<PlayerBody>();
        }

        private void OnEnable()
        {
            if (_moveAction != null) { _moveAction.action.Enable(); }
            if (_floorDownAction != null) { _floorDownAction.action.Enable(); }
            if (_floorUpAction != null) { _floorUpAction.action.Enable(); }
        }

        public void Initialize(IMatchSession session, StairDirectory stairs, ViewSettingsSO settings)
        {
            _session = session;
            _stairs = stairs;
            _settings = settings;
        }

        private void Update()
        {
            if (_session == null || _session.MatchState != EMatchState.Running || _moveAction == null) { return; }

            if (IsInStairTransit)
            {
                UpdateStairTransit();
                return;
            }
            if (TryStartStairTransit()) { return; }

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
            _controller.SimpleMove(direction * _session.PlayerMoveSpeed);

            if (hasInput)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
            }
            UpdatePose(new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude);
        }

        private bool TryStartStairTransit()
        {
            int direction = 0;
            if (_floorUpAction != null && _floorUpAction.action.WasPressedThisFrame()) { direction = 1; }
            else if (_floorDownAction != null && _floorDownAction.action.WasPressedThisFrame()) { direction = -1; }
            if (direction == 0 || !IsNearStair) { return false; }

            var target = (EFloor)((int)_body.Floor + direction);
            if (!_stairs.TryGet(target, out _)) { return false; } // 맨 아래층·맨 위층

            if (_session.IsPlayerWorking) { _session.CancelWork(); }
            _transitTarget = target;
            _transitTimer = _settings.StairTransitSeconds;
            IsInStairTransit = true;
            UpdatePose(0f);
            return true;
        }

        private void UpdateStairTransit()
        {
            _transitTimer -= Time.deltaTime;
            if (_transitTimer > 0f) { return; }

            _stairs.TryGet(_transitTarget, out Vector3 entry);
            _controller.enabled = false; // CharacterController는 꺼야 위치를 직접 옮길 수 있다
            transform.position = entry;
            _controller.enabled = true;
            IsInStairTransit = false;
        }

        private void UpdatePose(float speed)
        {
            if (_animatorDriver == null) { return; }
            EContaminationType? carried = null;
            if (_session.PlayerCarriedItemId.HasValue)
            {
                foreach (ItemSnapshot item in _session.Items)
                {
                    if (item.ItemId == _session.PlayerCarriedItemId.Value) { carried = item.Type; }
                }
            }
            EMinionActivity activity = _session.IsPlayerWorking ? EMinionActivity.Working : EMinionActivity.LobbyIdle;
            _animatorDriver.SetPose(MinionPoseResolver.Resolve(speed, activity, carried, _session.IsPlayerHoldingMop, false));
        }
    }
}
