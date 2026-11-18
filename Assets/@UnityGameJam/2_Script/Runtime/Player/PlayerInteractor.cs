using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 매 프레임 E키 대상을 골라 표시하고(Spec Player 규칙 4·6), E·Q 입력을 규칙 명령으로 바꾼다(규칙 4·5).
    /// 고르는 기준은 PlayerInteractionSelector가 갖고 있다.
    /// </summary>
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
            if (_session == null || _session.MatchState != EMatchState.Running)
            {
                SetMarker(InteractionChoice.None);
                return;
            }

            CurrentChoice = _session.IsPlayerWorking ? InteractionChoice.None : _selector.Select(BuildContext(), _session.Items, CollectMinions());
            SetMarker(CurrentChoice);

            if (_interactAction != null && _interactAction.action.WasPressedThisFrame()) { Execute(CurrentChoice); }
            if (_dropAction != null && _dropAction.action.WasPressedThisFrame()) { _session.TryDrop(); }
        }

        private void OnDestroy()
        {
            if (_targetMarker != null) { Destroy(_targetMarker.gameObject); }
        }

        private PlayerInteractionContext BuildContext()
        {
            EContaminationType? carried = null;
            if (_session.PlayerCarriedItemId.HasValue)
            {
                foreach (ItemSnapshot item in _session.Items)
                {
                    if (item.ItemId == _session.PlayerCarriedItemId.Value) { carried = item.Type; }
                }
            }
            Vector3? hellhound = _minions.TryGetValue(EWorkerType.Hellhound, out Transform body) && body != null ? body.position : _hellhoundStart;
            return new PlayerInteractionContext(transform.position, _settings.InteractRadius, carried, _session.IsPlayerHoldingMop, hellhound);
        }

        private List<(EWorkerType, Vector3)> CollectMinions()
        {
            _minionPositions.Clear();
            foreach (KeyValuePair<EWorkerType, Transform> pair in _minions)
            {
                if (pair.Key != EWorkerType.Hellhound && pair.Value != null) { _minionPositions.Add((pair.Key, pair.Value.position)); }
            }
            return _minionPositions;
        }

        private void Execute(InteractionChoice choice)
        {
            switch (choice.Kind)
            {
                case EInteractionKind.PlaceAtHome: _session.TryPlaceAtHome(); break;
                case EInteractionKind.ReturnMop: _session.TryReturnMop(); break;
                case EInteractionKind.BorrowMop: _session.TryBorrowMop(choice.Floor); break;
                case EInteractionKind.DispatchHellhound: _session.DispatchHellhound(); break;
                case EInteractionKind.TalkToMinion: _session.BeginTalk(choice.Minion); break;
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
