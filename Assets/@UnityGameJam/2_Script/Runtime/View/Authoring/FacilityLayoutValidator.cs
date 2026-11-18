using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 씬에서 뽑은 FacilityLayout이 Spec FacilityLayout 규칙 1~9를 지키는지 검사한다.
    /// 규칙(A 레인)은 이 데이터를 그대로 믿으므로, 마커 배치 실수를 여기서 잡는다.
    /// 판정은 XZ 평면 기준이다(높이는 무시).
    /// </summary>
    public static class FacilityLayoutValidator
    {
        private const float Epsilon = 0.001f;
        private const int MinPatrolPoints = 3;

        public static List<string> Validate(FacilityLayout layout)
        {
            var errors = new List<string>();
            ValidateZones(layout, errors);
            ValidateToilets(layout, errors);
            ValidateDischargeArea(layout, errors);
            ValidateStartPositions(layout, errors);
            ValidatePatrolRoutes(layout, errors);
            ValidateSpawnPoints(layout, errors);
            ValidateHomeSlots(layout, errors);
            if (layout.Floors.Select(f => f.Floor).Distinct().Count() != 3 || layout.Floors.Count != 3)
            {
                errors.Add($"층 범위는 층마다 하나씩 3개여야 합니다(현재 {layout.Floors.Count}개).");
            }
            return errors;
        }

        public static bool ContainsXZ(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x - Epsilon && point.x <= bounds.max.x + Epsilon
                && point.z >= bounds.min.z - Epsilon && point.z <= bounds.max.z + Epsilon;
        }

        /// <summary>맞닿기만 한 경우는 겹친 것으로 보지 않는다.</summary>
        public static bool OverlapsXZ(Bounds a, Bounds b)
        {
            return a.min.x < b.max.x - Epsilon && b.min.x < a.max.x - Epsilon
                && a.min.z < b.max.z - Epsilon && b.min.z < a.max.z - Epsilon;
        }

        public static bool IsInZone(FacilityLayout layout, EZoneId zone, Vector3 point)
        {
            ZoneArea area = layout.Zones.FirstOrDefault(z => z.Zone == zone);
            return area != null && area.Areas.Any(box => ContainsXZ(box, point));
        }

        private static void ValidateZones(FacilityLayout layout, List<string> errors)
        {
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                List<ZoneArea> found = layout.Zones.Where(z => z.Zone == zone).ToList();
                if (found.Count != 1)
                {
                    errors.Add($"구역 {zone}이(가) {found.Count}개입니다. 정확히 1개여야 합니다.");
                    continue;
                }
                if (found[0].Floor != ZoneDisplayName.GetFloor(zone))
                {
                    errors.Add($"구역 {zone}의 층이 {found[0].Floor}입니다. GDD상 {ZoneDisplayName.GetFloor(zone)}입니다.");
                }
                if (found[0].Areas.Count == 0)
                {
                    errors.Add($"구역 {zone}에 사각형이 없습니다.");
                }
            }

            var boxes = layout.Zones.SelectMany(z => z.Areas.Select(box => (z.Zone, box))).ToList();
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    if (OverlapsXZ(boxes[i].box, boxes[j].box))
                    {
                        errors.Add($"구역 사각형이 겹칩니다: {boxes[i].Zone} {boxes[i].box.center} / {boxes[j].Zone} {boxes[j].box.center}");
                    }
                }
            }
        }

        private static void ValidateToilets(FacilityLayout layout, List<string> errors)
        {
            foreach (EFloor floor in Enum.GetValues(typeof(EFloor)))
            {
                int count = layout.Toilets.Count(t => t.Floor == floor);
                if (count != 1) { errors.Add($"{floor} 화장실이 {count}개입니다. 층마다 1개여야 합니다."); }
            }

            foreach (ToiletStation toilet in layout.Toilets)
            {
                if (layout.Zones.SelectMany(z => z.Areas).Any(box => OverlapsXZ(box, toilet.Area)))
                {
                    errors.Add($"{toilet.Floor} 화장실이 판정 구역과 겹칩니다. 화장실은 판정에서 제외합니다.");
                }
                if (!ContainsXZ(toilet.Area, toilet.MopPickupPosition))
                {
                    errors.Add($"{toilet.Floor} 화장실의 걸레 위치가 화장실 범위 밖입니다.");
                }
            }
        }

        private static void ValidateDischargeArea(FacilityLayout layout, List<string> errors)
        {
            Bounds discharge = layout.DischargeArea;
            ZoneArea lobby = layout.Zones.FirstOrDefault(z => z.Zone == EZoneId.Floor1Lobby);
            bool isInside = lobby != null
                && lobby.Areas.Any(box => ContainsXZ(box, discharge.min) && ContainsXZ(box, discharge.max));
            if (!isInside || discharge.size.x <= Epsilon || discharge.size.z <= Epsilon)
            {
                errors.Add("배출 구역은 1층 로비 사각형 하나 안에 들어가야 합니다.");
            }
        }

        private static void ValidateStartPositions(FacilityLayout layout, List<string> errors)
        {
            foreach (EWorkerType worker in Enum.GetValues(typeof(EWorkerType)))
            {
                if (!layout.StartPositions.ContainsKey(worker)) { errors.Add($"{worker}의 시작점이 없습니다."); }
            }
        }

        private static void ValidatePatrolRoutes(FacilityLayout layout, List<string> errors)
        {
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                List<PatrolRoute> routes = layout.PatrolRoutes.Where(r => r.Zone == zone).ToList();
                if (routes.Count != 1)
                {
                    errors.Add($"구역 {zone}의 순찰 경로가 {routes.Count}개입니다. 1개여야 합니다.");
                    continue;
                }
                if (routes[0].Points.Count < MinPatrolPoints)
                {
                    errors.Add($"구역 {zone}의 순찰점이 {routes[0].Points.Count}개입니다. {MinPatrolPoints}개 이상이어야 합니다.");
                }
                foreach (Vector3 point in routes[0].Points.Where(p => !IsInZone(layout, zone, p)))
                {
                    errors.Add($"구역 {zone}의 순찰점 {point}이(가) 구역 밖입니다.");
                }
            }
        }

        private static void ValidateSpawnPoints(FacilityLayout layout, List<string> errors)
        {
            foreach (SpawnPoint spawn in layout.SpawnPoints.Where(s => !IsInZone(layout, s.Zone, s.Position)))
            {
                errors.Add($"생성 지점({spawn.Type}) {spawn.Position}이(가) 구역 {spawn.Zone} 밖입니다.");
            }
        }

        private static void ValidateHomeSlots(FacilityLayout layout, List<string> errors)
        {
            foreach (IGrouping<int, HomeSlot> group in layout.HomeSlots.GroupBy(s => s.SlotId).Where(g => g.Count() > 1))
            {
                errors.Add($"제자리 슬롯 ID {group.Key}이(가) {group.Count()}번 쓰였습니다.");
            }
            foreach (HomeSlot slot in layout.HomeSlots.Where(s => !IsInZone(layout, s.Zone, s.Position)))
            {
                errors.Add($"제자리 슬롯 {slot.SlotId} {slot.Position}이(가) 구역 {slot.Zone} 밖입니다.");
            }
        }
    }
}
