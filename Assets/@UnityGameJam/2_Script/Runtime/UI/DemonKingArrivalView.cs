using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    [RequireComponent(typeof(FacilityHud))]
    [DisallowMultipleComponent]
    public sealed class DemonKingArrivalView : MonoBehaviour
    {
        [SerializeField] private GameObject _demonKingPrefab;
        [SerializeField] private ResultPanel _resultPanel;
        [SerializeField] private GameObject[] _gameplayPanels;
        [SerializeField, Min(0f)] private float _arrivalSeconds = 1f;
        [SerializeField, Min(0.01f)] private float _walkSeconds = 0.72f;
        [SerializeField] private Vector3 _arrivalOffset = new Vector3(1.8f, 0f, 1.4f);

        private FacilityHud _hud;
        private MatchBootstrap _bootstrap;
        private IMatchSession _session;
        private GameObject _demonKing;
        private CharacterAnimatorDriver _kingAnimator;
        private Vector3 _walkFrom;
        private Vector3 _walkTo;
        private float _walkStartedAt;
        private bool _walking;
        private MatchResult _pendingResult;
        private float _revealAt;
        private bool _hasShownResult;

        private void Awake()
        {
            _hud = GetComponent<FacilityHud>();
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap == null) { return; }
            _bootstrap.MatchStarted += Bind;
            if (_bootstrap.Session != null) { Bind(_bootstrap); }
        }

        private void OnDisable()
        {
            if (_bootstrap != null) { _bootstrap.MatchStarted -= Bind; }
            if (_session != null) { _session.MatchStateChanged -= OnStateChanged; }
            _session = null;
        }

        private void Bind(MatchBootstrap bootstrap)
        {
            if (_session != null) { _session.MatchStateChanged -= OnStateChanged; }
            _session = bootstrap.Session;
            _session.MatchStateChanged += OnStateChanged;
            OnStateChanged(_session.MatchState);
        }

        private void OnDestroy()
        {
            if (_demonKing != null) { Destroy(_demonKing); }
        }

        private void OnStateChanged(EMatchState state)
        {
            if (state != EMatchState.DemonKingArrived && state != EMatchState.Resolved) { return; }
            if (_demonKing == null)
            {
                _hud.EndMatch();
                Vector3 player = _bootstrap.Player.Position;
                Vector3 position = FindArrivalPosition(player);
                Vector3 facing = Vector3.ProjectOnPlane(player - position, Vector3.up);
                Quaternion rotation = facing.sqrMagnitude > 0f ? Quaternion.LookRotation(facing) : Quaternion.identity;
                _demonKing = Instantiate(_demonKingPrefab, position, rotation, _bootstrap.transform);
                _kingAnimator = _demonKing.GetComponent<CharacterAnimatorDriver>();
                if (state == EMatchState.DemonKingArrived) { StartArrivalWalk(position, player); }
                // 이미 끝난 판에 연결됐으면 등장 대기 없이 결과를 연다.
                _revealAt = Time.unscaledTime + (state == EMatchState.DemonKingArrived ? Mathf.Max(_arrivalSeconds, _walkSeconds) : 0f);
            }
            if (state == EMatchState.Resolved && !_hasShownResult) { _pendingResult = _session.Result; }
        }

        private void LateUpdate()
        {
            if (_walking)
            {
                float progress = (Time.unscaledTime - _walkStartedAt) / _walkSeconds;
                _demonKing.transform.position = Vector3.Lerp(_walkFrom, _walkTo, progress);
                if (progress >= 1f)
                {
                    _walking = false;
                    _kingAnimator.SetPose(EPose.Idle);
                }
            }
            // 실제 판은 마왕 도착과 결과 확정을 같은 틱에 알린다.
            if (_pendingResult == null || Time.unscaledTime < _revealAt) { return; }
            foreach (GameObject panel in _gameplayPanels) { panel.SetActive(false); }
            _resultPanel.Show(_pendingResult, _session.FacilityThreshold, _session.ZoneThreshold);
            _hasShownResult = true;
            _pendingResult = null;
        }

        private void StartArrivalWalk(Vector3 destination, Vector3 player)
        {
            if (_kingAnimator == null || _walkSeconds <= 0f) { return; }
            float strideSpeed = _kingAnimator.GetLocomotionSpeed(0f);
            if (strideSpeed <= 0f) { return; }
            float stride = strideSpeed * _walkSeconds;
            Vector3 forward = _demonKing.transform.forward;
            _walkTo = destination;
            _walkFrom = destination - forward * stride;
            if (NavMesh.SamplePosition(destination, out NavMeshHit end, 0.25f, NavMesh.AllAreas) &&
                NavMesh.Raycast(end.position, _walkFrom, out NavMeshHit edge, NavMesh.AllAreas))
            {
                _walkFrom = edge.position;
                // 등장점이 벽에 붙어 있으면 플레이어 쪽의 비어 있는 통로를 사용한다.
                float approach = Mathf.Clamp(Vector3.Dot(player - destination, forward) - 0.75f, 0f, stride);
                float missingDistance = Mathf.Max(0f, stride - Vector3.Distance(_walkFrom, destination));
                _walkTo += forward * Mathf.Min(approach, missingDistance);
            }
            float distance = Vector3.Distance(_walkFrom, _walkTo);
            if (distance < 0.01f) { return; }
            _demonKing.transform.position = _walkFrom;
            _walkStartedAt = Time.unscaledTime;
            _walking = true;
            _kingAnimator.SetPose(EPose.Walk, distance / _walkSeconds);
        }

        private Vector3 FindArrivalPosition(Vector3 player)
        {
            const float sampleRadius = 1f;
            if (!NavMesh.SamplePosition(player, out NavMeshHit start, sampleRadius, NavMesh.AllAreas))
            {
                return player + _arrivalOffset;
            }
            // 모서리나 벽 옆에서는 같은 통로 안에 있는 다른 방향을 고른다.
            for (int turn = 0; turn < 4; turn++)
            {
                Vector3 candidate = start.position + Quaternion.Euler(0f, 90f * turn, 0f) * _arrivalOffset;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)
                    && !NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return start.position;
        }
    }
}
