using System;
using System.Collections.Generic;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Minion
{
    internal sealed class WorkerCapability
    {
        private readonly Dictionary<EWorkerType, WorkerStatsSO> _stats = new Dictionary<EWorkerType, WorkerStatsSO>();

        public WorkerCapability(IEnumerable<WorkerStatsSO> stats)
        {
            if (stats == null) { throw new ArgumentNullException(nameof(stats)); }

            foreach (WorkerStatsSO workerStats in stats)
            {
                if (workerStats == null || !Enum.IsDefined(typeof(EWorkerType), workerStats.Worker)
                    || !_stats.TryAdd(workerStats.Worker, workerStats))
                {
                    throw new ArgumentException("작업자별 능력 데이터가 하나씩 있어야 합니다.", nameof(stats));
                }
            }

            if (_stats.Count != Enum.GetValues(typeof(EWorkerType)).Length)
            {
                throw new ArgumentException("누락된 작업자 능력 데이터가 있습니다.", nameof(stats));
            }
        }

        public bool CanHandle(EWorkerType worker, EContaminationType type)
        {
            if (!_stats.TryGetValue(worker, out WorkerStatsSO stats)) { return false; }

            return type switch
            {
                EContaminationType.ClutterItem => stats.CanCarryClutter,
                EContaminationType.HeavyDebris => stats.CanCarryHeavyDebris || stats.CanDigestWaste,
                EContaminationType.Trash => stats.CanCarryTrash || stats.CanDigestWaste,
                EContaminationType.FloorStain => stats.CanMop,
                _ => false
            };
        }

        public bool CanDigest(EWorkerType worker, EContaminationType type)
        {
            return (type is EContaminationType.HeavyDebris or EContaminationType.Trash)
                && _stats.TryGetValue(worker, out WorkerStatsSO stats) && stats.CanDigestWaste;
        }
    }
}
