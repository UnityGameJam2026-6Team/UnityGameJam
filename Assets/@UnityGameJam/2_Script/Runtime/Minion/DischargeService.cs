using System;
using System.Linq;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Minion
{
    internal enum EDischargeResult
    {
        Started,
        Busy,
        Empty,
    }

    internal sealed class DischargeService
    {
        private readonly ContaminationRegistry _registry;
        private readonly WorkerStatsSO _stats;
        private float _elapsedTime;
        private bool _hasDispatch;

        public float RemainingTime => _hasDispatch ? Math.Max(0f, _stats.DispatchSeconds - _elapsedTime) : 0f;
        public bool IsDispatching => RemainingTime > 0f;
        public bool HasReachedReturnTime => !_hasDispatch || _elapsedTime >= _stats.DispatchSeconds * 0.5f;

        public DischargeService(ContaminationRegistry registry, WorkerStatsSO stats)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (stats == null) { throw new ArgumentNullException(nameof(stats)); }
            if (stats.Worker != EWorkerType.Hellhound || !float.IsFinite(stats.DispatchSeconds) || stats.DispatchSeconds <= 0f)
            {
                throw new ArgumentException("헬하운드의 배출 왕복 시간은 양수여야 합니다.", nameof(stats));
            }
            _stats = stats;
        }

        public EDischargeResult TryDispatch()
        {
            if (IsDispatching) { return EDischargeResult.Busy; }
            if (!_registry.Items.Any(item => item.State == EItemState.InDischargeArea)) { return EDischargeResult.Empty; }

            // 개체 변경 이벤트가 배출 중 상태를 읽을 수 있도록 출발을 먼저 기록한다.
            _elapsedTime = 0f;
            _hasDispatch = true;
            _registry.DispatchDischargeBatch();
            return EDischargeResult.Started;
        }

        public void Tick(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }

            if (!_hasDispatch) { return; }
            _elapsedTime += deltaTime;
            if (RemainingTime <= 0f) { _hasDispatch = false; }
        }
    }
}
