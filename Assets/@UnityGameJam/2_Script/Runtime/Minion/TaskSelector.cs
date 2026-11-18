using System;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;

namespace UnityGameJam.Minion
{
    internal sealed class TaskSelector
    {
        private readonly ContaminationRegistry _registry;
        private readonly WorkerCapability _capability;

        public TaskSelector(ContaminationRegistry registry, WorkerCapability capability)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public ContaminationItem FindTask(EWorkerType worker, EZoneId zone, Vector3 position, bool isHoldingMop)
        {
            ContaminationItem best = null;
            (int Priority, float DistanceSquared, int ItemId) bestRank = default;

            foreach (ContaminationItem item in _registry.Items)
            {
                if (!IsCandidate(item, worker, zone, isHoldingMop)) { continue; }

                var rank = (GetPriority(item.Type), (item.Position - position).sqrMagnitude, item.Id);
                if (best == null || rank.CompareTo(bestRank) < 0)
                {
                    best = item;
                    bestRank = rank;
                }
            }
            return best;
        }

        private bool IsCandidate(ContaminationItem item, EWorkerType worker, EZoneId zone, bool isHoldingMop)
        {
            if (item.Zone != zone || (item.ClaimedBy.HasValue && item.ClaimedBy != worker)) { return false; }
            if (isHoldingMop && item.Type != EContaminationType.FloorStain) { return false; }

            return item.State switch
            {
                EItemState.OnFloor => _capability.CanHandle(worker, item.Type),
                EItemState.InDischargeArea => _capability.CanDigest(worker, item.Type),
                _ => false
            };
        }

        private static int GetPriority(EContaminationType type)
        {
            return type switch
            {
                EContaminationType.ClutterItem => 0,
                EContaminationType.HeavyDebris => 1,
                EContaminationType.Trash => 2,
                EContaminationType.FloorStain => 3,
                _ => int.MaxValue
            };
        }
    }
}
