using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Contamination Type", fileName = "ContaminationType")]
    public sealed class ContaminationTypeSO : ScriptableObject
    {
        [Tooltip("오염 유형별로 청결도와 시설 전체 초기 오염량 계산에 사용하는 가중치")]
        [SerializeField] private ContaminationTypeEntry[] _entries = Array.Empty<ContaminationTypeEntry>();
        [Tooltip("플레이어가 바닥 얼룩 하나를 닦는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _playerMopSeconds;

        internal IReadOnlyList<ContaminationTypeEntry> Entries => _entries;
        public float PlayerMopSeconds => _playerMopSeconds;
    }

    [Serializable]
    internal struct ContaminationTypeEntry
    {
        [SerializeField] private EContaminationType _type;
        [SerializeField, Min(1)] private int _weight;

        public EContaminationType Type => _type;
        public int Weight => _weight;
    }
}
