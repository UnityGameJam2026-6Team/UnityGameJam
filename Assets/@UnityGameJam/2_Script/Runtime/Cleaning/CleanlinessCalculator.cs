using System;
using System.Collections.Generic;
using UnityGameJam.Contract;

namespace UnityGameJam.Cleaning
{
    internal sealed class CleanlinessCalculator
    {
        private readonly ContaminationRegistry _registry;
        private readonly Dictionary<EZoneId, long> _initialZoneWeights = new Dictionary<EZoneId, long>();
        private readonly long _initialFacilityWeight;

        public CleanlinessCalculator(ContaminationRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                long weight = GetCurrentWeight(zone);
                if (weight == 0)
                {
                    throw new ArgumentException($"{ZoneDisplayName.GetName(zone)}에 시작 오염이 없습니다.", nameof(registry));
                }

                _initialZoneWeights.Add(zone, weight);
                _initialFacilityWeight += weight;
            }
        }

        public float GetZoneCleanliness(EZoneId zone)
        {
            return CalculatePercent(_initialZoneWeights[zone], GetCurrentWeight(zone));
        }

        public float GetFacilityCleanliness()
        {
            return CalculatePercent(_initialFacilityWeight, GetCurrentWeight(null));
        }

        private long GetCurrentWeight(EZoneId? zone)
        {
            long weight = 0;
            foreach (ContaminationItem item in _registry.Items)
            {
                if (item.IsCounted && (!zone.HasValue || item.Zone == zone))
                {
                    weight += item.Weight;
                }
            }
            return weight;
        }

        private static float CalculatePercent(long initialWeight, long currentWeight)
        {
            double percent = 100d * (initialWeight - currentWeight) / initialWeight;
            return (float)Math.Clamp(percent, 0d, 100d);
        }
    }
}
