using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    public sealed class FacilityHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _zone;
        [SerializeField] private TMP_Text _zoneProgress;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private TMP_Text _facilityProgress;
        [SerializeField] private Slider _facilitySlider;
        [SerializeField] private RectTransform _facilityGoalTrack;
        [SerializeField] private RectTransform _facilitySurplusFill;
        [SerializeField] private GameObject _choices;
        [SerializeField] private TMP_Text _assignmentTitle;
        [SerializeField] private TMP_Text _assignmentRoom;
        [SerializeField] private TMP_Text _assignmentDialogue;
        [SerializeField] private TMP_Text _dialogueSpeaker;
        [SerializeField] private Button[] _rooms;
        [SerializeField] private Button _cancel;
        [SerializeField] private Button _hellhoundDispatch;
        [SerializeField] private GameObject[] _roomOrders = Array.Empty<GameObject>();
        [SerializeField] private MinionStatusPanel _minionPanel;
        [SerializeField] private ProposalStackView _proposalStack;
        [SerializeField] private GameObject _workGauge;
        [SerializeField] private RectTransform _workFill;
        private MatchBootstrap _bootstrap;
        private RectTransform _workGaugeRect;
        private float _workGaugeOriginY;
        private MinionSpeechBubble _speechBubble;
        private EWorkerType? _talking;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();
        private static readonly EZoneId[] Zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private int _lastSecond = -1;
        private float _facilityProgressVelocity;
        [SerializeField] private DemonKing.UI.DemonKingCountdownHUD _countdown;

        [SerializeField] private CanvasGroup _gameplayHud;
        [SerializeField] private Image _openingNumber;
        [SerializeField] private RectTransform _skillSlots;
        [SerializeField] private Sprite _shoutIcon;
        [SerializeField] private Sprite _surgeIcon;
        private CommandSkillHud _skillHud;
        private int _lastOpeningNumber;

        private void Awake()
        {
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            if (_skillSlots != null) { _skillHud = new CommandSkillHud(_skillSlots, _zone.font, _shoutIcon, _surgeIcon); }
            SetGameplayHudVisible(false);
            if (_countdown == null) { _countdown = GetComponentInChildren<DemonKing.UI.DemonKingCountdownHUD>(true); }
            if (_countdown != null)
            {
                _countdown.PauseTimer();
                if (_countdown.plate != null && _facilitySlider != null)
                {
                    var bar = (RectTransform)_facilitySlider.transform;
                    bar.SetParent(_countdown.plate, false);
                    bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f);
                    bar.pivot = new Vector2(.5f, 1f);
                    // 타이머 프레임 이미지의 투명 하단을 넘겨 외곽선에 붙인다.
                    bar.anchoredPosition = new Vector2(0f, 24f);
                    bar.SetAsLastSibling();
                }
            }
            _workGaugeRect = (RectTransform)_workGauge.transform;
            _workGaugeOriginY = _workGaugeRect.anchoredPosition.y;
            _assignmentTitle.text = "작업 지시";
            _choices.SetActive(false);
            for (int i = 0; i < _rooms.Length; i++)
            {
                EZoneId zone = Zones[i];
                _rooms[i].onClick.AddListener(() => Assign(zone));
            }
            _cancel.onClick.AddListener(Cancel);
            if (_hellhoundDispatch != null) { _hellhoundDispatch.onClick.AddListener(DispatchHellhound); }
            ConfigureRoomNavigation();
        }

        private void ConfigureRoomNavigation()
        {
            var rows = new List<Button>[(int)EFloor.Floor3 + 1];
            for (int i = 0; i < rows.Length; i++) { rows[i] = new List<Button>(); }
            for (int i = 0; i < _rooms.Length; i++) { rows[(int)ZoneDisplayName.GetFloor(Zones[i])].Add(_rooms[i]); }

            for (int floor = 0; floor < rows.Length; floor++)
            {
                List<Button> rooms = rows[floor];
                for (int room = 0; room < rooms.Count; room++)
                {
                    rooms[room].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = floor > 0 ? RoomAt(rows[floor - 1], room) : null,
                        selectOnDown = floor + 1 < rows.Length ? RoomAt(rows[floor + 1], room) : _cancel,
                        selectOnLeft = room > 0 ? rooms[room - 1] : null,
                        selectOnRight = room + 1 < rooms.Count ? rooms[room + 1] : null
                    };
                }
            }
            _cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _rooms[_rooms.Length - 1] };
        }

        private static Button RoomAt(List<Button> rooms, int column) =>
            rooms.Count > 0 ? rooms[Mathf.Min(rooms.Count - 1, column)] : null;

        private void LateUpdate()
        {
            IMatchSession session = _bootstrap != null ? _bootstrap.Session : null;
            ShowOpeningNumber();
            if (session == null || session.MatchState == EMatchState.Ready)
            {
                _countdown?.PauseTimer();
                return;
            }
            SetGameplayHudVisible(true);
            ShowTime(session.RemainingTime);
            EZoneId? zone = session.PlayerZone;
            _zone.text = zone.HasValue ? ZoneDisplayName.GetFullName(zone.Value) : ZoneDisplayName.GetFloorLabel(_bootstrap.Player.Floor);
            _zoneProgress.gameObject.SetActive(zone.HasValue);
            if (zone.HasValue) { ShowProgress(_zoneProgress, session.GetZoneCleanliness(zone.Value), session.ZoneThreshold); }
            ShowFacilityProgress(session.FacilityCleanliness, session.FacilityThreshold, Time.unscaledDeltaTime);
            IReadOnlyList<MinionStatus> minions = session.Minions;
            var skills = session as ICommandSkillSession;
            if (_skillSlots != null)
            {
                bool showSkills = skills != null && session.MatchState == EMatchState.Running;
                if (_skillSlots.gameObject.activeSelf != showSkills) { _skillSlots.gameObject.SetActive(showSkills); }
                if (showSkills) { _skillHud.Show(skills); }
            }
            _minionPanel.Show(minions, skills);
            bool isWorking = session.IsPlayerWorking;
            if (_workGauge.activeSelf != isWorking) { _workGauge.SetActive(isWorking); }
            if (isWorking)
            {
                Vector2 position = _workGaugeRect.anchoredPosition;
                position.y = _workGaugeOriginY + _minionPanel.HeightOffset;
                _workGaugeRect.anchoredPosition = position;
                _workFill.anchorMax = new Vector2(Mathf.Clamp01(session.PlayerWorkProgress / 100f), 1f);
            }

            bool wasTalking = _talking.HasValue;
            MinionStatus? talking = null;
            if (session.MatchState == EMatchState.Running)
            {
                foreach (MinionStatus minion in minions)
                {
                    if (minion.Activity == EMinionActivity.Talking) { talking = minion; break; }
                }
            }
            if (talking?.Minion != _talking)
            {
                if (talking.HasValue)
                {
                    MinionStatus minion = talking.Value;
                    if (_dialogueSpeaker != null) { _dialogueSpeaker.text = MinionStatusPanel.WorkerName(minion.Minion); }
                    bool hellhound = minion.Minion == EWorkerType.Hellhound;
                    _assignmentRoom.text = $"현재 담당 방  {MinionStatusPanel.RoomName(minion.AssignedZone)}";
                    if (_assignmentDialogue != null)
                    {
                        _assignmentDialogue.text = hellhound ? "무슨 일이야?" :
                            minion.AssignedZone.HasValue ? "어느 방으로 옮길까요?" : "어느 방부터 청소할까요?";
                    }
                    if (!_talking.HasValue) { OpenAssignmentPanel(minion.AssignedZone, hellhound); }
                }
                else { CloseAssignmentPanel(); }
                _talking = talking?.Minion;
                _bootstrap.FocusConversation(_talking);
            }
            if (_talking.HasValue && EventSystem.current != null &&
                EventSystem.current.currentInputModule is InputSystemUIInputModule input &&
                input.cancel?.action?.WasPressedThisFrame() == true) { Cancel(); }
            RectTransform messageBoundary = _minionPanel.Rect;
            if (isWorking) { messageBoundary = _workGaugeRect; }
            _proposalStack.Show(session, minions, _bootstrap.MatchDuration - session.RemainingTime,
                messageBoundary, wasTalking || talking.HasValue);
            var speech = (session as IMinionSpeechSource)?.CurrentSpeech;
            if (speech.HasValue && _speechBubble == null) { _speechBubble = new MinionSpeechBubble(_zone); }
            _speechBubble?.Show(_bootstrap, talking.HasValue ? null : speech);
        }

        private void OnDisable()
        {
            _speechBubble?.Hide();
            if (_talking.HasValue)
            {
                CloseAssignmentPanel();
                _talking = null;
            }
        }

        private void OpenAssignmentPanel(EZoneId? assigned, bool hellhound)
        {
            _assignmentTitle.text = hellhound ? "배출 지시" : "작업 지시";
            foreach (GameObject roomOrder in _roomOrders) { roomOrder.SetActive(!hellhound); }
            if (_hellhoundDispatch != null) { _hellhoundDispatch.gameObject.SetActive(hellhound); }
            Navigation cancelNavigation = _cancel.navigation;
            cancelNavigation.selectOnUp = hellhound ? _hellhoundDispatch : _rooms[_rooms.Length - 1];
            _cancel.navigation = cancelNavigation;
            SuspendPlayerInput();
            _choices.SetActive(true);
            GameSfxPlayer.Play(ESfxCue.PanelOpen);
            if (hellhound && _hellhoundDispatch != null)
            {
                _hellhoundDispatch.Select();
                return;
            }
            int focus = Array.FindIndex(Zones, zone => zone == assigned ||
                (!assigned.HasValue && ZoneDisplayName.GetFloor(zone) == _bootstrap.Player.Floor && ZoneDisplayName.IsLobby(zone)));
            _rooms[Mathf.Clamp(focus, 0, _rooms.Length - 1)].Select();
        }

        private void SuspendPlayerInput()
        {
            InputActionMap playerMap = InputSystem.actions.FindActionMap("Player", true);
            foreach (InputAction action in playerMap.actions)
            {
                if (action.enabled) { _suspendedActions.Add(action); }
            }
            playerMap.Disable();
        }

        private void CloseAssignmentPanel()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(_choices.transform))
            {
                events.SetSelectedGameObject(null);
            }
            _choices.SetActive(false);
            _bootstrap?.FocusConversation(null);
            RestorePlayerInput();
        }

        private void RestorePlayerInput()
        {
            foreach (InputAction action in _suspendedActions) { action.Enable(); }
            _suspendedActions.Clear();
        }

        internal void EndMatch()
        {
            ShowTime(0f);
            IMatchSession session = _bootstrap.Session;
            if (session != null) { ShowFacilityProgress(session.FacilityCleanliness, session.FacilityThreshold, 0f); }
            // 대화 중 마감되면 기존 잠금을 다음 판까지 유지한다.
            if (!_talking.HasValue) { SuspendPlayerInput(); }
            _talking = null;
            _choices.SetActive(false);
            _bootstrap.FocusConversation(null);
            _minionPanel.gameObject.SetActive(false);
            _proposalStack.gameObject.SetActive(false);
            _workGauge.SetActive(false);
            if (_skillSlots != null) { _skillSlots.gameObject.SetActive(false); }
            enabled = false;
        }

        private void OnDestroy()
        {
            _skillHud?.Release();
            _speechBubble?.Destroy();
            RestorePlayerInput();
        }

        private void ShowOpeningNumber()
        {
            if (_openingNumber == null || _bootstrap == null || _countdown == null) { return; }
            int number = _bootstrap.OpeningNumber;
            if (number != _lastOpeningNumber)
            {
                _lastOpeningNumber = number;
                _openingNumber.gameObject.SetActive(number > 0);
                if (number > 0) { _openingNumber.sprite = _countdown.digitSprites[number]; }
            }
            if (number <= 0) { return; }
            float settle = Mathf.Clamp01(_bootstrap.OpeningBeatProgress / 0.2f);
            _openingNumber.rectTransform.localScale = Vector3.one * Mathf.SmoothStep(1.17f, 1f, settle);
        }

        private void SetGameplayHudVisible(bool isVisible)
        {
            if (_gameplayHud == null || _gameplayHud.blocksRaycasts == isVisible) { return; }
            _gameplayHud.alpha = isVisible ? 1f : 0f;
            _gameplayHud.interactable = isVisible;
            _gameplayHud.blocksRaycasts = isVisible;
            _minionPanel.IsToggleBlocked = !isVisible;
        }

        private void ShowTime(float remaining)
        {
            if (_countdown != null)
            {
                _countdown.PauseTimer();
                if (_lastSecond < 0) { _countdown.JumpTo(remaining); }
                else { _countdown.NotifyRemaining(remaining); }
            }
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            if (seconds == _lastSecond) { return; }
            _lastSecond = seconds;
            if (_time != null) { _time.text = $"{seconds / 60:00}:{seconds % 60:00}"; }
        }

        private void ShowFacilityProgress(float cleanliness, float threshold, float deltaTime)
        {
            ShowProgress(_facilityProgress, cleanliness, threshold);
            if (_facilitySlider == null) { return; }

            float target = Mathf.Clamp(cleanliness, _facilitySlider.minValue, _facilitySlider.maxValue);
            float current = _facilitySlider.value;
            float next = current < target && deltaTime > 0f
                ? Mathf.SmoothDamp(current, target, ref _facilityProgressVelocity, 0.12f, Mathf.Infinity, deltaTime)
                : target;
            if (target - next < 0.05f)
            {
                next = target;
                _facilityProgressVelocity = 0f;
            }
            _facilitySlider.SetValueWithoutNotify(next);

            float goal = Mathf.InverseLerp(_facilitySlider.minValue, _facilitySlider.maxValue, threshold);
            if (_facilityGoalTrack != null) { _facilityGoalTrack.anchorMax = new Vector2(goal, 1f); }
            if (_facilitySurplusFill != null)
            {
                float fill = _facilitySlider.normalizedValue;
                _facilitySurplusFill.anchorMin = new Vector2(fill > goal ? goal / fill : 1f, 0f);
            }
        }

        private static void ShowProgress(TMP_Text label, float value, float threshold)
        {
            label.SetText("{0}%/{1}%", Mathf.FloorToInt(value), Mathf.FloorToInt(threshold));
        }

        private void DispatchHellhound()
        {
            if (_talking != EWorkerType.Hellhound) { return; }
            if (_bootstrap.Session.DispatchHellhound()) { GameSfxPlayer.Play(ESfxCue.Confirm); }
        }

        private void Assign(EZoneId zone)
        {
            if (_talking.HasValue && _bootstrap.Session.AssignRoom(_talking.Value, zone))
            {
                GameAudioPresenter.MarkWorkerHandled(_talking.Value);
                GameSfxPlayer.Play(ESfxCue.Confirm);
            }
        }

        private void Cancel()
        {
            if (_talking.HasValue)
            {
                _bootstrap.Session.CancelTalk(_talking.Value);
                GameSfxPlayer.Play(ESfxCue.Cancel);
            }
        }
    }
}
