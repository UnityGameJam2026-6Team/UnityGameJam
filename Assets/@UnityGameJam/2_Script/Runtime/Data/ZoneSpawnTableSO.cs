using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Zone Spawn Table", fileName = "ZoneSpawnTable")]
    public sealed class ZoneSpawnTableSO : ScriptableObject
    {
        [SerializeField] private ZoneSpawnRange[] _ranges = Array.Empty<ZoneSpawnRange>();
        [SerializeField, Min(1)] private int _minimumFacilityWeight;
        [SerializeField, Min(1)] private int _maximumFacilityWeight;
        [SerializeField, Min(1)] private int _maximumAttempts;

        internal IReadOnlyList<ZoneSpawnRange> Ranges => _ranges;
        public int MinimumFacilityWeight => _minimumFacilityWeight;
        public int MaximumFacilityWeight => _maximumFacilityWeight;
        public int MaximumAttempts => _maximumAttempts;
    }

    [Serializable]
    internal struct ZoneSpawnRange
    {
        [SerializeField] private EZoneId _zone;
        [SerializeField] private EContaminationType _type;
        [SerializeField, Min(0)] private int _minimum;
        [SerializeField, Min(0)] private int _maximum;

        public EZoneId Zone => _zone;
        public EContaminationType Type => _type;
        public int Minimum => _minimum;
        public int Maximum => _maximum;
    }
}
