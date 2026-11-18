using System.Text;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.View
{
    /// <summary>
    /// B5(HUD) 전까지 쓰는 디버그 상태 표시(왼쪽 위 텍스트). 실제 HUD가 생기면 지운다.
    /// 원격 정보 제한(GDD §2)을 지키지 않는 디버그용이다.
    /// </summary>
    public sealed class DebugMatchOverlay : MonoBehaviour
    {
        [SerializeField] private MatchBootstrap _bootstrap;
        [SerializeField] private bool _isVisible = true;

        private static readonly EWorkerType[] _assignableMinions = { EWorkerType.Goblin, EWorkerType.Orc, EWorkerType.Skeleton, EWorkerType.Slime };
        private static readonly EZoneId[] _zones = (EZoneId[])System.Enum.GetValues(typeof(EZoneId));

        private readonly StringBuilder _builder = new StringBuilder();
        private readonly System.Collections.Generic.Dictionary<EWorkerType, int> _nextZone = new System.Collections.Generic.Dictionary<EWorkerType, int>();
        private GUIStyle _style;
        private PlayerInteractor _interactor;
        private PlayerController _controller;

        private void OnGUI()
        {
            IMatchSession session = _bootstrap != null ? _bootstrap.Session : null;
            if (!_isVisible || session == null) { return; }
            if (_style == null) { _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16 }; }

            _builder.Clear();
            _builder.AppendLine($"[디버그] {session.MatchState}  남은 시간 {session.RemainingTime:0.0}s");
            EZoneId? zone = session.PlayerZone;
            _builder.AppendLine(zone.HasValue
                ? $"구역: {ZoneDisplayName.GetFullName(zone.Value)}  청결도 {session.GetZoneCleanliness(zone.Value):0}%"
                : "구역: (판정 구역 밖)");
            _builder.AppendLine($"시설 청결도 {session.FacilityCleanliness:0}%   속도 {session.PlayerMoveSpeed:0.0}m/s");
            _builder.AppendLine($"운반: {(session.PlayerCarriedItemId.HasValue ? "#" + session.PlayerCarriedItemId.Value : "-")}   걸레: {(session.IsPlayerHoldingMop ? "O" : "-")}");
            if (session.IsPlayerWorking) { _builder.AppendLine($"작업 중 {session.PlayerWorkProgress:0}%"); }
            if (_interactor == null && _bootstrap.Player != null) { _interactor = _bootstrap.Player.GetComponent<PlayerInteractor>(); }
            if (_interactor != null) { _builder.AppendLine($"E: {_interactor.CurrentChoice.Kind}   Q: 내려놓기"); }
            if (_controller == null && _bootstrap.Player != null) { _controller = _bootstrap.Player.GetComponent<PlayerController>(); }
            if (_controller != null && _controller.IsInStairTransit) { _builder.AppendLine("계단 이동 중..."); }
            else if (_controller != null && _controller.IsNearStair) { _builder.AppendLine("계단: 1 아래층 / 2 위층"); }
            GUI.Box(new Rect(10f, 10f, 420f, 170f), _builder.ToString(), _style);

            DrawMinionButtons(session);
        }

        /// <summary>B6 배정 패널 전까지 쓰는 디버그 배정. 누를 때마다 다음 구역으로 배정한다.</summary>
        private void DrawMinionButtons(IMatchSession session)
        {
            float y = 190f;
            foreach (MinionStatus status in session.Minions)
            {
                string zone = status.AssignedZone.HasValue ? ZoneDisplayName.GetFullName(status.AssignedZone.Value) : "-";
                string label = $"{status.Minion} [{status.Activity}] {zone}";
                if (System.Array.IndexOf(_assignableMinions, status.Minion) >= 0)
                {
                    _nextZone.TryGetValue(status.Minion, out int next);
                    if (GUI.Button(new Rect(10f, y, 420f, 26f), label + $"  → {ZoneDisplayName.GetFullName(_zones[next])}"))
                    {
                        session.AssignRoom(status.Minion, _zones[next]);
                        _nextZone[status.Minion] = (next + 1) % _zones.Length;
                    }
                }
                else
                {
                    GUI.Box(new Rect(10f, y, 420f, 26f), label, _style);
                }
                y += 30f;
            }
        }
    }
}
