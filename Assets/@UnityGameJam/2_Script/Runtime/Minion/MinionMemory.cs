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
            public readonly Dictionary<int, (ItemSnapshot Item, bool Available)> Items = new();
            public readonly HashSet<Vector3> SeenSpawnPoints = new HashSet<Vector3>();
            public readonly HashSet<Vector3> TriedSpawnPoints = new HashSet<Vector3>();
            public bool IsScouted;
            public RoomReport Report;
        }

        private readonly struct RoomReport
        {
            public readonly int Revision;
            public readonly (int Heavy, int Trash, int Clutter, int Stain) FloorWork;
            public readonly bool IsScouted, IsClean;

            public RoomReport(int revision, bool isScouted, bool isClean, (int Heavy, int Trash, int Clutter, int Stain) floorWork)
            {
                Revision = revision;
                IsScouted = isScouted;
                IsClean = isClean;
                FloorWork = floorWork;
            }

            public int Count(EContaminationType type) => type switch
            {
                EContaminationType.HeavyDebris => FloorWork.Heavy,
                EContaminationType.Trash => FloorWork.Trash,
                EContaminationType.ClutterItem => FloorWork.Clutter,
                EContaminationType.FloorStain => FloorWork.Stain,
                _ => 0
            };
        }

        private static readonly EZoneId[] KnownZones = (EZoneId[])System.Enum.GetValues(typeof(EZoneId));
        private readonly Dictionary<EZoneId, RoomObservation> _observations = new Dictionary<EZoneId, RoomObservation>();
        private readonly Dictionary<EZoneId, RoomReport> _shared = new();
        private readonly HashSet<EZoneId> _roomsToResurvey = new();

        public void InvalidateRoom(EZoneId zone)
        {
            _observations.Remove(zone);
            _shared.Remove(zone);
            _roomsToResurvey.Add(zone);
        }

        public bool NeedsResurvey(EZoneId zone) => _roomsToResurvey.Contains(zone);

        public void BeginSurvey(EZoneId zone)
        {
            if (!_observations.TryGetValue(zone, out RoomObservation room)) { return; }
            room.TriedSpawnPoints.Clear();
        }

        public void Observe(EZoneId zone, Vector3 position, float radius, ContaminationRegistry registry,
            FacilityLayout layout, int revision)
        {
            if (!_observations.TryGetValue(zone, out RoomObservation room))
            {
                room = new RoomObservation();
                _observations.Add(zone, room);
            }

            if (_shared.TryGetValue(zone, out RoomReport shared) && shared.IsClean && shared.Revision > room.Report.Revision)
            { room.Items.Clear(); }

            foreach (int id in room.Items.Where(entry => IsVisible(entry.Value.Item.Position, position, radius))
                         .Select(entry => entry.Key).ToArray())
            {
                room.Items.Remove(id);
            }
            foreach (ContaminationItem item in registry.Items)
            {
                if (item.Zone == zone && item.IsCounted && IsVisible(item.Position, position, radius))
                {
                    room.Items[item.Id] = (item.Snapshot, !item.ClaimedBy.HasValue);
                }
            }
            bool hasUnseenPoint = false;
            foreach (SpawnPoint spawn in layout.SpawnPoints)
            {
                if (spawn.Zone != zone || room.SeenSpawnPoints.Contains(spawn.Position)) { continue; }
                if (IsVisible(spawn.Position, position, radius)) { room.SeenSpawnPoints.Add(spawn.Position); }
                else { hasUnseenPoint = true; }
            }
            if (!hasUnseenPoint && !NeedsResurvey(zone)) { room.IsScouted = true; }
            UpdateReport(room, revision);
        }

        public bool HasUnseenSpawnNear(EZoneId zone, Vector3 position, float radius, FacilityLayout layout)
        {
            _observations.TryGetValue(zone, out RoomObservation room);
            foreach (SpawnPoint spawn in layout.SpawnPoints)
            {
                if (spawn.Zone == zone && room?.SeenSpawnPoints.Contains(spawn.Position) != true
                    && IsVisible(spawn.Position, position, radius))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryFindUnseenPoint(EZoneId zone, Vector3 position, FacilityLayout layout, out Vector3 point)
        {
            point = default;
            if (!_observations.TryGetValue(zone, out RoomObservation room)) { return false; }
            float nearest = float.PositiveInfinity;
            foreach (SpawnPoint spawn in layout.SpawnPoints)
            {
                if (spawn.Zone != zone || room.SeenSpawnPoints.Contains(spawn.Position)
                    || room.TriedSpawnPoints.Contains(spawn.Position)) { continue; }
                float distance = (spawn.Position - position).sqrMagnitude;
                if (distance >= nearest) { continue; }
                nearest = distance;
                point = spawn.Position;
            }
            if (float.IsPositiveInfinity(nearest)) { return false; }
            room.TriedSpawnPoints.Add(point);
            return true;
        }

        public void FinishPatrol(EZoneId zone, FacilityLayout layout, int revision)
        {
            _roomsToResurvey.Remove(zone);
            if (_observations.TryGetValue(zone, out RoomObservation room))
            {
                room.IsScouted = layout.SpawnPoints.Where(point => point.Zone == zone)
                    .All(point => room.SeenSpawnPoints.Contains(point.Position));
                UpdateReport(room, revision);
            }
        }

        public void NoteClaimed(EZoneId zone, int itemId, int revision)
        {
            if (!_observations.TryGetValue(zone, out RoomObservation room) || !room.Items.TryGetValue(itemId, out var item)) { return; }
            room.Items[itemId] = (item.Item, false);
            UpdateReport(room, revision);
        }

        public void NoteRemoved(EZoneId zone, int itemId, int revision)
        {
            if (_observations.TryGetValue(zone, out RoomObservation room) && room.Items.Remove(itemId))
            {
                UpdateReport(room, revision);
            }
        }

        private static void UpdateReport(RoomObservation room, int revision)
        {
            if (!room.IsScouted && room.Items.Count == 0 && room.Report.Revision == 0) { return; }
            (int Heavy, int Trash, int Clutter, int Stain) floorWork = default;
            foreach (var (item, isAvailable) in room.Items.Values)
            {
                if (!isAvailable || item.State != EItemState.OnFloor) { continue; }
                switch (item.Type)
                {
                    case EContaminationType.HeavyDebris: floorWork.Heavy++; break;
                    case EContaminationType.Trash: floorWork.Trash++; break;
                    case EContaminationType.ClutterItem: floorWork.Clutter++; break;
                    case EContaminationType.FloorStain: floorWork.Stain++; break;
                }
            }
            room.Report = new RoomReport(revision, room.IsScouted, room.IsScouted && room.Items.Count == 0, floorWork);
        }

        public bool ShareWith(MinionMemory other)
        {
            bool changed = false;
            foreach (EZoneId zone in KnownZones)
            {
                RoomReport report = LatestReport(zone);
                RoomReport previous = other.LatestReport(zone);
                if (report.Revision <= previous.Revision) { continue; }
                other._shared[zone] = report;
                changed |= previous.Revision == 0 || report.IsScouted != previous.IsScouted || report.IsClean != previous.IsClean
                    || report.FloorWork != previous.FloorWork;
            }
            return changed;
        }

        private RoomReport DirectReport(EZoneId zone) => _observations.TryGetValue(zone, out RoomObservation room) ? room.Report : default;

        private RoomReport LatestReport(EZoneId zone)
        {
            RoomReport direct = DirectReport(zone);
            return _shared.TryGetValue(zone, out RoomReport shared) && shared.Revision > direct.Revision ? shared : direct;
        }

        public IEnumerable<(EZoneId Zone, int ItemId, EContaminationType Type)> ObservedFloorTasks()
        {
            foreach (var (zone, room) in _observations)
            {
                if (IsKnownClean(zone)) { continue; }
                foreach (var (item, isAvailable) in room.Items.Values)
                {
                    if (isAvailable && item.State == EItemState.OnFloor) { yield return (zone, item.ItemId, item.Type); }
                }
            }
        }

        public bool HasObservedTask(ContaminationItem item) =>
            _observations.TryGetValue(item.Zone, out RoomObservation room)
            && room.Items.TryGetValue(item.Id, out var observed)
            && observed.Item.Position == item.Position && observed.Item.State == EItemState.OnFloor;

        public bool HasSurveyedDirectly(EZoneId zone) => DirectReport(zone).IsScouted;
        public bool IsKnownClean(EZoneId zone) => LatestReport(zone).IsClean;
        public bool HasCompletedSurvey(EZoneId zone) => LatestReport(zone).IsScouted;

        public bool HasKnownWork(EZoneId zone, EWorkerType worker, WorkerCapability capability, bool canBorrowMop) =>
            CountWork(LatestReport(zone), worker, capability, canBorrowMop) > 0;

        public int KnownTaskCount(EZoneId zone, EContaminationType type) => LatestReport(zone).Count(type);

        public int KnownTaskCount(EZoneId zone, EWorkerType worker, WorkerCapability capability, bool canBorrowMop) =>
            CountWork(LatestReport(zone), worker, capability, canBorrowMop);

        public bool HasDirectKnownWork(EZoneId zone, EWorkerType worker, WorkerCapability capability, bool canBorrowMop) =>
            CountWork(DirectReport(zone), worker, capability, canBorrowMop) > 0;

        private static int CountWork(RoomReport report, EWorkerType worker, WorkerCapability capability, bool canBorrowMop)
        {
            int count = 0;
            for (int index = 0; index <= (int)EContaminationType.FloorStain; index++)
            {
                var type = (EContaminationType)index;
                if ((canBorrowMop || type != EContaminationType.FloorStain) && capability.CanHandle(worker, type))
                {
                    count += report.Count(type);
                }
            }
            return count;
        }

        internal static bool IsVisible(Vector3 target, Vector3 origin, float radius)
        {
            float x = target.x - origin.x;
            float z = target.z - origin.z;
            return x * x + z * z <= radius * radius;
        }
    }
}
