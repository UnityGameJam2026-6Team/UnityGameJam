using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;

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

        public override IMatchSession Create(MatchSetup setup)
        {
            if (setup == null) { throw new System.ArgumentNullException(nameof(setup)); }

            var spawner = new ContaminationSpawner(setup.Layout, _spawnTable, _types, new SeededRandom(setup.Seed));
            return new MatchSimulation(setup, _rules, _types, _workerStats, spawner.Spawn());
        }
    }
}
