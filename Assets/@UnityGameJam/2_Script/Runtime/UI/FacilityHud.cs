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
        [SerializeField] private GameObject _choices;
        [SerializeField] private TMP_Text _assignmentTitle;
        [SerializeField] private TMP_Text _assignmentRoom;
        [SerializeField] private TMP_Text _assignmentDialogue;
        [SerializeField] private TMP_Text _dialogueSpeaker;
        [SerializeField] private Button[] _rooms;
        [SerializeField] private Button _cancel;
        [SerializeField] private MinionStatusPanel _minionPanel;
        [SerializeField] private ProposalStackView _proposalStack;
        [SerializeField] private GameObject _workGauge;
        [SerializeField] private RectTransform _workFill;
        private MatchBootstrap _bootstrap;
        private RectTransform _assignmentPanel;
        private RectTransform _workGaugeRect;
        private Vector2 _assignmentOrigin;
        private MinionSpeechBubble _speechBubble;
        private EWorkerType? _talking;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();
        private static readonly EZoneId[] Zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private int _lastSecond = -1;
        [SerializeField] private DemonKing.UI.DemonKingCountdownHUD _countdown;

        private void Awake()
        {
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            if (_countdown == null) { _countdown = GetComponentInChildren<DemonKing.UI.DemonKingCountdownHUD>(true); }
            if (_countdown != null) { _countdown.PauseTimer(); }
            _assignmentPanel = (RectTransform)_choices.transform;
            _assignmentOrigin = _assignmentPanel.anchoredPosition;
            _workGaugeRect = (RectTransform)_workGauge.transform;
            _assignmentTitle.text = "작업 지시";
            _choices.SetActive(false);
            for (int i = 0; i < _rooms.Length; i++)
            {
                EZoneId zone = Zones[i];
                _rooms[i].onClick.AddListener(() => Assign(zone));
            }
            _cancel.onClick.AddListener(Cancel);
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
            if (session == null) { return; }
            ShowTime(session.RemainingTime);
            EZoneId? zone = session.PlayerZone;
            _zone.text = zone.HasValue ? ZoneDisplayName.GetFullName(zone.Value) : ZoneDisplayName.GetFloorLabel(_bootstrap.Player.Floor);
            _zoneProgress.gameObject.SetActive(zone.HasValue);
            if (zone.HasValue) { ShowProgress(_zoneProgress, "지역 진척도", session.GetZoneCleanliness(zone.Value), session.ZoneThreshold); }
            ShowProgress(_facilityProgress, "전체 진행도", session.FacilityCleanliness, session.FacilityThreshold);
            IReadOnlyList<MinionStatus> minions = session.Minions;
            _minionPanel.Show(minions);
            float lowerHudTop = _assignmentOrigin.y + _minionPanel.HeightOffset;
            _assignmentPanel.anchoredPosition = new Vector2(_assignmentOrigin.x, lowerHudTop);
            bool isWorking = session.IsPlayerWorking;
            if (_workGauge.activeSelf != isWorking) { _workGauge.SetActive(isWorking); }
            if (isWorking)
            {
                _workGaugeRect.anchoredPosition = new Vector2(_workGaugeRect.anchoredPosition.x, lowerHudTop);
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
                    _assignmentRoom.text = $"현재 담당 방  {MinionStatusPanel.RoomName(minion.AssignedZone)}";
                    if (_assignmentDialogue != null)
                    {
                        _assignmentDialogue.text = minion.AssignedZone.HasValue ? "어느 방으로 옮길까요?" : "어느 방부터 청소할까요?";
                    }
                    if (!_talking.HasValue) { OpenAssignmentPanel(minion.AssignedZone); }
                }
                else { CloseAssignmentPanel(); }
                _talking = talking?.Minion;
                _bootstrap.FocusConversation(_talking);
            }
            if (_talking.HasValue && EventSystem.current != null &&
                EventSystem.current.currentInputModule is InputSystemUIInputModule input &&
                input.cancel?.action?.WasPressedThisFrame() == true) { Cancel(); }
            RectTransform messageBoundary = _minionPanel.Rect;
            if (talking.HasValue) { messageBoundary = _assignmentPanel; }
            else if (isWorking) { messageBoundary = _workGaugeRect; }
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

        private void OpenAssignmentPanel(EZoneId? assigned)
        {
            SuspendPlayerInput();
            _choices.SetActive(true);
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
            // 대화 중 마감되면 기존 잠금을 다음 판까지 유지한다.
            if (!_talking.HasValue) { SuspendPlayerInput(); }
            _talking = null;
            _choices.SetActive(false);
            _bootstrap.FocusConversation(null);
            _minionPanel.gameObject.SetActive(false);
            _proposalStack.gameObject.SetActive(false);
            _workGauge.SetActive(false);
            enabled = false;
        }

        private void OnDestroy()
        {
            _speechBubble?.Destroy();
            RestorePlayerInput();
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

        private static void ShowProgress(TMP_Text label, string title, float value, float threshold)
        {
            int percent = Mathf.FloorToInt(value);
            label.SetText(value < threshold ? title + "  {0}% / {1}%  미달" : title + "  {0}% / {1}%", percent, threshold);
        }

        private void Assign(EZoneId zone)
        {
            if (_talking.HasValue) { _bootstrap.Session.AssignRoom(_talking.Value, zone); }
        }

        private void Cancel()
        {
            if (_talking.HasValue) { _bootstrap.Session.CancelTalk(_talking.Value); }
        }
    }
}
