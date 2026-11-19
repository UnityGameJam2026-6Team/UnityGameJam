using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 층 프리팹(FloorAuthoring)과 그 아래 마커를 모아 계약 데이터 FacilityLayout으로 만든다.
    /// Bootstrap이 층 인스턴스를 인스펙터로 넘겨 호출한다. 그래서 씬 전체를 Find하지 않는다.
    /// </summary>
    public static class FacilityLayoutBuilder
    {
        /// <param name="warnings">구역을 찾지 못한 마커처럼 데이터에 담을 수 없는 문제를 모은다.</param>
        public static FacilityLayout Build(IReadOnlyList<FloorAuthoring> floors, List<string> warnings)
        {
            var layout = new FacilityLayout();
            var zoneMap = new Dictionary<EZoneId, ZoneArea>();
            var dischargeAreas = new List<Bounds>();

            foreach (FloorAuthoring floor in floors.Where(f => f != null))
            {
                layout.Floors.Add(new FloorArea { Floor = floor.Floor, Area = floor.GetWorldBounds() });

                foreach (ZoneVolumeAuthoring volume in floor.GetComponentsInChildren<ZoneVolumeAuthoring>(true))
                {
                    if (!zoneMap.TryGetValue(volume.Zone, out ZoneArea area))
                    {
                        area = new ZoneArea { Zone = volume.Zone, Floor = floor.Floor };
                        zoneMap[volume.Zone] = area;
                        layout.Zones.Add(area);
                    }
                    area.Areas.Add(volume.GetWorldBounds());
                }

                foreach (ToiletAuthoring toilet in floor.GetComponentsInChildren<ToiletAuthoring>(true))
                {
                    layout.Toilets.Add(new ToiletStation { Floor = floor.Floor, Area = toilet.GetWorldBounds(), MopPickupPosition = toilet.MopPickupPosition });
                }

                foreach (WasteBinAuthoring bin in floor.GetComponentsInChildren<WasteBinAuthoring>())
                {
                    layout.WasteBins.Add(new WasteBin { Floor = floor.Floor, Position = bin.UsePosition });
                }

                foreach (PatrolRouteAuthoring route in floor.GetComponentsInChildren<PatrolRouteAuthoring>(true))
                {
                    var patrol = new PatrolRoute { Zone = route.Zone };
                    patrol.Points.AddRange(route.GetPoints());
                    layout.PatrolRoutes.Add(patrol);
                }

                foreach (StartPointAuthoring start in floor.GetComponentsInChildren<StartPointAuthoring>(true))
                {
                    if (layout.StartPositions.ContainsKey(start.Worker)) { warnings.Add($"{start.Worker}의 시작점이 중복입니다: {start.name}"); }
                    layout.StartPositions[start.Worker] = start.transform.position;
                }

                dischargeAreas.AddRange(floor.GetComponentsInChildren<DischargeAreaAuthoring>(true).Select(d => d.GetWorldBounds()));
            }

            if (dischargeAreas.Count != 1) { warnings.Add($"배출 구역이 {dischargeAreas.Count}개입니다. 1개여야 합니다."); }
            if (dischargeAreas.Count > 0) { layout.DischargeArea = dischargeAreas[0]; }

            // 생성 지점과 제자리 슬롯은 구역 볼륨이 모두 모인 뒤 위치로 구역을 정한다.
            foreach (FloorAuthoring floor in floors.Where(f => f != null))
            {
                foreach (SpawnPointAuthoring spawn in floor.GetComponentsInChildren<SpawnPointAuthoring>(true))
                {
                    Vector3 position = spawn.transform.position;
                    if (TryFindZone(layout, position, out EZoneId zone)) { layout.SpawnPoints.Add(new SpawnPoint { Zone = zone, Type = spawn.Type, Position = position }); }
                    else { warnings.Add($"생성 지점 {spawn.name} {position}이(가) 어떤 구역에도 속하지 않습니다."); }
                }

                foreach (HomeSlotAuthoring slot in floor.GetComponentsInChildren<HomeSlotAuthoring>(true))
                {
                    Vector3 position = slot.transform.position;
                    if (TryFindZone(layout, position, out EZoneId zone)) { layout.HomeSlots.Add(new HomeSlot { SlotId = slot.SlotId, Zone = zone, Position = position }); }
                    else { warnings.Add($"제자리 슬롯 {slot.SlotId} {position}이(가) 어떤 구역에도 속하지 않습니다."); }
                }
            }
            return layout;
        }

        public static bool TryFindZone(FacilityLayout layout, Vector3 position, out EZoneId zone)
        {
            foreach (ZoneArea area in layout.Zones)
            {
                if (area.Areas.Any(box => FacilityLayoutValidator.ContainsXZ(box, position)))
                {
                    zone = area.Zone;
                    return true;
                }
            }
            zone = default;
            return false;
        }
    }
}
