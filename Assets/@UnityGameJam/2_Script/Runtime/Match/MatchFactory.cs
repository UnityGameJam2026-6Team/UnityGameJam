using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Proposal;

namespace UnityGameJam.Match
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Match Factory", fileName = "MatchFactory")]
    public sealed class MatchFactory : MatchSessionFactoryBase
    {
        [SerializeField] private GameRuleSO _rules;
        [SerializeField] private ContaminationTypeSO _types;
        [SerializeField] private ZoneSpawnTableSO _spawnTable;
        [SerializeField] private WorkerStatsSO[] _workerStats;

        public override float? InteractionDistance => _rules != null ? _rules.InteractionDistance : null;
        public override float? MatchDuration => _rules != null ? _rules.MatchSeconds : null;

        public void ValidateInitialContamination(FacilityLayout layout, int seed) => Spawn(layout, seed);

        public override IMatchSession Create(MatchSetup setup)
        {
            if (setup == null) { throw new System.ArgumentNullException(nameof(setup)); }

            IProposalDialogueProvider dialogue = null;
#if UNITY_EDITOR
            if (_rules != null) { dialogue = OpenAiProposalDialogueProvider.Create(_rules.ApiTimeoutSeconds); }
#endif
            return new MatchSimulation(setup, _rules, _types, _workerStats, Spawn(setup.Layout, setup.Seed), dialogue);
        }

        private IReadOnlyList<ContaminationItem> Spawn(FacilityLayout layout, int seed)
        {
            return new ContaminationSpawner(layout, _spawnTable, _types, new SeededRandom(seed)).Spawn();
        }
    }
}
