using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>각 층에서 인접 층으로 이어지는 출입 지점을 연결한다.</summary>
    public sealed class StairDirectory
    {
        private readonly Dictionary<(EFloor From, EFloor To), Vector3> _entries = new();
        internal IReadOnlyDictionary<(EFloor From, EFloor To), Vector3> Entries => _entries;

        public static StairDirectory FromFloors(IEnumerable<FloorAuthoring> floors)
        {
            var directory = new StairDirectory();
            foreach (FloorAuthoring floor in floors)
            {
                if (floor == null) { continue; }
                foreach (StairAuthoring stair in floor.GetComponentsInChildren<StairAuthoring>(true))
                {
                    EFloor target = floor.Floor + (stair.GoesUp ? 1 : -1);
                    directory._entries[(floor.Floor, target)] = stair.EntryPosition;
                }
            }
            return directory;
        }

        public bool TryGet(EFloor from, EFloor to, out Vector3 entry)
        {
            return _entries.TryGetValue((from, to), out entry);
        }

        public bool TryFindExit(EFloor floor, Vector3 position, float radius, out Vector3 exit)
        {
            float nearest = radius * radius;
            bool found = false;
            exit = default;
            foreach (var link in _entries)
            {
                if (link.Key.From != floor || !TryGet(link.Key.To, floor, out Vector3 arrival)) { continue; }
                Vector3 offset = position - link.Value;
                offset.y = 0f;
                if (offset.sqrMagnitude > nearest) { continue; }
                nearest = offset.sqrMagnitude;
                exit = arrival;
                found = true;
            }
            return found;
        }
    }
}
