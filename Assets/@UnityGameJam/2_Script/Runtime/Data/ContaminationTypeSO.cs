using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Contamination Type", fileName = "ContaminationType")]
    public sealed class ContaminationTypeSO : ScriptableObject
    {
        [SerializeField] private ContaminationTypeEntry[] _entries = Array.Empty<ContaminationTypeEntry>();
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
