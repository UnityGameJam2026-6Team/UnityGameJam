using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Player
{
    public enum EInteractionKind
    {
        None,
        PlaceAtHome,
        ReturnMop,
        BorrowMop,
        DispatchHellhound,
        TalkToMinion,
        StartMop,
        PickUp,
    }

    public readonly struct InteractionChoice
    {
        public static readonly InteractionChoice None = new InteractionChoice(EInteractionKind.None, -1, default, default, Vector3.zero);

        public EInteractionKind Kind { get; }
        public int ItemId { get; }
        public EWorkerType Minion { get; }
        public EFloor Floor { get; }
        public Vector3 TargetPosition { get; }

        public InteractionChoice(EInteractionKind kind, int itemId, EWorkerType minion, EFloor floor, Vector3 targetPosition)
        {
            Kind = kind;
            ItemId = itemId;
            Minion = minion;
            Floor = floor;
            TargetPosition = targetPosition;
        }
    }

    public readonly struct PlayerInteractionContext
    {
        public Vector3 PlayerPosition { get; }
        public float Radius { get; }
        public EContaminationType? CarriedType { get; }
        public bool IsHoldingMop { get; }
        public Vector3? HellhoundPosition { get; }

        public PlayerInteractionContext(Vector3 playerPosition, float radius, EContaminationType? carriedType, bool isHoldingMop, Vector3? hellhoundPosition)
        {
            PlayerPosition = playerPosition;
            Radius = radius;
            CarriedType = carriedType;
            IsHoldingMop = isHoldingMop;
            HellhoundPosition = hellhoundPosition;
        }
    }

    /// <summary>
    /// E키가 지금 할 행동 하나를 고른다(Spec Player 규칙 4, U206 임시 우선순위).
    /// 거리는 XZ 평면 기준이다. 실제로 할 수 있는지는 규칙(A 레인)이 명령을 받아 다시 판단한다.
    /// </summary>
    public sealed class PlayerInteractionSelector
    {
        private readonly FacilityLayout _layout;

        public PlayerInteractionSelector(FacilityLayout layout)
        {
            _layout = layout;
        }

        public InteractionChoice Select(in PlayerInteractionContext context, IReadOnlyList<ItemSnapshot> items, IReadOnlyList<(EWorkerType Minion, Vector3 Position)> minions)
        {
            Vector3 player = context.PlayerPosition;
            float radius = context.Radius;
            bool isCarrying = context.CarriedType.HasValue;

            // 1. 작은 물건을 들고 제자리 옆
            if (context.CarriedType == EContaminationType.ClutterItem && TryNearest(_layout.HomeSlots, s => s.Position, player, radius, out HomeSlot slot))
            {
                return new InteractionChoice(EInteractionKind.PlaceAtHome, -1, default, default, slot.Position);
            }

            // 2. 화장실: 걸레 반납 또는 대여 (물건을 들고 있으면 대여하지 않는다)
            if (TryNearest(_layout.Toilets, t => t.MopPickupPosition, player, radius, out ToiletStation toilet))
            {
                if (context.IsHoldingMop) { return new InteractionChoice(EInteractionKind.ReturnMop, -1, default, toilet.Floor, toilet.MopPickupPosition); }
                if (!isCarrying) { return new InteractionChoice(EInteractionKind.BorrowMop, -1, default, toilet.Floor, toilet.MopPickupPosition); }
            }

            // 3. 헬하운드
            if (context.HellhoundPosition.HasValue && DistanceXZ(player, context.HellhoundPosition.Value) <= radius)
            {
                return new InteractionChoice(EInteractionKind.DispatchHellhound, -1, EWorkerType.Hellhound, default, context.HellhoundPosition.Value);
            }

            // 4. 졸개 대화
            if (TryNearest(minions, m => m.Position, player, radius, out (EWorkerType Minion, Vector3 Position) minion))
            {
                return new InteractionChoice(EInteractionKind.TalkToMinion, -1, minion.Minion, default, minion.Position);
            }

            // 5. 걸레를 들고 얼룩 옆
            if (context.IsHoldingMop && TryNearestItem(items, player, radius, i => i.Type == EContaminationType.FloorStain, out ItemSnapshot stain))
            {
                return new InteractionChoice(EInteractionKind.StartMop, stain.ItemId, default, default, stain.Position);
            }

            // 6. 빈손으로 주울 수 있는 개체 옆
            if (!isCarrying && !context.IsHoldingMop && TryNearestItem(items, player, radius, i => i.Type != EContaminationType.FloorStain, out ItemSnapshot item))
            {
                return new InteractionChoice(EInteractionKind.PickUp, item.ItemId, default, default, item.Position);
            }

            return InteractionChoice.None;
        }

        private static bool TryNearestItem(IReadOnlyList<ItemSnapshot> items, Vector3 player, float radius, System.Func<ItemSnapshot, bool> filter, out ItemSnapshot nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            foreach (ItemSnapshot item in items)
            {
                bool isOnGround = item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea;
                if (!isOnGround || !filter(item)) { continue; }
                float distance = DistanceXZ(player, item.Position);
                if (distance <= radius && distance < best)
                {
                    best = distance;
                    nearest = item;
                }
            }
            return best < float.MaxValue;
        }

        private static bool TryNearest<T>(IEnumerable<T> candidates, System.Func<T, Vector3> position, Vector3 player, float radius, out T nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            foreach (T candidate in candidates)
            {
                float distance = DistanceXZ(player, position(candidate));
                if (distance <= radius && distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }
            return best < float.MaxValue;
        }

        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
