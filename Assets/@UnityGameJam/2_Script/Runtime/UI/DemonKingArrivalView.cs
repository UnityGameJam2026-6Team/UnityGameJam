using System.Collections.Generic;
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
        private int _kingStepPhase = -1;
        private int _dogStepPhase = -1;
        private int _playerStepPhase = -1;
        private MatchResult _pendingResult;
        private float _revealAt;
        private bool _hasShownResult;
        private bool _entranceRequested;
        private FloorAuthoring _stage;
        private ViewSettingsSO.EndingSettings _ending;
        private CharacterAnimatorDriver _stagePlayer;
        private CharacterAnimatorDriver _stageDog;
        private readonly List<(GameObject Original, bool WasVisible, GameObject Actor)> _stageMinions = new();
        private GameObject _playerVisual;
        private GameObject _dogVisual;
        private bool _playerWasVisible;
        private bool _dogWasVisible;
        private Quaternion _leftClosed;
        private Quaternion _rightClosed;
        private float _endingStartedAt;
        private bool _isSuccess;
        private Transform _trash;
        private Vector3 _throwFrom;
        private Quaternion _throwRotation;
        private bool _trashReleased;
        private bool _hasImpact;

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
            ReleaseEntrance();
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
            if (_hasShownResult) { return; }
            if (state == EMatchState.DemonKingArrived)
            {
                _hud.EndMatch();
                _stage = _bootstrap.ArrivalFloor;
                _entranceRequested = _stage != null && _stage.ArrivalStage != null &&
                    _stage.ArrivalLeftDoor != null && _stage.ArrivalRightDoor != null && _bootstrap.ViewCamera != null;
                if (!_entranceRequested) { BeginLocalArrival(true); }
            }
            else if (state == EMatchState.Resolved)
            {
                _pendingResult = _session.Result;
                if (_demonKing != null) { return; }
                if (_entranceRequested && _pendingResult != null && BeginEntrance(_pendingResult.IsSuccess)) { return; }
                if (!_entranceRequested) { _hud.EndMatch(); }
                BeginLocalArrival(_entranceRequested);
            }
        }

        private void BeginLocalArrival(bool isAnimated)
        {
            Vector3 player = _bootstrap.Player.Position;
            Vector3 position = FindArrivalPosition(player);
            _demonKing = Instantiate(_demonKingPrefab, position, Facing(position, player), _bootstrap.transform);
            _kingAnimator = _demonKing.GetComponent<CharacterAnimatorDriver>();
            if (isAnimated) { StartArrivalWalk(position, player); }
            _revealAt = Time.unscaledTime + (isAnimated ? Mathf.Max(_arrivalSeconds, _walkSeconds) : 0f);
        }

        private void LateUpdate()
        {
            if (_ending != null && !_hasShownResult)
            {
                PlayStep(_kingAnimator, ESfxCue.HeavyStep, ref _kingStepPhase);
                PlayStep(_stageDog, ESfxCue.HoundStep, ref _dogStepPhase);
                PlayStep(_stagePlayer, ESfxCue.LightStep, ref _playerStepPhase);
                UpdateEntrance(Time.unscaledTime - _endingStartedAt);
            }
            if (_walking)
            {
                PlayStep(_kingAnimator, ESfxCue.HeavyStep, ref _kingStepPhase);
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
            _resultPanel.RecordMatch(_pendingResult, _bootstrap);
            _resultPanel.Show(_pendingResult, _session.FacilityThreshold, _session.ZoneThreshold);
            _hasShownResult = true;
            _pendingResult = null;
        }

        private static void PlayStep(CharacterAnimatorDriver actor, ESfxCue cue, ref int previousPhase)
        {
            if (actor == null || actor.CurrentPose != EPose.Walk || actor.CurrentClip == null || actor.CurrentClip.length <= 0f)
            {
                previousPhase = -1;
                return;
            }

            int phase = Mathf.FloorToInt(actor.ClipTime * 2f / actor.CurrentClip.length);
            if (phase == previousPhase) { return; }
            previousPhase = phase;
            // 현관 컷의 관객 위치는 마감 직전 플레이어의 층과 무관하다.
            GameSfxPlayer.Play(cue);
        }

        private bool BeginEntrance(bool isSuccess)
        {
            ArtSlot player = _bootstrap.Player.GetComponentInChildren<ArtSlot>(true);
            ArtSlot dog = null;
            foreach (MinionAgent minion in _bootstrap.Minions)
            {
                if (minion.Worker != EWorkerType.Hellhound) { continue; }
                dog = minion.GetComponentInChildren<ArtSlot>(true);
                break;
            }
            ViewSettingsSO.EndingSettings settings = _bootstrap.ViewSettings.Ending;
            ArtSlot trash = settings.ThrowTrash != null ? settings.ThrowTrash.GetComponentInChildren<ArtSlot>(true) : null;
            if (player == null || dog == null || settings.DogGreeting == null || (!isSuccess && trash == null)) { return false; }

            _ending = settings;
            _isSuccess = isSuccess;
            _endingStartedAt = Time.unscaledTime;
            float verdictSeconds = isSuccess ? _ending.SuccessSeconds :
                _ending.ThrowLookSeconds + _ending.ThrowReleaseAt + _ending.ThrowFlightSeconds +
                _ending.ImpactHoldSeconds + _ending.ImpactRecoverySeconds;
            _revealAt = _endingStartedAt + _ending.DoorSeconds + _ending.WalkSeconds + _ending.FaceoffSeconds + verdictSeconds;
            _leftClosed = _stage.ArrivalLeftDoor.localRotation;
            _rightClosed = _stage.ArrivalRightDoor.localRotation;
            _playerVisual = player.gameObject;
            _dogVisual = dog.gameObject;
            _playerWasVisible = _playerVisual.activeSelf;
            _dogWasVisible = _dogVisual.activeSelf;
            _stagePlayer = CreateStageActor(player, PlayerStart, EWorkerType.Player);
            _stagePlayer.gameObject.SetActive(false);
            _stageDog = CreateStageActor(dog, _ending.DogStart, EWorkerType.Hellhound);
            _playerVisual.SetActive(false);
            _dogVisual.SetActive(false);
            _demonKing = Instantiate(_demonKingPrefab, World(_ending.KingStart), _stage.ArrivalStage.rotation, _bootstrap.transform);
            _kingAnimator = _demonKing.GetComponent<CharacterAnimatorDriver>();
            foreach (GameObject panel in _gameplayPanels) { panel.SetActive(false); }
            if (!isSuccess)
            {
                PrepareStageMinions();
                Vector3 position = World(PlayerStart);
                Quaternion rotation = Facing(position, World(_ending.KingStop));
                Vector3 ground = position + rotation * new Vector3(0.24f, 0f, 0.67f);
                _trash = Instantiate(trash.gameObject, ground, rotation, _bootstrap.transform).transform;
                _trash.gameObject.SetActive(false);
            }
            UpdateEntrance(0f);
            return true;
        }

        private CharacterAnimatorDriver CreateStageActor(ArtSlot source, Vector3 offset, EWorkerType worker)
        {
            GameObject actor = Instantiate(source.gameObject, World(offset), _stage.ArrivalStage.rotation, _bootstrap.transform);
            actor.SetActive(true);
            var animator = actor.AddComponent<CharacterAnimatorDriver>();
            animator.SetMotionTuning(_bootstrap.ViewSettings.GetMotionTuning(worker));
            return animator;
        }

        private void PrepareStageMinions()
        {
            foreach (MinionAgent minion in _bootstrap.Minions)
            {
                if (minion.Worker == EWorkerType.Hellhound) { continue; }
                if (_stageMinions.Count >= _ending.FailureMinionOffsets.Length) { break; }
                ArtSlot art = minion.GetComponentInChildren<ArtSlot>(true);
                if (art == null) { continue; }
                Vector3 offset = PlayerStart + _ending.FailureMinionOffsets[_stageMinions.Count];
                GameObject actor = CreateStageActor(art, offset, minion.Worker).gameObject;
                actor.transform.rotation = Facing(actor.transform.position, _stagePlayer.transform.position);
                actor.SetActive(false);
                GameObject original = art.gameObject;
                _stageMinions.Add((original, original.activeSelf, actor));
                original.SetActive(false);
            }
        }

        private Vector3 PlayerStart => _isSuccess ? _ending.PlayerStart : _ending.FailurePlayerStart;

        private void UpdateEntrance(float elapsed)
        {
            float door = Ease(elapsed / _ending.DoorSeconds);
            _stage.ArrivalLeftDoor.localRotation = _leftClosed * Quaternion.Euler(0f, door * _ending.DoorAngle, 0f);
            _stage.ArrivalRightDoor.localRotation = _rightClosed * Quaternion.Euler(0f, -door * _ending.DoorAngle, 0f);

            float walkTime = elapsed - _ending.DoorSeconds;
            Vector3 kingStart = World(_ending.KingStart);
            Vector3 kingStop = World(_ending.KingStop);
            Vector3 playerStart = World(PlayerStart);
            Vector3 kingPosition = Vector3.Lerp(kingStart, kingStop, walkTime / _ending.WalkSeconds);
            Quaternion kingFacing = Facing(kingStart, kingStop);
            float faceTime = walkTime - _ending.WalkSeconds;
            Quaternion kingToPlayer = Facing(kingStop, playerStart);
            kingFacing = Quaternion.Slerp(kingFacing, kingToPlayer, Ease(faceTime / _ending.FaceoffSeconds));
            _demonKing.transform.SetPositionAndRotation(kingPosition, kingFacing);
            bool isWalking = walkTime >= 0f && faceTime < 0f;
            _kingAnimator.SetPose(isWalking ? EPose.Walk : EPose.Idle, Vector3.Distance(kingStart, kingStop) / _ending.WalkSeconds);
            if (faceTime >= 0f && !_stagePlayer.gameObject.activeSelf)
            {
                _stagePlayer.gameObject.SetActive(true);
                foreach (var minion in _stageMinions) { minion.Actor.SetActive(true); }
                if (_trash != null) { _trash.gameObject.SetActive(true); }
            }
            _stagePlayer.transform.rotation = Quaternion.Slerp(_stage.ArrivalStage.rotation, Facing(playerStart, kingStop),
                Ease(faceTime / _ending.FaceoffSeconds));
            UpdateDog(walkTime, kingPosition);

            if (walkTime < 0f) { ShowShot(_ending.Door, elapsed / _ending.DoorSeconds); }
            else if (faceTime < 0f) { ShowShot(_ending.Entrance, walkTime / _ending.WalkSeconds); }
            else if (faceTime < _ending.FaceoffSeconds)
            {
                if (_isSuccess) { ShowShot(_ending.Faceoff, faceTime / _ending.FaceoffSeconds); }
                else { ShowShot(_ending.Throw, 0f); }
            }
            else if (_isSuccess)
            {
                float resultTime = faceTime - _ending.FaceoffSeconds;
                Vector3 step = _stagePlayer.transform.forward * _ending.SuccessStep;
                _stagePlayer.transform.position = playerStart + step * Mathf.Clamp01(resultTime / _ending.SuccessStepSeconds);
                _stagePlayer.SetPose(resultTime < _ending.SuccessStepSeconds ? EPose.Walk : EPose.Idle,
                    _ending.SuccessStep / _ending.SuccessStepSeconds);
                ShowShot(_ending.Success, resultTime / _ending.SuccessSeconds);
            }
            else { UpdateFailure(faceTime - _ending.FaceoffSeconds, kingStop, kingToPlayer); }
        }

        private void UpdateDog(float walkTime, Vector3 kingPosition)
        {
            float runTime = walkTime - _ending.DogRunAt;
            Vector3 start = World(_ending.DogStart);
            Vector3 stop = World(_ending.DogStop);
            Vector3 position = Vector3.Lerp(start, stop, runTime / _ending.DogRunSeconds);
            bool isRunning = runTime >= 0f && runTime < _ending.DogRunSeconds;
            _stageDog.transform.SetPositionAndRotation(position, isRunning ? Facing(start, stop) : Facing(position, kingPosition));
            if (runTime >= _ending.DogRunSeconds)
            {
                _stageDog.SampleCinematicClip(_ending.DogGreeting, runTime - _ending.DogRunSeconds);
            }
            else
            {
                _stageDog.SetPose(isRunning ? EPose.Walk : EPose.Idle,
                    Vector3.Distance(start, stop) / _ending.DogRunSeconds, true);
            }
        }

        private void UpdateFailure(float elapsed, Vector3 kingPosition, Quaternion kingFacing)
        {
            if (elapsed < _ending.ThrowLookSeconds)
            {
                float phase = Ease(elapsed / _ending.ThrowLookSeconds);
                float angle = Mathf.Sin(phase * 2f * Mathf.PI) * _ending.ThrowLookAngle;
                _stagePlayer.transform.rotation *= Quaternion.Euler(0f, angle, 0f);
                ShowShot(_ending.Throw, 0f);
                return;
            }
            UpdateThrow(elapsed - _ending.ThrowLookSeconds, kingPosition, kingFacing);
        }

        private void UpdateThrow(float elapsed, Vector3 kingPosition, Quaternion kingFacing)
        {
            float flight = (elapsed - _ending.ThrowReleaseAt) / _ending.ThrowFlightSeconds;
            float impactTime = elapsed - _ending.ThrowReleaseAt - _ending.ThrowFlightSeconds;
            float poseTime = elapsed - Mathf.Clamp(impactTime, 0f, _ending.ImpactHoldSeconds);
            if (flight >= 0f && !_trashReleased)
            {
                _stagePlayer.SampleCinematicState(ECharacterAnimation.PickUpHeavy, _ending.ThrowReleaseAt);
                GetThrowGrip(out _throwFrom, out _throwRotation);
                _trashReleased = true;
            }
            _stagePlayer.SampleCinematicState(ECharacterAnimation.PickUpHeavy, poseTime);
            Vector3 contact = kingPosition + Vector3.up * _ending.ThrowTargetHeight + kingFacing * Vector3.forward * 0.32f;
            if (flight < 0f)
            {
                if (elapsed >= _ending.ThrowGripAt)
                {
                    GetThrowGrip(out Vector3 hand, out Quaternion rotation);
                    _trash.SetPositionAndRotation(hand, rotation);
                }
            }
            else if (flight < 1f)
            {
                Vector3 arc = Vector3.up * (4f * flight * (1f - flight) * _ending.ThrowArcHeight);
                _trash.SetPositionAndRotation(Vector3.Lerp(_throwFrom, contact, flight) + arc,
                    _throwRotation * Quaternion.Euler(0f, 0f, flight * 210f));
            }
            else
            {
                if (!_hasImpact)
                {
                    GameSfxPlayer.Play(ESfxCue.HardPlace);
                    _hasImpact = true;
                }
                float recovery = Ease((impactTime - _ending.ImpactHoldSeconds) / _ending.ImpactRecoverySeconds);
                Vector3 direction = kingFacing * Vector3.forward;
                float recoil = (1f - recovery) * _ending.ImpactRecoil;
                _demonKing.transform.SetPositionAndRotation(kingPosition - direction * recoil,
                    kingFacing * Quaternion.Euler(-42f * recoil, 0f, 0f));
                Vector3 landing = kingPosition + direction * 0.78f;
                _trash.SetPositionAndRotation(Vector3.Lerp(contact, landing, recovery),
                    _throwRotation * Quaternion.Euler(0f, 0f, 210f + recovery * 150f));
                ShowShot(_ending.Impact, recovery);
                return;
            }
            ShowShot(_ending.Throw, elapsed / (_ending.ThrowReleaseAt + _ending.ThrowFlightSeconds));
        }

        private void GetThrowGrip(out Vector3 position, out Quaternion rotation)
        {
            if (_stagePlayer.TryGetGrip(null, out position, out rotation)) { return; }
            position = _stagePlayer.transform.position + Vector3.up * 0.85f + _stagePlayer.transform.forward * 0.6f;
        }

        private void ShowShot(ViewSettingsSO.EndingShot shot, float progress)
        {
            float amount = Ease(progress);
            _bootstrap.ViewCamera.ShowEndingShot(World(Vector3.Lerp(shot.StartPosition, shot.EndPosition, amount)),
                World(Vector3.Lerp(shot.StartFocus, shot.EndFocus, amount)));
        }

        private void ReleaseEntrance()
        {
            if (_ending == null) { return; }
            if (_stage != null && _stage.ArrivalLeftDoor != null) { _stage.ArrivalLeftDoor.localRotation = _leftClosed; }
            if (_stage != null && _stage.ArrivalRightDoor != null) { _stage.ArrivalRightDoor.localRotation = _rightClosed; }
            if (_playerVisual != null) { _playerVisual.SetActive(_playerWasVisible); }
            if (_dogVisual != null) { _dogVisual.SetActive(_dogWasVisible); }
            if (_stagePlayer != null) { Destroy(_stagePlayer.gameObject); }
            if (_stageDog != null) { Destroy(_stageDog.gameObject); }
            foreach (var minion in _stageMinions)
            {
                if (minion.Original != null) { minion.Original.SetActive(minion.WasVisible); }
                if (minion.Actor != null) { Destroy(minion.Actor); }
            }
            _stageMinions.Clear();
            if (_trash != null) { Destroy(_trash.gameObject); }
            if (_demonKing != null) { Destroy(_demonKing); }
            _demonKing = null;
            if (_bootstrap != null && _bootstrap.ViewCamera != null) { _bootstrap.ViewCamera.ClearEndingShot(); }
            _entranceRequested = false;
            _ending = null;
        }

        private Vector3 World(Vector3 offset) => _stage.ArrivalStage.TransformPoint(offset);

        private static float Ease(float progress) => Mathf.SmoothStep(0f, 1f, progress);

        private static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
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
