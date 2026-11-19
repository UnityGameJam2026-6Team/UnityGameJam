using System;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Facility
{
    internal sealed class ZoneMap
    {
        private readonly FacilityLayout _layout;

        public ZoneMap(FacilityLayout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public EZoneId? FindZone(Vector3 position, EFloor floor)
        {
            foreach (ZoneArea zone in _layout.Zones)
            {
                if (zone.Floor != floor) { continue; }

                foreach (Bounds area in zone.Areas)
                {
                    if (ContainsXZ(area, position)) { return zone.Zone; }
                }
            }
            return null;
        }

        public EZoneId ResolveDropZone(Vector3 position, EFloor floor)
        {
            return FindZone(position, floor) ?? (floor switch
            {
                EFloor.Floor1 => EZoneId.Floor1Lobby,
                EFloor.Floor2 => EZoneId.Floor2Lobby,
                EFloor.Floor3 => EZoneId.Floor3Lobby,
                _ => throw new ArgumentOutOfRangeException(nameof(floor))
            });
        }

        public bool IsInDischargeArea(Vector3 position, EFloor floor)
        {
            Bounds area = _layout.DischargeArea;
            return floor == EFloor.Floor1 && area.size.x > 0f && area.size.z > 0f && ContainsXZ(area, position);
        }

        public WasteBin FindWasteBinInReach(IWorkerBody body, float reach) =>
            _layout.FindNearestWasteBin(body.Floor, body.Position, reach);

        public HomeSlot FindHomeSlot(int slotId)
        {
            return _layout.HomeSlots.Find(slot => slot.SlotId == slotId);
        }

        private static bool ContainsXZ(Bounds area, Vector3 position)
        {
            Vector3 min = area.min;
            Vector3 max = area.max;
            return position.x >= min.x && position.x <= max.x
                && position.z >= min.z && position.z <= max.z;
        }
    }
}
