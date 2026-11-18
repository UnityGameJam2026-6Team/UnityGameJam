using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    public sealed class MinionStatusPanel : MonoBehaviour
    {
        [SerializeField] private InputActionReference _toggle;
        [SerializeField] private TMP_Text _rows;
        [SerializeField] private GameObject _body;

        private readonly StringBuilder _text = new StringBuilder(192);

        private void OnEnable()
        {
            _toggle.action.performed += OnToggle;
            _toggle.action.Enable();
        }

        private void OnDisable()
        {
            _toggle.action.performed -= OnToggle;
            _toggle.action.Disable();
        }

        private void OnToggle(InputAction.CallbackContext context)
        {
            _body.SetActive(!_body.activeSelf);
        }

        public void Show(IReadOnlyList<MinionStatus> minions)
        {
            if (!_body.activeSelf) { return; }
            _text.Clear();
            for (int i = 0; i < minions.Count; i++)
            {
                MinionStatus status = minions[i];
                if (i > 0) { _text.AppendLine(); }
                string room = status.AssignedZone.HasValue ? ZoneDisplayName.GetFullName(status.AssignedZone.Value) : "미배정";
                _text.Append(WorkerName(status.Minion)).Append("   ").Append(room).Append("  ·  ").Append(ActivityName(status.Activity));
            }
            _rows.SetText(_text);
        }

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
