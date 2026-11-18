using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Zone Spawn Table", fileName = "ZoneSpawnTable")]
    public sealed class ZoneSpawnTableSO : ScriptableObject
    {
        [Header("구역별 초기 오염 개수")]
        [Tooltip("구역과 오염 유형 조합마다 한 행을 유지합니다. 최대 개수는 생성 지점과 제자리 슬롯 수 이내여야 합니다.")]
        [SerializeField] private ZoneSpawnRange[] _ranges = Array.Empty<ZoneSpawnRange>();

        [Header("시설 전체 초기 오염량")]
        [Tooltip("오염 개수에 유형별 가중치를 곱한 합의 최소값")]
        [SerializeField, Min(1)] private int _minimumFacilityWeight;
        [Tooltip("오염 개수에 유형별 가중치를 곱한 합의 최대값")]
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
