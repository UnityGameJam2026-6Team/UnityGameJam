using System;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>성공한 운반 상태 전이를 한 번씩 집기·내려놓기 동작으로 전달한다.</summary>
    internal sealed class CarryAnimationState
    {
        private readonly EWorkerType _worker;
        private readonly Action<bool, bool> _beginHandling;
        private int? _itemId;

        public EContaminationType? CarriedType { get; private set; }
        public bool IsHoldingMop { get; private set; }

        public CarryAnimationState(EWorkerType worker, Action<bool, bool> beginHandling)
        {
            _worker = worker;
            _beginHandling = beginHandling;
        }

        public void Observe(ItemSnapshot item)
        {
            bool isCarried = item.State == EItemState.Carried && item.Carrier == _worker;
            if (isCarried && item.ItemId != _itemId)
            {
                _itemId = item.ItemId;
                CarriedType = item.Type;
                _beginHandling(true, item.Type == EContaminationType.HeavyDebris);
            }
            else if (!isCarried && item.ItemId == _itemId)
            {
                _itemId = null;
                CarriedType = null;
                _beginHandling(false, item.Type == EContaminationType.HeavyDebris);
            }
        }

        public void ObserveMop(bool isHolding)
        {
            if (isHolding == IsHoldingMop) { return; }
            IsHoldingMop = isHolding;
            _beginHandling(isHolding, false);
        }

        public float GetMoveSpeed(CharacterAnimatorDriver animator, float baseSpeed, float ruleSpeed, bool running)
        {
            return animator != null
                ? animator.GetAdjustedMoveSpeed(baseSpeed, ruleSpeed, running, CarriedType, IsHoldingMop)
                : ruleSpeed;
        }
    }
}
