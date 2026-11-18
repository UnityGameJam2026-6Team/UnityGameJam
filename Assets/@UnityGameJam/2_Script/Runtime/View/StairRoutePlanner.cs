using System.Collections.Generic;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>계단으로 지나갈 층 목록(Spec MinionAgent 규칙 3). 출발 층은 빼고 도착 층은 넣는다.</summary>
    public static class StairRoutePlanner
    {
        public static List<EFloor> FloorsToPass(EFloor from, EFloor to)
        {
            var floors = new List<EFloor>();
            int step = to > from ? 1 : -1;
            for (int floor = (int)from; floor != (int)to; floor += step)
            {
                floors.Add((EFloor)(floor + step));
            }
            return floors;
        }
    }
}
