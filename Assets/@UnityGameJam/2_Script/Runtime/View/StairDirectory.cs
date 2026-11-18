using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>층 → 계단 출입 지점. 층 프리팹 안의 StairAuthoring에서 만든다(B 레인 전용, 규칙은 모른다).</summary>
    public sealed class StairDirectory
    {
        private readonly Dictionary<EFloor, Vector3> _entries = new Dictionary<EFloor, Vector3>();

        public static StairDirectory FromFloors(IEnumerable<FloorAuthoring> floors)
        {
            var directory = new StairDirectory();
            foreach (FloorAuthoring floor in floors)
            {
                if (floor == null) { continue; }
                StairAuthoring stair = floor.GetComponentInChildren<StairAuthoring>(true);
                if (stair != null) { directory._entries[floor.Floor] = stair.EntryPosition; }
            }
            return directory;
        }

        public bool TryGet(EFloor floor, out Vector3 entry)
        {
            return _entries.TryGetValue(floor, out entry);
        }
    }
}
