using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 플레이어 이동(Spec Player 규칙 1~3). 속도는 규칙이 계산한 PlayerMoveSpeed를 쓴다.
    /// 카메라가 북쪽(+Z)을 보고 있어서 입력을 월드 축 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float InputDeadZone = 0.01f;

        [Header("입력")]
        [SerializeField] private InputActionReference _moveAction;

        [Header("회전")]
        [Tooltip("이동 방향으로 도는 속도(도/초)")]
        [SerializeField] private float _turnSpeed = 720f;

        private CharacterController _controller;
        private IMatchSession _session;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (_moveAction != null) { _moveAction.action.Enable(); }
        }

        public void Initialize(IMatchSession session)
        {
            _session = session;
        }

        private void Update()
        {
            if (_session == null || _session.MatchState != EMatchState.Running || _moveAction == null) { return; }

            Vector2 input = _moveAction.action.ReadValue<Vector2>();
            bool hasInput = input.sqrMagnitude > InputDeadZone;

            // 작업 중에는 움직이지 않고, 이동 입력이 오면 작업을 멈춘다(U205 임시).
            if (_session.IsPlayerWorking)
            {
                if (hasInput) { _session.CancelWork(); }
                _controller.SimpleMove(Vector3.zero);
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
        }
    }
}
