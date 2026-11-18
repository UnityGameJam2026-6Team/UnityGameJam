using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;

namespace UnityGameJam.Minion
{
    internal sealed class MinionMemory
    {
        private sealed class RoomObservation
        {
            public readonly Dictionary<int, ItemSnapshot> Items = new Dictionary<int, ItemSnapshot>();
            public readonly HashSet<Vector3> SeenSpawnPoints = new HashSet<Vector3>();
            public bool IsScouted;
        }

        private readonly Dictionary<EZoneId, RoomObservation> _observations = new Dictionary<EZoneId, RoomObservation>();

        public void Observe(EZoneId zone, Vector3 position, float radius, ContaminationRegistry registry, FacilityLayout layout)
        {
            if (!_observations.TryGetValue(zone, out RoomObservation room))
            {
                room = new RoomObservation();
                _observations.Add(zone, room);
            }

            foreach (int id in room.Items.Where(entry => IsVisible(entry.Value.Position, position, radius))
                         .Select(entry => entry.Key).ToArray())
            {
                room.Items.Remove(id);
            }
            foreach (ContaminationItem item in registry.Items)
            {
                if (item.Zone == zone && item.IsCounted && IsVisible(item.Position, position, radius))
                {
                    room.Items[item.Id] = item.Snapshot;
                }
            }
            foreach (SpawnPoint spawn in layout.SpawnPoints)
            {
                if (spawn.Zone == zone && IsVisible(spawn.Position, position, radius)) { room.SeenSpawnPoints.Add(spawn.Position); }
            }
        }

        public void FinishPatrol(EZoneId zone, FacilityLayout layout)
        {
            if (_observations.TryGetValue(zone, out RoomObservation room))
            {
                room.IsScouted = layout.SpawnPoints.Where(point => point.Zone == zone)
                    .All(point => room.SeenSpawnPoints.Contains(point.Position));
            }
        }

        public bool HasVisited(EZoneId zone) => _observations.ContainsKey(zone);

        public bool IsKnownClean(EZoneId zone)
        {
            return _observations.TryGetValue(zone, out RoomObservation room) && room.IsScouted && room.Items.Count == 0;
        }

        public bool HasKnownWork(EZoneId zone, EWorkerType worker, WorkerCapability capability, bool canBorrowMop = true)
        {
            return _observations.TryGetValue(zone, out RoomObservation room)
                && room.Items.Values.Any(item => (canBorrowMop || item.Type != EContaminationType.FloorStain)
                    && (item.State == EItemState.InDischargeArea
                        ? capability.CanDigest(worker, item.Type) : capability.CanHandle(worker, item.Type)));
        }

        internal static bool IsVisible(Vector3 target, Vector3 origin, float radius)
        {
            float x = target.x - origin.x;
            float z = target.z - origin.z;
            return x * x + z * z <= radius * radius;
        }
    }
}
