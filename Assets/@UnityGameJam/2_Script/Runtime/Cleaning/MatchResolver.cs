using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityGameJam.Contract;

namespace UnityGameJam.Cleaning
{
    internal static class MatchResolver
    {
        public static MatchResult Resolve(ContaminationRegistry registry, CleanlinessCalculator cleanliness,
            IReadOnlyDictionary<EWorkerType, IWorkerBody> bodies, float zoneThreshold, float facilityThreshold)
        {
            registry.DropAllCarried(bodies);
            float facility = cleanliness.GetFacilityCleanliness();
            bool isSuccess = facility >= facilityThreshold;
            var zones = new Dictionary<EZoneId, float>();
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                float score = cleanliness.GetZoneCleanliness(zone);
                zones.Add(zone, score);
                isSuccess &= score >= zoneThreshold;
            }
            return new MatchResult(isSuccess, facility, new ReadOnlyDictionary<EZoneId, float>(zones));
        }
    }
}
