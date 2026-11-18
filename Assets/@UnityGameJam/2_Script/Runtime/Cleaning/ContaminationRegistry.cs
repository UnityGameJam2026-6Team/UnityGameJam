using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Facility;

namespace UnityGameJam.Cleaning
{
    internal sealed class ContaminationRegistry
    {
        private readonly ZoneMap _zoneMap;
        private readonly Dictionary<int, ContaminationItem> _items = new Dictionary<int, ContaminationItem>();

        public IReadOnlyCollection<ContaminationItem> Items => _items.Values;
        public event Action<ItemSnapshot> ItemChanged;

        public ContaminationRegistry(ZoneMap zoneMap, IEnumerable<ContaminationItem> initialItems)
        {
            _zoneMap = zoneMap ?? throw new ArgumentNullException(nameof(zoneMap));
            if (initialItems == null) { throw new ArgumentNullException(nameof(initialItems)); }

            foreach (ContaminationItem initialItem in initialItems)
            {
                var item = new ContaminationItem(initialItem.Id, initialItem.Type, initialItem.Weight,
                    initialItem.Zone, initialItem.Position, initialItem.HomeSlotId);
                if (item.IsWaste && item.Zone == EZoneId.Floor1Lobby
                    && _zoneMap.IsInDischargeArea(item.Position, EFloor.Floor1))
                {
                    item.SetState(EItemState.InDischargeArea);
                }
                _items.Add(item.Id, item);
            }
        }

        public ContaminationItem FindItem(int itemId)
        {
            _items.TryGetValue(itemId, out ContaminationItem item);
            return item;
        }

        public bool TryClaim(int itemId, EWorkerType worker)
        {
            ContaminationItem item = FindItem(itemId);
            if (!CanStartWork(item, worker)) { return false; }

            item.SetClaim(worker);
            return true;
        }

        public bool TryRelease(int itemId, EWorkerType worker)
        {
            ContaminationItem item = FindItem(itemId);
            if (item?.ClaimedBy != worker) { return false; }

            item.SetClaim(null);
            return true;
        }

        public bool TryPickUp(int itemId, EWorkerType worker)
        {
            ContaminationItem item = FindItem(itemId);
            if (!CanStartWork(item, worker) || item.Type == EContaminationType.FloorStain) { return false; }
            if (Items.Any(other => IsCarriedBy(other, worker))) { return false; }

            item.SetState(EItemState.Carried, worker);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public bool TryDrop(int itemId, EWorkerType worker, IWorkerBody body)
        {
            ContaminationItem item = FindItem(itemId);
            if (!IsCarriedBy(item, worker) || body == null) { return false; }

            (Vector3 position, EZoneId zone, EItemState state) = GetDropLocation(item, body);
            item.SetLocation(zone, position);
            item.SetState(state);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public int DropAllCarried(IReadOnlyDictionary<EWorkerType, IWorkerBody> workers)
        {
            if (workers == null) { throw new ArgumentNullException(nameof(workers)); }

            var drops = new List<(ContaminationItem Item, Vector3 Position, EZoneId Zone, EItemState State)>();
            foreach (ContaminationItem item in Items)
            {
                if (item.State != EItemState.Carried) { continue; }
                if (!workers.TryGetValue(item.Carrier.Value, out IWorkerBody body) || body == null)
                {
                    throw new ArgumentException("운반 중인 작업자의 위치가 필요합니다.", nameof(workers));
                }

                (Vector3 position, EZoneId zone, EItemState state) = GetDropLocation(item, body);
                drops.Add((item, position, zone, state));
            }

            // 모든 위치를 확인한 뒤 일괄 반영해야 마감 점수가 일부 내려놓기만 반영하지 않는다.
            var changes = new List<ItemSnapshot>(drops.Count);
            foreach (var (item, position, zone, state) in drops)
            {
                item.SetLocation(zone, position);
                item.SetState(state);
                changes.Add(item.Snapshot);
            }
            PublishChanges(changes);
            return drops.Count;
        }

        public bool TryPlaceAtHome(int itemId, EWorkerType worker, int slotId)
        {
            ContaminationItem item = FindItem(itemId);
            if (!IsCarriedBy(item, worker) || item.Type != EContaminationType.ClutterItem || item.HomeSlotId != slotId)
            {
                return false;
            }
            HomeSlot home = _zoneMap.FindHomeSlot(slotId);
            if (home == null) { return false; }

            item.SetLocation(home.Zone, home.Position);
            item.SetState(EItemState.Resolved);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public bool TryCompleteMop(int itemId, EWorkerType worker)
        {
            ContaminationItem item = FindItem(itemId);
            if (item?.ClaimedBy != worker || item.Type != EContaminationType.FloorStain) { return false; }

            item.SetState(EItemState.Resolved);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public bool TryStartDigest(int itemId)
        {
            ContaminationItem item = FindItem(itemId);
            if (!CanStartWork(item, EWorkerType.Slime) || !item.IsWaste) { return false; }
            if (Items.Any(other => other.State == EItemState.Digesting)) { return false; }

            item.SetState(EItemState.Digesting, EWorkerType.Slime);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public bool TryCompleteDigest(int itemId)
        {
            ContaminationItem item = FindItem(itemId);
            if (item?.State != EItemState.Digesting) { return false; }

            item.SetState(EItemState.Resolved);
            ItemChanged?.Invoke(item.Snapshot);
            return true;
        }

        public int DispatchDischargeBatch()
        {
            var changes = new List<ItemSnapshot>();
            foreach (ContaminationItem item in Items)
            {
                if (item.State != EItemState.InDischargeArea) { continue; }

                item.SetState(EItemState.Dispatched);
                changes.Add(item.Snapshot);
            }
            // 알림 도중 새 폐기물이 들어와도 이 묶음은 이미 확정되어 있다.
            PublishChanges(changes);
            return changes.Count;
        }

        private (Vector3 Position, EZoneId Zone, EItemState State) GetDropLocation(ContaminationItem item, IWorkerBody body)
        {
            Vector3 position = body.Position;
            EFloor floor = body.Floor;
            if (item.IsWaste && _zoneMap.IsInDischargeArea(position, floor))
            {
                return (position, EZoneId.Floor1Lobby, EItemState.InDischargeArea);
            }
            return (position, _zoneMap.ResolveDropZone(position, floor), EItemState.OnFloor);
        }

        private void PublishChanges(List<ItemSnapshot> changes)
        {
            foreach (ItemSnapshot change in changes)
            {
                ItemChanged?.Invoke(change);
            }
        }

        private static bool CanStartWork(ContaminationItem item, EWorkerType worker)
        {
            return item != null && item.IsCounted
                && (!item.ClaimedBy.HasValue || item.ClaimedBy == worker || worker == EWorkerType.Player);
        }

        private static bool IsCarriedBy(ContaminationItem item, EWorkerType worker)
        {
            return item?.State == EItemState.Carried && item.Carrier == worker;
        }
    }
}
