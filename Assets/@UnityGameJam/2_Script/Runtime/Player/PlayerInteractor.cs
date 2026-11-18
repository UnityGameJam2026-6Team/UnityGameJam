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
        [SerializeField] private InputActionReference _dropAction;

        [Header("표시")]
        [Tooltip("고른 대상 위에 띄우는 표시")]
        [SerializeField] private Transform _targetMarker;

        private readonly List<(EWorkerType Minion, Vector3 Position)> _minionPositions = new List<(EWorkerType, Vector3)>();
        private readonly Dictionary<EWorkerType, Transform> _minions = new Dictionary<EWorkerType, Transform>();

        private IMatchSession _session;
        private PlayerInteractionSelector _selector;
        private ViewSettingsSO _settings;
        private PlayerBody _playerBody;
        private PlayerController _playerController;
        private EFloor? _borrowedMopFloor;
        private Vector3? _hellhoundStart;

        public InteractionChoice CurrentChoice { get; private set; } = InteractionChoice.None;

        private void OnEnable()
        {
            if (_interactAction != null) { _interactAction.action.Enable(); }
            if (_dropAction != null) { _dropAction.action.Enable(); }
        }

        public void Initialize(IMatchSession session, FacilityLayout layout, ViewSettingsSO settings)
        {
            _session = session;
            _settings = settings;
            _playerBody = GetComponent<PlayerBody>();
            _playerController = GetComponent<PlayerController>();
            _borrowedMopFloor = null;
            _selector = new PlayerInteractionSelector(layout);
            _hellhoundStart = layout.StartPositions.TryGetValue(EWorkerType.Hellhound, out Vector3 hellhound) ? hellhound : (Vector3?)null;
            if (_targetMarker != null) { _targetMarker.SetParent(null, true); } // 플레이어 회전을 따라가지 않게 분리
        }

        /// <summary>졸개 몸(B3)이 생기면 등록한다. 헬하운드를 등록하면 시작점 대신 실제 위치로 배출 명령 대상을 고른다.</summary>
        public void RegisterMinion(EWorkerType minion, Transform body)
        {
            _minions[minion] = body;
        }

        private void Update()
        {
            if (_session == null || _session.MatchState != EMatchState.Running || _playerController.IsHandling || _playerController.IsInStairTransit)
            {
                CurrentChoice = InteractionChoice.None;
                SetMarker(CurrentChoice);
                return;
            }

            CurrentChoice = _session.IsPlayerWorking ? InteractionChoice.None : _selector.Select(BuildContext(), _session.Items, CollectMinions());
            SetMarker(CurrentChoice);

            if (_interactAction != null && _interactAction.action.WasPressedThisFrame()) { Execute(CurrentChoice); }
            if (!_playerController.IsHandling && _dropAction != null && _dropAction.action.WasPressedThisFrame()) { _session.TryDrop(); }
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
            Vector3? hellhound = _minions.TryGetValue(EWorkerType.Hellhound, out Transform body) && body != null ? body.position : _hellhoundStart;
            return new PlayerInteractionContext(transform.position, _playerBody.Floor, _settings.InteractRadius, carried,
                _session.IsPlayerHoldingMop, _borrowedMopFloor, hellhound);
        }

        private List<(EWorkerType, Vector3)> CollectMinions()
        {
            _minionPositions.Clear();
            foreach (KeyValuePair<EWorkerType, Transform> pair in _minions)
            {
                if (pair.Key == EWorkerType.Hellhound || pair.Value == null) { continue; }
                if (pair.Value.TryGetComponent(out MinionAgent minion) && minion.IsHandling) { continue; }
                _minionPositions.Add((pair.Key, pair.Value.position));
            }
            return _minionPositions;
        }

        private void Execute(InteractionChoice choice)
        {
            switch (choice.Kind)
            {
                case EInteractionKind.PlaceAtHome: _session.TryPlaceAtHome(); break;
                case EInteractionKind.ReturnMop:
                    if (_session.TryReturnMop())
                    {
                        _borrowedMopFloor = null;
                        _playerController.RefreshMop();
                    }
                    break;
                case EInteractionKind.BorrowMop:
                    if (_session.TryBorrowMop(choice.Floor))
                    {
                        _borrowedMopFloor = choice.Floor;
                        _playerController.RefreshMop();
                    }
                    break;
                case EInteractionKind.DispatchHellhound: _session.DispatchHellhound(); break;
                case EInteractionKind.TalkToMinion:
                    if (_session.BeginTalk(choice.Minion) && _minions.TryGetValue(choice.Minion, out Transform partner))
                    {
                        _playerController.FaceForTalk(choice.Minion, partner);
                    }
                    break;
                case EInteractionKind.StartMop: _session.TryStartMop(choice.ItemId); break;
                case EInteractionKind.PickUp: _session.TryPickUp(choice.ItemId); break;
            }
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
