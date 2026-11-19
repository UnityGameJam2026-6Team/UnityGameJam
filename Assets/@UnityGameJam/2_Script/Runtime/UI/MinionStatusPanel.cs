using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    public sealed class MinionStatusPanel : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Slot
        {
            [SerializeField] internal EWorkerType _worker;
            [SerializeField] internal TMP_Text _name;
            [SerializeField] internal TMP_Text _room;
            [SerializeField] internal TMP_Text _activity;
            internal bool HasStatus;
            internal EZoneId? LastZone;
            internal EMinionActivity LastActivity;
        }

        [SerializeField] private Slot[] _slots = System.Array.Empty<Slot>();
        [SerializeField] private InputActionReference _toggle;
        [SerializeField] private RectTransform _body;
        [SerializeField] private TMP_Text _title;
        private InputAction _toggleAction;
        private bool _ownsToggleAction;
        private float _expandedHeight;
        private float _collapsedHeight;

        public bool IsToggleBlocked { get; set; }

        internal RectTransform Rect { get; private set; }
        internal float HeightOffset => Rect.rect.height - _expandedHeight;

        private void Awake()
        {
            Rect = (RectTransform)transform;
            _expandedHeight = Rect.rect.height;
            _collapsedHeight = ((RectTransform)_title.transform.parent).rect.height;
            SetExpanded(_body.gameObject.activeSelf);
        }

        private void OnEnable()
        {
            _toggleAction = _toggle?.action;
            if (_toggleAction != null)
            {
                _ownsToggleAction = !_toggleAction.enabled;
                _toggleAction.performed += OnToggle;
                if (_ownsToggleAction) { _toggleAction.Enable(); }
            }

            foreach (Slot slot in _slots)
            {
                slot.HasStatus = false;
                if (slot._name != null) { slot._name.text = WorkerName(slot._worker); }
                if (slot._room != null) { slot._room.text = "미배정"; }
                if (slot._activity != null) { slot._activity.text = "정보 없음"; }
            }
        }

        private void OnDisable()
        {
            if (_toggleAction == null) { return; }
            _toggleAction.performed -= OnToggle;
            if (_ownsToggleAction) { _toggleAction.Disable(); }
            _toggleAction = null;
        }

        private void OnToggle(InputAction.CallbackContext context)
        {
            if (IsToggleBlocked || GameMenu.BlocksGameplayInput) { return; }
            bool isExpanded = !_body.gameObject.activeSelf;
            SetExpanded(isExpanded);
            GameSfxPlayer.Play(isExpanded ? ESfxCue.PanelOpen : ESfxCue.PanelClose);
        }

        internal void SetExpanded(bool isExpanded)
        {
            _body.gameObject.SetActive(isExpanded);
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, isExpanded ? _expandedHeight : _collapsedHeight);
            _title.text = isExpanded ? "졸개 현황  ·  Tab 접기" : "졸개 현황  ·  Tab 펼치기";
        }

        public void Show(IReadOnlyList<MinionStatus> minions)
        {
            foreach (Slot slot in _slots)
            {
                MinionStatus? found = null;
                if (minions != null)
                {
                    for (int i = 0; i < minions.Count; i++)
                    {
                        if (minions[i].Minion == slot._worker) { found = minions[i]; break; }
                    }
                }
                if (!found.HasValue)
                {
                    if (slot.HasStatus)
                    {
                        slot._room.text = "미배정";
                        slot._activity.text = "정보 없음";
                        slot.HasStatus = false;
                    }
                    continue;
                }
                MinionStatus status = found.Value;
                if (!slot.HasStatus || slot.LastZone != status.AssignedZone)
                {
                    slot._room.text = RoomName(status.AssignedZone);
                    slot.LastZone = status.AssignedZone;
                }
                if (!slot.HasStatus || slot.LastActivity != status.Activity)
                {
                    slot._activity.text = ActivityName(status.Activity);
                    slot.LastActivity = status.Activity;
                }
                slot.HasStatus = true;
            }
        }

        internal static string RoomName(EZoneId? zone) => zone.HasValue ? ZoneDisplayName.GetFullName(zone.Value) : "미배정";

        internal static string WorkerName(EWorkerType worker) => worker switch
        {
            EWorkerType.Goblin => "고블린",
            EWorkerType.Orc => "오크",
            EWorkerType.Skeleton => "스켈레톤",
            EWorkerType.Slime => "슬라임",
            EWorkerType.Hellhound => "헬하운드",
            _ => "기타"
        };

        private static string ActivityName(EMinionActivity activity) => activity switch
        {
            EMinionActivity.LobbyIdle => "대기",
            EMinionActivity.Talking => "대화 중",
            EMinionActivity.MovingToRoom => "이동 중",
            EMinionActivity.Surveying => "탐색 중",
            EMinionActivity.Working => "작업 중",
            EMinionActivity.CarryingWaste => "폐기물 운반",
            EMinionActivity.HandlingMop => "걸레 대여·반납",
            EMinionActivity.AwaitingResponse => "응답 대기",
            EMinionActivity.ReturningToLobby => "로비 복귀",
            EMinionActivity.Dispatching => "배출 중",
            _ => "기타"
        };
    }
}
