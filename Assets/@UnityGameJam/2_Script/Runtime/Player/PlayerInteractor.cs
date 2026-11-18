using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 매 프레임 상호작용 대상을 골라 표시하고(Spec Player 규칙 4·6), 상호작용·내려놓기 입력을 규칙 명령으로 바꾼다(규칙 4·5).
    /// 고르는 기준은 PlayerInteractionSelector가 갖고 있다.
    /// </summary>
    [RequireComponent(typeof(PlayerBody), typeof(PlayerController))]
    [DefaultExecutionOrder(-20)]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("표시")]
        [Tooltip("고른 대상 위에 띄우는 표시")]
        [SerializeField] private Transform _targetMarker;

        private readonly List<(EWorkerType Minion, Vector3 Position)> _minionPositions = new List<(EWorkerType, Vector3)>();
        private readonly Dictionary<EWorkerType, Transform> _minions = new Dictionary<EWorkerType, Transform>();

        private IMatchSession _session;
        private PlayerInteractionSelector _selector;
        private float _interactionDistance;
        private PlayerBody _playerBody;
        private PlayerController _playerController;
        private EFloor? _borrowedMopFloor;
        private Transform _hellhound;
        private MinionStatusPresenter _hellhoundPresenter;

        public InteractionChoice CurrentChoice { get; private set; } = InteractionChoice.None;
        private bool IsHellhoundVisible => _hellhound != null && _hellhoundPresenter?.IsHidden != true;

        private void OnEnable()
        {
            if (_interactAction != null) { _interactAction.action.Enable(); }
        }

        public void Initialize(IMatchSession session, FacilityLayout layout, float interactionDistance)
        {
            _session = session;
            _interactionDistance = interactionDistance;
            _playerBody = GetComponent<PlayerBody>();
            _playerController = GetComponent<PlayerController>();
            _borrowedMopFloor = null;
            _selector = new PlayerInteractionSelector(layout);
            if (_targetMarker != null) { _targetMarker.SetParent(null, true); } // 플레이어 회전을 따라가지 않게 분리
        }

        /// <summary>대화 상대는 실제로 생성된 졸개의 위치에서 고른다.</summary>
        public void RegisterMinion(EWorkerType minion, Transform body)
        {
            _minions[minion] = body;
            if (minion == EWorkerType.Hellhound)
            {
                _hellhound = body;
                _hellhoundPresenter = body != null ? body.GetComponent<MinionStatusPresenter>() : null;
            }
        }

        public void SetInteractionDistance(float distance) => _interactionDistance = distance;

        private void Update()
        {
            if (_playerController != null && _playerController.IsTalkingToHellhound &&
                (!IsHellhoundVisible || PlayerInteractionSelector.DistanceXZ(transform.position, _hellhound.position) > _interactionDistance))
            {
                _playerController.EndHellhoundTalk();
            }
            if (_session == null || _session.MatchState != EMatchState.Running || _playerController.IsHandling
                || _playerController.IsInStairTransit || _playerController.IsInConversation)
            {
                CurrentChoice = InteractionChoice.None;
                SetMarker(CurrentChoice);
                return;
            }

            CurrentChoice = _session.IsPlayerWorking ? InteractionChoice.None : _selector.Select(BuildContext(), _session.Items, CollectMinions());
            SetMarker(CurrentChoice);

            if (_interactAction != null && _interactAction.action.WasPressedThisFrame()) { Execute(CurrentChoice); }
        }

        private void OnDestroy()
        {
            if (_targetMarker != null) { Destroy(_targetMarker.gameObject); }
        }

        private PlayerInteractionContext BuildContext()
        {
            ItemSnapshot? carried = null;
            int? carriedId = _session.PlayerCarriedItemId;
            if (carriedId.HasValue)
            {
                foreach (ItemSnapshot item in _session.Items)
                {
                    if (item.ItemId == carriedId.Value)
                    {
                        carried = item;
                        break;
                    }
                }
            }
            Vector3? hellhound = IsHellhoundVisible ? _hellhound.position : (Vector3?)null;
            return new PlayerInteractionContext(transform.position, _playerBody.Floor, _interactionDistance, carried,
                _session.IsPlayerHoldingMop, _borrowedMopFloor, hellhound);
        }

        private List<(EWorkerType, Vector3)> CollectMinions()
        {
            _minionPositions.Clear();
            foreach (KeyValuePair<EWorkerType, Transform> pair in _minions)
            {
                if (pair.Key == EWorkerType.Hellhound || pair.Value == null) { continue; }
                if (pair.Value.TryGetComponent(out MinionAgent minion) && (minion.IsHandling || minion.IsInStairTransit)) { continue; }
                _minionPositions.Add((pair.Key, pair.Value.position));
            }
            return _minionPositions;
        }

        private void Execute(InteractionChoice choice)
        {
            bool accepted = false;
            switch (choice.Kind)
            {
                case EInteractionKind.PlaceAtHome:
                    accepted = _session.TryPlaceAtHome();
                    break;
                case EInteractionKind.ReturnMop:
                    accepted = _session.TryReturnMop();
                    if (accepted)
                    {
                        _borrowedMopFloor = null;
                        _playerController.RefreshMop();
                    }
                    break;
                case EInteractionKind.BorrowMop:
                    accepted = _session.TryBorrowMop(choice.Floor);
                    if (accepted)
                    {
                        _borrowedMopFloor = choice.Floor;
                        _playerController.RefreshMop();
                    }
                    break;
                case EInteractionKind.TalkToMinion:
                    if (_minions.TryGetValue(choice.Minion, out Transform partner) && partner != null)
                    {
                        // 헬하운드는 방 배정 세션에 참여하지 않고 대화창에서 배출을 확인한다.
                        accepted = choice.Minion == EWorkerType.Hellhound || _session.BeginTalk(choice.Minion);
                        if (accepted) { _playerController.FaceForTalk(choice.Minion, partner); }
                    }
                    break;
                case EInteractionKind.StartMop:
                    accepted = _session.TryStartMop(choice.ItemId);
                    break;
                case EInteractionKind.PickUp:
                    accepted = _session.TryPickUp(choice.ItemId);
                    break;
                case EInteractionKind.Drop:
                    accepted = _session.TryDrop();
                    break;
            }
            if (!accepted && choice.Kind != EInteractionKind.None) { GameSfxPlayer.Play(ESfxCue.Unavailable); }
        }

        private void SetMarker(InteractionChoice choice)
        {
            if (_targetMarker == null) { return; }
            bool isVisible = choice.Kind != EInteractionKind.None;
            if (_targetMarker.gameObject.activeSelf != isVisible) { _targetMarker.gameObject.SetActive(isVisible); }
            if (isVisible) { _targetMarker.SetPositionAndRotation(choice.TargetPosition + Vector3.up * 0.03f, Quaternion.identity); }
        }
    }
}
