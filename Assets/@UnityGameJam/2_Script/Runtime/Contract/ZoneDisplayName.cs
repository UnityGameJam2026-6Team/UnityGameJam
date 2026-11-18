using System.Collections.Generic;

namespace UnityGameJam.Contract
{
    /// <summary>
    /// 구역의 표시 이름과 층. GDD §2의 이름을 그대로 쓴다.
    /// 제안 대사(A 레인)와 HUD·배정 패널(B 레인)이 같은 문구를 쓰도록 한 곳에 둔다.
    /// </summary>
    public static class ZoneDisplayName
    {
        private static readonly Dictionary<EZoneId, (string Name, EFloor Floor, bool IsLobby)> _zones =
            new Dictionary<EZoneId, (string, EFloor, bool)>
            {
                { EZoneId.Floor1Lobby, ("1층 로비", EFloor.Floor1, true) },
                { EZoneId.MultipurposeRoom, ("다목적실", EFloor.Floor1, false) },
                { EZoneId.Lounge, ("휴게실", EFloor.Floor1, false) },
                { EZoneId.Storage, ("창고", EFloor.Floor1, false) },
                { EZoneId.Floor2Lobby, ("2층 로비", EFloor.Floor2, true) },
                { EZoneId.MeetingRoom, ("회의실", EFloor.Floor2, false) },
                { EZoneId.Workshop, ("작업실", EFloor.Floor2, false) },
                { EZoneId.Floor3Lobby, ("3층 로비", EFloor.Floor3, true) },
                { EZoneId.ExecutiveRoom, ("간부실", EFloor.Floor3, false) },
            };

        /// <summary>"다목적실", "1층 로비"</summary>
        public static string GetName(EZoneId zone)
        {
            return _zones[zone].Name;
        }

        public static EFloor GetFloor(EZoneId zone)
        {
            return _zones[zone].Floor;
        }

        public static bool IsLobby(EZoneId zone)
        {
            return _zones[zone].IsLobby;
        }

        /// <summary>"1층"</summary>
        public static string GetFloorLabel(EFloor floor)
        {
            return $"{(int)floor + 1}층";
        }

        /// <summary>원격 안내용(GDD §9) "1층 다목적실". 로비는 이름에 층이 이미 있으므로 "2층 로비"만 쓴다.</summary>
        public static string GetFullName(EZoneId zone)
        {
            return IsLobby(zone) ? GetName(zone) : $"{GetFloorLabel(GetFloor(zone))} {GetName(zone)}";
        }
    }
}
