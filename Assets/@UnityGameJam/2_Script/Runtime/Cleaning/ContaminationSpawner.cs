using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Cleaning
{
    internal sealed class ContaminationSpawner
    {
        private static readonly EZoneId[] _zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private static readonly EContaminationType[] _types = (EContaminationType[])Enum.GetValues(typeof(EContaminationType));

        private readonly SpawnGroup[] _groups;
        private readonly IRandom _random;
        private readonly int _minimumFacilityWeight;
        private readonly int _maximumFacilityWeight;
        private readonly int _maximumAttempts;

        public ContaminationSpawner(FacilityLayout layout, ZoneSpawnTableSO table, ContaminationTypeSO types, IRandom random)
        {
            if (layout == null) { throw new ArgumentNullException(nameof(layout)); }
            if (table == null) { throw new ArgumentNullException(nameof(table)); }
            if (types == null) { throw new ArgumentNullException(nameof(types)); }
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _minimumFacilityWeight = table.MinimumFacilityWeight;
            _maximumFacilityWeight = table.MaximumFacilityWeight;
            _maximumAttempts = table.MaximumAttempts;
            if (_minimumFacilityWeight <= 0 || _maximumFacilityWeight < _minimumFacilityWeight || _maximumAttempts <= 0)
            {
                throw new ArgumentException("시설 시작 오염량 범위와 최대 추첨 횟수를 확인해야 합니다.", nameof(table));
            }

            _groups = BuildGroups(layout, table, types);
            long minimumPossibleWeight = _groups.Sum(group => (long)group.Range.Minimum * group.Weight);
            long maximumPossibleWeight = _groups.Sum(group => (long)group.Range.Maximum * group.Weight);
            if (minimumPossibleWeight > _maximumFacilityWeight || maximumPossibleWeight < _minimumFacilityWeight)
            {
                throw new ArgumentException("구역별 개수 범위로 시설 시작 오염량 범위를 맞출 수 없습니다.", nameof(table));
            }
        }

        public IReadOnlyList<ContaminationItem> Spawn()
        {
            var counts = new int[_groups.Length];
            for (int attempt = 0; attempt < _maximumAttempts; attempt++)
            {
                long weight = 0;
                for (int index = 0; index < _groups.Length; index++)
                {
                    SpawnGroup group = _groups[index];
                    ZoneSpawnRange range = group.Range;
                    int count = range.Minimum == range.Maximum
                        ? range.Minimum
                        : _random.Next(range.Minimum, range.Maximum + 1);
                    counts[index] = count;
                    weight += (long)count * group.Weight;
                }

                if (weight >= _minimumFacilityWeight && weight <= _maximumFacilityWeight)
                {
                    return CreateItems(counts);
                }
            }
            throw new InvalidOperationException("추첨 한도 안에 시설 시작 오염량 범위를 맞추지 못했습니다.");
        }

        private static SpawnGroup[] BuildGroups(FacilityLayout layout, ZoneSpawnTableSO table, ContaminationTypeSO types)
        {
            var layoutZones = new HashSet<EZoneId>(layout.Zones.Select(zone => zone.Zone));
            if (layout.Zones.Count != _zones.Length || !layoutZones.SetEquals(_zones))
            {
                throw new ArgumentException("시설에 각각 다른 판정 구역이 하나씩 있어야 합니다.", nameof(layout));
            }

            Dictionary<EContaminationType, int> weights = types.Entries.ToDictionary(entry => entry.Type, entry => entry.Weight);
            if (weights.Count != _types.Length || _types.Any(type => !weights.TryGetValue(type, out int weight) || weight <= 0))
            {
                throw new ArgumentException("오염 유형마다 양의 가중치가 하나씩 있어야 합니다.", nameof(types));
            }

            Dictionary<(EZoneId, EContaminationType), ZoneSpawnRange> ranges =
                table.Ranges.ToDictionary(range => (range.Zone, range.Type));
            if (ranges.Count != _zones.Length * _types.Length)
            {
                throw new ArgumentException("구역과 오염 유형 조합마다 생성 범위가 하나씩 있어야 합니다.", nameof(table));
            }

            if (layout.SpawnPoints.Select(point => point.Position).Distinct().Count() != layout.SpawnPoints.Count
                || layout.HomeSlots.Select(slot => slot.SlotId).Distinct().Count() != layout.HomeSlots.Count)
            {
                throw new ArgumentException("오염 생성 위치와 제자리 슬롯 ID는 각각 고유해야 합니다.", nameof(layout));
            }

            ILookup<(EZoneId, EContaminationType), Vector3> points =
                layout.SpawnPoints.ToLookup(point => (point.Zone, point.Type), point => point.Position);
            ILookup<EZoneId, int> slots = layout.HomeSlots.ToLookup(slot => slot.Zone, slot => slot.SlotId);
            var groups = new List<SpawnGroup>(ranges.Count);

            foreach (EZoneId zone in _zones)
            {
                long minimumZoneCount = 0;
                foreach (EContaminationType type in _types)
                {
                    if (!ranges.TryGetValue((zone, type), out ZoneSpawnRange range)
                        || range.Minimum < 0 || range.Maximum < range.Minimum)
                    {
                        throw new ArgumentException($"{ZoneDisplayName.GetName(zone)}의 {type} 생성 범위가 올바르지 않습니다.", nameof(table));
                    }

                    Vector3[] positions = points[(zone, type)].ToArray();
                    int[] homeSlots = type == EContaminationType.ClutterItem ? slots[zone].ToArray() : Array.Empty<int>();
                    if (range.Maximum > positions.Length
                        || (type == EContaminationType.ClutterItem && range.Maximum > homeSlots.Length))
                    {
                        throw new ArgumentException($"{ZoneDisplayName.GetName(zone)}의 {type} 생성 지점 또는 제자리 슬롯이 부족합니다.", nameof(layout));
                    }

                    minimumZoneCount += range.Minimum;
                    groups.Add(new SpawnGroup(range, weights[type], positions, homeSlots));
                }

                if (minimumZoneCount == 0)
                {
                    throw new ArgumentException($"{ZoneDisplayName.GetName(zone)}에 최소 한 개의 시작 오염이 필요합니다.", nameof(table));
                }
            }
            return groups.ToArray();
        }

        private IReadOnlyList<ContaminationItem> CreateItems(int[] counts)
        {
            var items = new List<ContaminationItem>();
            for (int groupIndex = 0; groupIndex < _groups.Length; groupIndex++)
            {
                int count = counts[groupIndex];
                if (count == 0) { continue; }

                SpawnGroup group = _groups[groupIndex];
                Vector3[] positions = Sample(group.Points, count);
                int[] slots = group.Range.Type == EContaminationType.ClutterItem
                    ? Sample(group.HomeSlots, count)
                    : Array.Empty<int>();

                for (int index = 0; index < positions.Length; index++)
                {
                    int? homeSlot = slots.Length > 0 ? slots[index] : (int?)null;
                    items.Add(new ContaminationItem(items.Count + 1, group.Range.Type, group.Weight,
                        group.Range.Zone, positions[index], homeSlot));
                }
            }
            return items.AsReadOnly();
        }

        private T[] Sample<T>(T[] candidates, int count)
        {
            var pool = (T[])candidates.Clone();
            var selection = new T[count];
            for (int index = 0; index < count; index++)
            {
                int chosen = _random.Next(index, pool.Length);
                selection[index] = pool[chosen];
                pool[chosen] = pool[index];
            }
            return selection;
        }

        private sealed class SpawnGroup
        {
            public ZoneSpawnRange Range { get; }
            public int Weight { get; }
            public Vector3[] Points { get; }
            public int[] HomeSlots { get; }

            public SpawnGroup(ZoneSpawnRange range, int weight, Vector3[] points, int[] homeSlots)
            {
                Range = range;
                Weight = weight;
                Points = points;
                HomeSlots = homeSlots;
            }
        }
    }
}
