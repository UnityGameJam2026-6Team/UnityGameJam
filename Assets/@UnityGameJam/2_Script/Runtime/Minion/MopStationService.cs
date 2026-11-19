using System;
using System.Collections.Generic;
using System.Linq;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Minion
{
    internal sealed class MopStationService
    {
        private readonly Dictionary<EFloor, ToiletStation> _stations;
        private readonly Dictionary<EFloor, int> _available;
        private readonly Dictionary<EWorkerType, EFloor> _borrowed = new Dictionary<EWorkerType, EFloor>();
        private readonly GameRuleSO _rules;

        public int AvailableCount => _available.Values.Sum();

        public MopStationService(FacilityLayout layout, GameRuleSO rules)
        {
            if (layout == null) { throw new ArgumentNullException(nameof(layout)); }
            if (rules == null) { throw new ArgumentNullException(nameof(rules)); }
            if (rules.MopsPerToilet <= 0 || !float.IsFinite(rules.InteractionDistance) || rules.InteractionDistance <= 0f)
            {
                throw new ArgumentException("화장실의 걸레 수와 상호작용 반경은 양수여야 합니다.", nameof(rules));
            }

            _stations = layout.Toilets.ToDictionary(station => station.Floor);
            EFloor[] floors = (EFloor[])Enum.GetValues(typeof(EFloor));
            if (_stations.Count != floors.Length || floors.Any(floor => !_stations.ContainsKey(floor)))
            {
                throw new ArgumentException("각 층에 화장실이 하나씩 있어야 합니다.", nameof(layout));
            }

            _available = floors.ToDictionary(floor => floor, _ => rules.MopsPerToilet);
            _rules = rules;
        }

        public int GetAvailableCount(EFloor floor)
        {
            return _available.TryGetValue(floor, out int count) ? count : 0;
        }

        public ToiletStation GetBorrowedFrom(EWorkerType worker)
        {
            return _borrowed.TryGetValue(worker, out EFloor floor) ? _stations[floor] : null;
        }

        public ToiletStation FindAvailableStation(EFloor currentFloor)
        {
            if (!_stations.ContainsKey(currentFloor)) { return null; }

            ToiletStation closest = null;
            int closestDistance = int.MaxValue;
            foreach (ToiletStation station in _stations.Values)
            {
                if (_available[station.Floor] == 0) { continue; }

                int distance = Math.Abs((int)station.Floor - (int)currentFloor);
                if (closest == null || distance < closestDistance
                    || (distance == closestDistance && station.Floor < closest.Floor))
                {
                    closest = station;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        public bool CanBorrow(EWorkerType worker, IWorkerBody body) =>
            body != null && !_borrowed.ContainsKey(worker)
            && _stations.TryGetValue(body.Floor, out ToiletStation station)
            && _available[station.Floor] > 0 && IsInReach(station, body);

        public bool TryBorrow(EWorkerType worker, IWorkerBody body)
        {
            if (!CanBorrow(worker, body)) { return false; }

            _borrowed.Add(worker, body.Floor);
            _available[body.Floor]--;
            return true;
        }

        public bool TryReturn(EWorkerType worker, IWorkerBody body)
        {
            ToiletStation station = GetBorrowedFrom(worker);
            if (body == null || station == null || !IsInReach(station, body)) { return false; }

            return Release(worker);
        }

        public bool Release(EWorkerType worker)
        {
            if (!_borrowed.Remove(worker, out EFloor floor)) { return false; }
            _available[floor]++;
            return true;
        }

        private bool IsInReach(ToiletStation station, IWorkerBody body)
        {
            return station.Floor == body.Floor
                && (body.Position - station.MopPickupPosition).sqrMagnitude <= _rules.InteractionDistance * _rules.InteractionDistance;
        }
    }
}
