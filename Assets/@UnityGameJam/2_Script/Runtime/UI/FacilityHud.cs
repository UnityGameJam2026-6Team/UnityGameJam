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
        [SerializeField] private Button[] _rooms;
        [SerializeField] private Button _cancel;
        [SerializeField] private MinionStatusPanel _minionPanel;
        [SerializeField] private ProposalStackView _proposalStack;
        [SerializeField] private GameObject _workGauge;
        [SerializeField] private RectTransform _workFill;
        private MatchBootstrap _bootstrap;
        private EWorkerType? _talking;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();
        private int _lastSecond = -1;

        private void Awake()
        {
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            _choices.SetActive(false);
            EZoneId[] zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
            for (int i = 0; i < _rooms.Length; i++)
            {
                EZoneId zone = zones[i];
                _rooms[i].onClick.AddListener(() => Assign(zone));
            }
            _cancel.onClick.AddListener(Cancel);
        }

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
            _minionPanel.Show(session.Minions);
            _workGauge.SetActive(session.IsPlayerWorking);
            _workFill.anchorMax = new Vector2(Mathf.Clamp01(session.PlayerWorkProgress / 100f), 1f);

            MinionStatus? talking = null;
            if (session.MatchState == EMatchState.Running)
            {
                foreach (MinionStatus minion in session.Minions)
                {
                    if (minion.Activity == EMinionActivity.Talking) { talking = minion; break; }
                }
            }
            if (talking?.Minion != _talking)
            {
                if (talking.HasValue)
                {
                    MinionStatus minion = talking.Value;
                    _assignmentTitle.text = $"{MinionStatusPanel.WorkerName(minion.Minion)}에게 맡길 곳";
                    _assignmentRoom.text = $"현재 담당 방  {MinionStatusPanel.RoomName(minion.AssignedZone)}";
                    if (!_talking.HasValue) { OpenAssignmentPanel(); }
                }
                else { CloseAssignmentPanel(); }
                _talking = talking?.Minion;
            }
            if (_talking.HasValue && EventSystem.current != null &&
                EventSystem.current.currentInputModule is InputSystemUIInputModule input &&
                input.cancel?.action?.WasPressedThisFrame() == true) { Cancel(); }
            _proposalStack.Show(session, _bootstrap.InitialMatchTime - session.RemainingTime,
                (RectTransform)_choices.transform, talking.HasValue);
        }

        private void OnDisable()
        {
            if (_talking.HasValue)
            {
                CloseAssignmentPanel();
                _talking = null;
            }
        }

        private void OpenAssignmentPanel()
        {
            SuspendPlayerInput();
            _choices.SetActive(true);
            _rooms[0].Select();
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
            _choices.SetActive(false);
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
            _minionPanel.gameObject.SetActive(false);
            _proposalStack.gameObject.SetActive(false);
            _workGauge.SetActive(false);
            enabled = false;
        }

        private void OnDestroy()
        {
            RestorePlayerInput();
        }

        private void ShowTime(float remaining)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            if (seconds == _lastSecond) { return; }
            _lastSecond = seconds;
            _time.text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
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
