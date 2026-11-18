using System;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Cleaning
{
    internal sealed class ContaminationItem
    {
        public int Id { get; }
        public EContaminationType Type { get; }
        public int Weight { get; }
        public int? HomeSlotId { get; }
        public EZoneId Zone { get; private set; }
        public Vector3 Position { get; private set; }
        public EItemState State { get; private set; } = EItemState.OnFloor;
        public EWorkerType? Carrier { get; private set; }
        public EWorkerType? ClaimedBy { get; private set; }

        public bool IsCounted => State is EItemState.OnFloor or EItemState.InDischargeArea;
        public bool IsWaste => Type is EContaminationType.HeavyDebris or EContaminationType.Trash;
        public ItemSnapshot Snapshot => new ItemSnapshot(Id, Type, State, Position, Carrier);

        public ContaminationItem(int id, EContaminationType type, int weight, EZoneId zone, Vector3 position,
            int? homeSlotId = null)
        {
            if (weight <= 0) { throw new ArgumentOutOfRangeException(nameof(weight)); }

            Id = id;
            Type = type;
            Weight = weight;
            HomeSlotId = homeSlotId;
            Zone = zone;
            Position = position;
        }

        internal void SetClaim(EWorkerType? worker)
        {
            ClaimedBy = worker;
        }

        internal void SetState(EItemState state, EWorkerType? carrier = null)
        {
            State = state;
            Carrier = carrier;
            ClaimedBy = null;
        }

        internal void SetLocation(EZoneId zone, Vector3 position)
        {
            Zone = zone;
            Position = position;
        }
    }
}
