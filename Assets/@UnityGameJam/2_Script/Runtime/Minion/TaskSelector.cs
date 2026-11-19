using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;

namespace UnityGameJam.Minion
{
    internal sealed class TaskSelector
    {
        private readonly ContaminationRegistry _registry;
        private readonly WorkerCapability _capability;
        private volatile IReadOnlyDictionary<EWorkerType, EContaminationType> _preferences;

        public TaskSelector(ContaminationRegistry registry, WorkerCapability capability)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public void SetPreferences(IReadOnlyDictionary<EWorkerType, EContaminationType> preferences) => _preferences = preferences;

        public EContaminationType? PreferredTask(EWorkerType worker)
        {
            var preferences = _preferences;
            if (preferences != null && preferences.TryGetValue(worker, out EContaminationType type) && _capability.CanHandle(worker, type))
            {
                return type;
            }
            EContaminationType? fallback = worker switch
            {
                EWorkerType.Goblin => EContaminationType.ClutterItem,
                EWorkerType.Orc or EWorkerType.Slime => EContaminationType.HeavyDebris,
                EWorkerType.Skeleton => EContaminationType.FloorStain,
                _ => null
            };
            return fallback.HasValue && _capability.CanHandle(worker, fallback.Value) ? fallback : null;
        }

        public ContaminationItem FindTask(EWorkerType worker, EZoneId zone, Vector3 position, bool isHoldingMop,
            float observationRadius = float.PositiveInfinity, MinionMemory memory = null,
            IReadOnlyDictionary<int, Vector3> unreachable = null, bool canBorrowMop = true)
        {
            EContaminationType? preferred = PreferredTask(worker);
            ContaminationItem best = null;
            (int Priority, float DistanceSquared, int ItemId) bestRank = default;

            foreach (ContaminationItem item in _registry.Items)
            {
                if (!IsCandidate(item, worker, zone, isHoldingMop)
                    || (!isHoldingMop && !canBorrowMop && item.Type == EContaminationType.FloorStain)
                    || (unreachable != null && unreachable.TryGetValue(item.Id, out Vector3 failedAt) && failedAt == item.Position)
                    || (!MinionMemory.IsVisible(item.Position, position, observationRadius) && memory?.HasObservedTask(item) != true))
                {
                    continue;
                }

                var rank = (item.Type == preferred ? -1 : GetPriority(item.Type), (item.Position - position).sqrMagnitude, item.Id);
                if (best == null || rank.CompareTo(bestRank) < 0)
                {
                    best = item;
                    bestRank = rank;
                }
            }
            return best;
        }

        public bool IsCandidate(ContaminationItem item, EWorkerType worker, EZoneId zone, bool isHoldingMop)
        {
            if (item.Zone != zone || (item.ClaimedBy.HasValue && item.ClaimedBy != worker)) { return false; }
            if (isHoldingMop && item.Type != EContaminationType.FloorStain) { return false; }

            return item.State == EItemState.OnFloor && _capability.CanHandle(worker, item.Type);
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
