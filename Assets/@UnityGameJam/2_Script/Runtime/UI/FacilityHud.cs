using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private Button[] _rooms;
        [SerializeField] private Button _cancel;
        [SerializeField] private MinionStatusPanel _minionPanel;
        [SerializeField] private ProposalStackView _proposalStack;
        [SerializeField] private GameObject _workGauge;
        [SerializeField] private RectTransform _workFill;
        private MatchBootstrap _bootstrap;
        private EWorkerType? _talking;
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
            int seconds = Mathf.Max(0, Mathf.CeilToInt(session.RemainingTime));
            if (seconds != _lastSecond)
            {
                _lastSecond = seconds;
                _time.text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
            }
            EZoneId? zone = session.PlayerZone;
            _zone.text = zone.HasValue ? ZoneDisplayName.GetFullName(zone.Value) : ZoneDisplayName.GetFloorLabel(_bootstrap.Player.Floor);
            _zoneProgress.gameObject.SetActive(zone.HasValue);
            if (zone.HasValue) { ShowProgress(_zoneProgress, "지역 진척도", session.GetZoneCleanliness(zone.Value), session.ZoneThreshold); }
            ShowProgress(_facilityProgress, "전체 진행도", session.FacilityCleanliness, session.FacilityThreshold);
            _minionPanel.Show(session.Minions);
            _workGauge.SetActive(session.IsPlayerWorking);
            _workFill.anchorMax = new Vector2(Mathf.Clamp01(session.PlayerWorkProgress / 100f), 1f);

            EWorkerType? talking = null;
            if (session.MatchState == EMatchState.Running)
            {
                foreach (MinionStatus minion in session.Minions)
                {
                    if (minion.Activity == EMinionActivity.Talking) { talking = minion.Minion; break; }
                }
            }
            if (talking != _talking)
            {
                _talking = talking;
                _choices.SetActive(talking.HasValue);
                if (talking.HasValue) { _rooms[0].Select(); }
            }
            if (_talking.HasValue && EventSystem.current != null &&
                EventSystem.current.currentInputModule is InputSystemUIInputModule input &&
                input.cancel?.action?.WasPressedThisFrame() == true) { Cancel(); }
            _proposalStack.Show(session, _bootstrap.InitialMatchTime - session.RemainingTime,
                (RectTransform)_choices.transform, talking.HasValue);
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
