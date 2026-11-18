using System;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Minion
{
    internal sealed class SlimeDigestion
    {
        private readonly ContaminationRegistry _registry;
        private readonly float _digestSeconds;
        private int? _itemId;

        public float RemainingTime { get; private set; }
        public bool IsDigesting => _itemId.HasValue;

        public SlimeDigestion(ContaminationRegistry registry, WorkerStatsSO stats)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (stats == null) { throw new ArgumentNullException(nameof(stats)); }
            if (stats.Worker != EWorkerType.Slime || !float.IsFinite(stats.DigestSeconds) || stats.DigestSeconds <= 0f)
            {
                throw new ArgumentException("슬라임의 소화 시간은 양수여야 합니다.", nameof(stats));
            }
            _digestSeconds = stats.DigestSeconds;
        }

        public bool TryEat(int itemId)
        {
            if (IsDigesting) { return false; }

            // 레지스트리 이벤트가 즉시 발생하므로 현재 소화 상태를 먼저 기록한다.
            _itemId = itemId;
            RemainingTime = _digestSeconds;
            if (_registry.TryStartDigest(itemId)) { return true; }

            _itemId = null;
            RemainingTime = 0f;
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (!_itemId.HasValue) { return; }

            RemainingTime = Math.Max(0f, RemainingTime - deltaTime);
            if (RemainingTime > 0f) { return; }

            int completedItemId = _itemId.Value;
            _itemId = null;
            _registry.TryCompleteDigest(completedItemId);
        }
    }
}
