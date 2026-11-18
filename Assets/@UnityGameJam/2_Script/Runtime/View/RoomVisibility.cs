using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>플레이어가 처음 들어간 방의 가림을 한 판 동안 해제한다(U13).</summary>
    internal sealed class RoomVisibility
    {
        private readonly List<(ZoneArea Room, List<GameObject> Covers)> _unvisited = new();

        public RoomVisibility(FacilityLayout layout, GameObject coverPrefab, Transform parent)
        {
            foreach (ZoneArea room in layout.Zones)
            {
                if (ZoneDisplayName.IsLobby(room.Zone)) { continue; }
                var covers = new List<GameObject>(room.Areas.Count);
                foreach (Bounds bounds in room.Areas)
                {
                    GameObject cover = Object.Instantiate(coverPrefab, bounds.center, Quaternion.identity);
                    cover.transform.localScale = bounds.size;
                    cover.transform.SetParent(parent, true);
                    covers.Add(cover);
                }
                _unvisited.Add((room, covers));
            }
        }

        public void RevealAt(EFloor floor, Vector3 playerPosition)
        {
            for (int i = _unvisited.Count - 1; i >= 0; i--)
            {
                (ZoneArea room, List<GameObject> covers) = _unvisited[i];
                if (room.Floor != floor) { continue; }
                foreach (Bounds bounds in room.Areas)
                {
                    if (!FacilityLayoutValidator.ContainsXZ(bounds, playerPosition)) { continue; }
                    foreach (GameObject cover in covers) { cover.SetActive(false); }
                    _unvisited.RemoveAt(i);
                    break;
                }
            }
        }

        public bool IsCovered(EFloor floor, Vector3 position)
        {
            foreach (var (room, _) in _unvisited)
            {
                if (room.Floor != floor) { continue; }
                foreach (Bounds bounds in room.Areas)
                {
                    if (FacilityLayoutValidator.ContainsXZ(bounds, position)) { return true; }
                }
            }
            return false;
        }
    }
}
