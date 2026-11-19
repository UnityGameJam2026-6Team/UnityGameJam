using UnityGameJam.UI;
using System.Collections.Generic;
using TMPro;
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
        [SerializeField] private RectTransform _wasteDirection;
        [SerializeField] private RectTransform _wasteArrow;

        private readonly List<(EWorkerType Minion, Vector3 Position)> _minionPositions = new List<(EWorkerType, Vector3)>();
        private readonly Dictionary<EWorkerType, (MinionAgent Agent, MinionStatusPresenter Status, float Head)> _minions = new();

        private IMatchSession _session;
        private InputAction _shoutAction;
        private InputAction _surgeAction;
        private PlayerInteractionSelector _selector;
        private float _interactionDistance;
        private PlayerBody _playerBody;
        private PlayerController _playerController;
        private EFloor? _borrowedMopFloor;
        private Transform _viewCamera;
        private StairDirectory _stairs;
        private FacilityLayout _layout;
        private int? _shownCarriedItem;
        private HomeSlot _heldHomeSlot;
        private TMP_Text _directionLabel;
        private string _arrowText;
        private string _interactKey;
        private (int Frame, ECommandSkill Skill)? _cooldownPress;
        private Vector3 _wasteScale;
        private float _playerHead;
        private CanvasGroup _targetMarkerGroup;
        private EWorkerType? _markedMinion;
        private const float TargetMarkerFadeSeconds = 0.16f;

        public InteractionChoice CurrentChoice { get; private set; } = InteractionChoice.None;
        internal HomeSlot HeldHomeSlot
        {
            get
            {
                int? itemId = _session?.PlayerCarriedItemId;
                if (itemId != _shownCarriedItem)
                {
                    _shownCarriedItem = itemId;
                    _heldHomeSlot = itemId.HasValue ? FindHeldHomeSlot(itemId.Value) : null;
                }
                return _heldHomeSlot;
            }
        }

        internal EWorkerType? AvailableConversationTarget => !GameMenu.BlocksGameplayInput && isActiveAndEnabled && CurrentChoice.Kind == EInteractionKind.TalkToMinion && !_playerController.IsInConversation ? CurrentChoice.Minion : null;

        internal ECommandSkill? CooldownSkillPressedThisFrame =>
            _cooldownPress is { } press && press.Frame == Time.frameCount ? press.Skill : null;

        private bool CanInteract => !GameMenu.BlocksGameplayInput && _session?.MatchState == EMatchState.Running && !_playerController.IsHandling
            && !_playerController.IsInStairTransit && !_playerController.IsInConversation;

        private void OnEnable()
        {
            if (_interactAction != null) { _interactAction.action.Enable(); }
            _shoutAction = _interactAction?.action?.actionMap?.FindAction("Shout");
            _surgeAction = _interactAction?.action?.actionMap?.FindAction("Surge");
            _shoutAction?.Enable();
            _surgeAction?.Enable();
        }

        public void Initialize(IMatchSession session, FacilityLayout layout, float interactionDistance, FloorCameraFollow camera, StairDirectory stairs)
        {
            _session = session;
            _interactionDistance = interactionDistance;
            _playerBody = GetComponent<PlayerBody>();
            _playerController = GetComponent<PlayerController>();
            _borrowedMopFloor = null;
            _selector = new PlayerInteractionSelector(layout);
            _viewCamera = camera != null ? camera.transform : null;
            _stairs = stairs;
            _layout = layout;
            _shownCarriedItem = null;
            _heldHomeSlot = null;
            _directionLabel = _wasteArrow != null ? _wasteArrow.GetComponent<TMP_Text>() : null;
            _arrowText = _directionLabel != null ? _directionLabel.text : null;
            RefreshInteractKey();
            _playerHead = HeadHeight(this);
            _markedMinion = null;
            if (_targetMarker != null)
            {
                _targetMarkerGroup = _targetMarker.GetComponentInChildren<CanvasGroup>(true);
                if (_targetMarkerGroup != null) { _targetMarkerGroup.alpha = 0f; }
                _targetMarker.gameObject.SetActive(false);
            }
            if (_wasteDirection != null) { _wasteScale = _wasteDirection.localScale; }
        }

        public void RegisterMinion(MinionAgent agent, MinionStatusPresenter status) =>
            _minions[agent.Worker] = (agent, status, HeadHeight(agent));

        private static float HeadHeight(Component unit)
        {
            ArtSlot art = unit.GetComponentInChildren<ArtSlot>(true);
            return (art != null && art.ModelTop > 0f ? art.ModelTop : 1.8f) + 0.25f;
        }

        public void SetInteractionDistance(float distance) => _interactionDistance = distance;

        internal void RefreshInteractKey() => _interactKey = _interactAction?.action.GetBindingDisplayString();

        private void Update()
        {
            if (GameMenu.BlocksGameplayInput) { return; }
            if (_session is ICommandSkillSession skills && _session.MatchState == EMatchState.Running
                && !_playerController.IsInStairTransit && !_playerController.IsInConversation)
            {
                if (_shoutAction?.WasPressedThisFrame() == true) { CastSkill(skills, ECommandSkill.Shout); }
                else if (_surgeAction?.WasPressedThisFrame() == true) { CastSkill(skills, ECommandSkill.Surge); }
            }
            if (!CanInteract)
            {
                CurrentChoice = InteractionChoice.None;
                return;
            }

            CurrentChoice = _session.IsPlayerWorking ? InteractionChoice.None : _selector.Select(BuildContext(), _session.Items, CollectMinions());

            if (_interactAction != null && _interactAction.action.WasPressedThisFrame()) { Execute(CurrentChoice); }
        }

        private void CastSkill(ICommandSkillSession skills, ECommandSkill skill)
        {
            if (skills.SkillCooldownRemaining(skill) > 0f)
            {
                _cooldownPress = (Time.frameCount, skill);
                return;
            }
            if (!skills.TryCastSkill(skill)) { GameSfxPlayer.Play(ESfxCue.Unavailable); }
        }

        internal bool TryGetGroundWorkContext(out PlayerInteractionContext context)
        {
            context = default;
            if (!isActiveAndEnabled || !CanInteract || _session.IsPlayerWorking || _session.PlayerCarriedItemId.HasValue) { return false; }
            context = BuildContext();
            return true;
        }

        private void OnDisable()
        {
            _shoutAction?.Disable();
            _surgeAction?.Disable();
            _markedMinion = null;
            if (_targetMarkerGroup != null) { _targetMarkerGroup.alpha = 0f; }
            if (_targetMarker != null) { _targetMarker.gameObject.SetActive(false); }
            if (_wasteDirection != null) { _wasteDirection.gameObject.SetActive(false); }
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
            Vector3? hellhound = _minions.TryGetValue(EWorkerType.Hellhound, out var hound)
                && IsAvailable(hound.Agent) && hound.Status.IsAvailableForTalk ? hound.Agent.Position : null;
            return new PlayerInteractionContext(transform.position, _playerBody.Floor, _interactionDistance, carried,
                _session.IsPlayerHoldingMop, _borrowedMopFloor, hellhound);
        }

        private HomeSlot FindHeldHomeSlot(int carriedId)
        {
            foreach (ItemSnapshot item in _session.Items)
            {
                if (item.ItemId != carriedId || item.Type != EContaminationType.ClutterItem || !item.HomeSlotId.HasValue) { continue; }
                foreach (HomeSlot slot in _layout.HomeSlots)
                {
                    if (slot.SlotId == item.HomeSlotId.Value) { return slot; }
                }
                break;
            }
            return null;
        }

        private List<(EWorkerType, Vector3)> CollectMinions()
        {
            _minionPositions.Clear();
            foreach (var (agent, _, _) in _minions.Values)
            {
                if (agent.Worker != EWorkerType.Hellhound && IsAvailable(agent))
                {
                    _minionPositions.Add((agent.Worker, agent.Position));
                }
            }
            return _minionPositions;
        }

        private bool IsAvailable(MinionAgent agent) => agent != null && agent.Floor == _playerBody.Floor
            && !agent.IsHandling && !agent.IsInStairTransit;

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
                    accepted = _session.BeginTalk(choice.Minion);
                    if (accepted && _minions.TryGetValue(choice.Minion, out var partner))
                    {
                        _playerController.FaceForTalk(choice.Minion, partner.Agent.transform);
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

        private void UpdateTargetMarker(float deltaTime)
        {
            if (_targetMarkerGroup == null || _viewCamera == null) { return; }

            bool canTalk = CurrentChoice.Kind == EInteractionKind.TalkToMinion;
            EWorkerType? candidate = canTalk && !_playerController.IsInConversation
                && _minions.TryGetValue(CurrentChoice.Minion, out var selectable) && selectable.Agent != null
                ? CurrentChoice.Minion : null;
            _markedMinion ??= candidate;
            bool isCurrent = _markedMinion.HasValue && _markedMinion == candidate;
            float alpha = Mathf.MoveTowards(_targetMarkerGroup.alpha, isCurrent ? 1f : 0f,
                deltaTime / TargetMarkerFadeSeconds);
            _targetMarkerGroup.alpha = alpha;

            if (alpha > 0f && _markedMinion.HasValue && _minions.TryGetValue(_markedMinion.Value, out var target)
                && target.Agent != null)
            {
                _targetMarker.SetPositionAndRotation(target.Agent.Position + Vector3.up * target.Head, _viewCamera.rotation);
            }
            if (!isCurrent && alpha <= 0f) { _markedMinion = null; }

            bool visible = alpha > 0f;
            if (_targetMarker.gameObject.activeSelf != visible) { _targetMarker.gameObject.SetActive(visible); }
        }

        private void LateUpdate()
        {
            UpdateTargetMarker(Time.unscaledDeltaTime);

            if (_wasteDirection == null || _wasteArrow == null) { return; }
            HomeSlot home = HeldHomeSlot;
            bool carryingWaste = _playerController != null
                && (_playerController.CarriedType is EContaminationType.Trash or EContaminationType.HeavyDebris);
            if (_viewCamera == null || _session?.MatchState != EMatchState.Running || (home == null && !carryingWaste)
                || _playerController.IsInStairTransit)
            {
                _wasteDirection.gameObject.SetActive(false);
                return;
            }

            EFloor floor = _playerBody.Floor;
            Vector3 position = transform.position;
            WasteBin bin = _playerController.CarriedType == EContaminationType.Trash
                ? _layout.FindNearestWasteBin(floor, position) : null;
            Bounds discharge = _layout.DischargeArea;
            Vector3 destination = home?.Position ?? bin?.Position ?? discharge.center;
            EFloor targetFloor = home != null ? ZoneDisplayName.GetFloor(home.Zone) : bin?.Floor ?? EFloor.Floor1;
            bool visible = floor == targetFloor
                || (_stairs != null && _stairs.TryGet(floor, floor + (floor < targetFloor ? 1 : -1), out destination));
            _wasteDirection.gameObject.SetActive(visible);
            if (!visible) { return; }

            bool atDestination = floor == targetFloor && (home != null || bin != null
                ? PlayerInteractionSelector.DistanceXZ(position, destination) <= _interactionDistance
                : discharge.size.x > 0f && discharge.size.z > 0f && FacilityLayoutValidator.ContainsXZ(discharge, position));
            bool canPlaceHere = home != null && CurrentChoice.Kind == EInteractionKind.PlaceAtHome;
            bool canDropHere = atDestination && (canPlaceHere || (home == null && CurrentChoice.Kind == EInteractionKind.Drop));
            bool showKey = canPlaceHere && !string.IsNullOrEmpty(_interactKey);
            if (_directionLabel != null)
            {
                string label = showKey ? _interactKey : _arrowText;
                if (_directionLabel.text != label) { _directionLabel.text = label; }
            }
            Vector3 offset = Vector3.ProjectOnPlane(destination - position, Vector3.up);
            float angle = atDestination ? 180f :
                Mathf.Atan2(-Vector3.Dot(offset, _viewCamera.right), Vector3.Dot(offset, _viewCamera.up)) * Mathf.Rad2Deg;
            float bob = canDropHere ? .12f * (1f - Mathf.Cos(Time.time * Mathf.PI * 2.8f)) : 0f;
            Vector3 arrowPosition = position + Vector3.up * (_playerHead + .30f) + _viewCamera.up * bob;
            _wasteDirection.SetPositionAndRotation(arrowPosition, _viewCamera.rotation);
            _wasteDirection.localScale = _wasteScale * Mathf.Lerp(1.45f, 0.7f, Mathf.Clamp01((atDestination ? 0f : offset.magnitude) / 12f));
            _wasteArrow.localRotation = Quaternion.Euler(0f, 0f, showKey ? 0f : angle);
        }
    }
}
