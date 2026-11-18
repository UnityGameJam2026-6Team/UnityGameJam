using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;
using UnityGameJam.Minion;

namespace UnityGameJam.Test
{
    public class DischargeServiceTest
    {
        private const string StatsPath = "Assets/@UnityGameJam/3_Data/ScriptableObject/WorkerStats_Hellhound.asset";

        [Test]
        public void 헬하운드는_출발할_때_적치된_폐기물만_배출한다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, Vector3.zero);
            var debris = new ContaminationItem(2, EContaminationType.HeavyDebris, 4, EZoneId.Floor1Lobby, Vector3.right * 5f);
            ContaminationRegistry registry = CreateRegistry(trash, debris);
            WorkerStatsSO stats = AssetDatabase.LoadAssetAtPath<WorkerStatsSO>(StatsPath);
            var service = new DischargeService(registry, stats);
            bool wasDispatchingWhenItemChanged = false;
            registry.ItemChanged += item =>
            {
                if (item.ItemId == 1) { wasDispatchingWhenItemChanged = service.IsDispatching; }
            };

            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Started));
            Assert.That(wasDispatchingWhenItemChanged, Is.True);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Dispatched));
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.OnFloor));

            Assert.That(registry.TryPickUp(2, EWorkerType.Orc), Is.True);
            Assert.That(registry.TryDrop(2, EWorkerType.Orc, new EntranceBody()), Is.True);
            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Busy));
            service.Tick(stats.DispatchSeconds / 2f);
            Assert.That(service.IsDispatching, Is.True);
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.InDischargeArea));

            service.Tick(stats.DispatchSeconds / 2f);
            Assert.That(service.IsDispatching, Is.False);
            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Started));
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.Dispatched));
        }

        [Test]
        public void 배출_중이거나_폐기물이_없으면_명령을_거절한다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, Vector3.zero);
            ContaminationRegistry registry = CreateRegistry(trash);
            WorkerStatsSO stats = AssetDatabase.LoadAssetAtPath<WorkerStatsSO>(StatsPath);
            var service = new DischargeService(registry, stats);

            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Started));
            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Busy));
            service.Tick(stats.DispatchSeconds);
            Assert.That(service.TryDispatch(), Is.EqualTo(EDischargeResult.Empty));
            Assert.That(service.IsDispatching, Is.False);
        }

        private static ContaminationRegistry CreateRegistry(params ContaminationItem[] items)
        {
            var layout = new FacilityLayout { DischargeArea = new Bounds(Vector3.zero, Vector3.one) };
            return new ContaminationRegistry(new ZoneMap(layout), items);
        }

        private sealed class EntranceBody : IWorkerBody
        {
            public Vector3 Position => Vector3.zero;
            public EFloor Floor => EFloor.Floor1;
        }
    }
}
