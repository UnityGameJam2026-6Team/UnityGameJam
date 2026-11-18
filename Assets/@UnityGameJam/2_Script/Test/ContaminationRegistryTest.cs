using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Facility;

namespace UnityGameJam.Test
{
    public class ContaminationRegistryTest
    {
        private static readonly Vector3 _entrance = Vector3.zero;
        private static readonly Vector3 _floor1Lobby = new Vector3(4f, 0f, 0f);
        private static readonly Vector3 _floor2Lobby = new Vector3(20f, 0f, 0f);
        private static readonly Vector3 _workshop = new Vector3(24f, 0f, 0f);

        [Test]
        public void 현재_오염이_시작량보다_많으면_청결도는_영이다()
        {
            var debris = new ContaminationItem(1, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            var trash = new ContaminationItem(2, EContaminationType.Trash, 3, EZoneId.Floor2Lobby, _floor2Lobby);
            ContaminationRegistry registry = CreateScoredRegistry(debris, trash);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.True);
            Assert.That(registry.TryDrop(1, EWorkerType.Orc, new WorkerBody(_floor2Lobby, EFloor.Floor2)), Is.True);

            Assert.That(calculator.GetZoneCleanliness(EZoneId.Floor2Lobby), Is.Zero);
            Assert.That(calculator.GetZoneCleanliness(EZoneId.Workshop), Is.EqualTo(100f));
            Assert.That(calculator.GetFacilityCleanliness(), Is.Zero);
        }

        [Test]
        public void 시설_청결도는_시설_전체_가중치로_계산한다()
        {
            var debris = new ContaminationItem(1, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateScoredRegistry(debris);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.True);

            Assert.That(calculator.GetZoneCleanliness(EZoneId.Workshop), Is.EqualTo(100f));
            Assert.That(calculator.GetFacilityCleanliness(), Is.EqualTo(100f * 4f / 12f).Within(0.001f));
        }

        [Test]
        public void 시작할_때_오염이_없는_구역이_있으면_계산기를_만들_수_없다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateRegistry(trash);

            Assert.Throws<ArgumentException>(() => new CleanlinessCalculator(registry));
        }

        [Test]
        public void 작업실_잔해를_운반하는_동안_제외하고_내려놓은_로비에_포함한다()
        {
            var debris = new ContaminationItem(1, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateScoredRegistry(debris);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.True);
            Assert.That(calculator.GetZoneCleanliness(EZoneId.Workshop), Is.EqualTo(100f));
            Assert.That(calculator.GetFacilityCleanliness(), Is.GreaterThan(0f));

            Assert.That(registry.TryDrop(1, EWorkerType.Orc, new WorkerBody(_floor2Lobby, EFloor.Floor2)), Is.True);

            Assert.That(registry.FindItem(1).Zone, Is.EqualTo(EZoneId.Floor2Lobby));
            Assert.That(calculator.GetZoneCleanliness(EZoneId.Workshop), Is.EqualTo(100f));
            Assert.That(calculator.GetFacilityCleanliness(), Is.Zero);
        }

        [TestCase(20f)]
        [TestCase(24f)]
        public void 판정_구역_밖의_폐기물은_현재_층_로비에_귀속한다(float x)
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, _floor1Lobby);
            ContaminationRegistry registry = CreateScoredRegistry(trash);
            var calculator = new CleanlinessCalculator(registry);
            var outsideZone = new Vector3(x, 0f, 4f);

            Assert.That(CreateZoneMap().FindZone(outsideZone, EFloor.Floor2), Is.Null);
            Assert.That(registry.TryPickUp(1, EWorkerType.Player), Is.True);
            Assert.That(registry.TryDrop(1, EWorkerType.Player, new WorkerBody(outsideZone, EFloor.Floor2)), Is.True);

            Assert.That(registry.FindItem(1).Zone, Is.EqualTo(EZoneId.Floor2Lobby));
            Assert.That(calculator.GetZoneCleanliness(EZoneId.Floor1Lobby), Is.EqualTo(100f));
            Assert.That(calculator.GetFacilityCleanliness(), Is.Zero);
        }

        [Test]
        public void 배출을_시작할_때_현관에_적치된_폐기물만_제외한다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, _entrance);
            var debris = new ContaminationItem(2, EContaminationType.HeavyDebris, 4, EZoneId.Floor1Lobby, _floor1Lobby);
            ContaminationRegistry registry = CreateScoredRegistry(trash, debris);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.DispatchDischargeBatch(), Is.EqualTo(1));
            Assert.That(calculator.GetFacilityCleanliness(), Is.EqualTo(20f).Within(0.001f));
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Dispatched));
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.OnFloor));

            Assert.That(registry.TryPickUp(2, EWorkerType.Orc), Is.True);
            Assert.That(registry.TryDrop(2, EWorkerType.Orc, new WorkerBody(_entrance, EFloor.Floor1)), Is.True);
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.InDischargeArea));
            Assert.That(calculator.GetFacilityCleanliness(), Is.EqualTo(20f).Within(0.001f));
            Assert.That(registry.DispatchDischargeBatch(), Is.EqualTo(1));
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.Dispatched));
        }

        [Test]
        public void 슬라임은_한_번에_하나를_소화하고_시작할_때만_오염을_제외한다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, _workshop);
            var debris = new ContaminationItem(2, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateScoredRegistry(trash, debris);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.TryStartDigest(1), Is.True);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Digesting));
            float score = calculator.GetFacilityCleanliness();
            Assert.That(score, Is.EqualTo(20f).Within(0.001f));
            Assert.That(registry.TryStartDigest(2), Is.False);
            Assert.That(registry.TryCompleteDigest(1), Is.True);

            Assert.That(calculator.GetFacilityCleanliness(), Is.EqualTo(score));
            Assert.That(registry.TryStartDigest(2), Is.True);
        }

        [Test]
        public void 작은_물건은_지정된_제자리에_둘_때만_정리된다()
        {
            var clutter = new ContaminationItem(1, EContaminationType.ClutterItem, 2, EZoneId.Workshop, _workshop, 20);
            ContaminationRegistry registry = CreateRegistry(clutter);

            Assert.That(registry.TryPickUp(1, EWorkerType.Goblin), Is.True);
            Assert.That(registry.TryPlaceAtHome(1, EWorkerType.Goblin, 21), Is.False);
            Assert.That(registry.TryPlaceAtHome(1, EWorkerType.Orc, 20), Is.False);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Carried));
            Assert.That(registry.TryPlaceAtHome(1, EWorkerType.Goblin, 20), Is.True);

            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Resolved));
            Assert.That(registry.FindItem(1).Position, Is.EqualTo(_workshop + Vector3.right));
        }

        [Test]
        public void 바닥_얼룩을_닦으면_한_번만_오염에서_제외한다()
        {
            var stain = new ContaminationItem(1, EContaminationType.FloorStain, 1, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateScoredRegistry(stain);
            var calculator = new CleanlinessCalculator(registry);
            var changes = new List<ItemSnapshot>();
            registry.ItemChanged += changes.Add;

            Assert.That(registry.TryClaim(1, EWorkerType.Skeleton), Is.True);
            Assert.That(registry.TryCompleteMop(1, EWorkerType.Skeleton), Is.True);
            Assert.That(registry.TryCompleteMop(1, EWorkerType.Skeleton), Is.False);

            Assert.That(calculator.GetZoneCleanliness(EZoneId.Workshop), Is.EqualTo(100f));
            Assert.That(changes.Select(item => item.State), Is.EqualTo(new[] { EItemState.Resolved }));
        }

        [Test]
        public void 선점한_개체는_다른_졸개가_가져갈_수_없고_플레이어는_가져갈_수_있다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateRegistry(trash);

            Assert.That(registry.TryClaim(1, EWorkerType.Goblin), Is.True);
            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.False);
            Assert.That(registry.TryPickUp(1, EWorkerType.Player), Is.True);

            Assert.That(registry.FindItem(1).Carrier, Is.EqualTo(EWorkerType.Player));
            Assert.That(registry.FindItem(1).ClaimedBy, Is.Null);
            Assert.That(registry.TryClaim(1, EWorkerType.Goblin), Is.False);
        }

        [Test]
        public void 선점을_빼앗긴_작업자가_얼룩을_완료하거나_새_선점을_해제할_수_없다()
        {
            var stain = new ContaminationItem(1, EContaminationType.FloorStain, 1, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateRegistry(stain);

            Assert.That(registry.TryClaim(1, EWorkerType.Skeleton), Is.True);
            Assert.That(registry.TryClaim(1, EWorkerType.Player), Is.True);
            Assert.That(registry.TryCompleteMop(1, EWorkerType.Skeleton), Is.False);
            Assert.That(registry.TryRelease(1, EWorkerType.Skeleton), Is.False);
            Assert.That(registry.TryRelease(1, EWorkerType.Player), Is.True);
            Assert.That(registry.TryClaim(1, EWorkerType.Skeleton), Is.True);
        }

        [Test]
        public void 물건을_든_작업자가_다른_물건을_또_주울_수_없다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, _workshop);
            var debris = new ContaminationItem(2, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            ContaminationRegistry registry = CreateRegistry(trash, debris);

            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.True);
            Assert.That(registry.TryPickUp(2, EWorkerType.Orc), Is.False);
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.OnFloor));
        }

        [Test]
        public void 작은_물건을_현관에_내려놓으면_로비_바닥_오염으로_집계한다()
        {
            var clutter = new ContaminationItem(1, EContaminationType.ClutterItem, 2, EZoneId.Workshop, _workshop, 20);
            ContaminationRegistry registry = CreateScoredRegistry(clutter);
            var calculator = new CleanlinessCalculator(registry);

            Assert.That(registry.TryPickUp(1, EWorkerType.Player), Is.True);
            Assert.That(calculator.GetFacilityCleanliness(), Is.GreaterThan(0f));
            Assert.That(registry.TryDrop(1, EWorkerType.Player, new WorkerBody(_entrance, EFloor.Floor1)), Is.True);

            Assert.That(registry.FindItem(1).Zone, Is.EqualTo(EZoneId.Floor1Lobby));
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(registry.DispatchDischargeBatch(), Is.Zero);
            Assert.That(calculator.GetFacilityCleanliness(), Is.Zero);
        }

        [Test]
        public void 마감용_내려놓기는_운반물만_작업자의_현재_위치에_귀속한다()
        {
            var debris = new ContaminationItem(1, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, _workshop);
            var clutter = new ContaminationItem(2, EContaminationType.ClutterItem, 2, EZoneId.Workshop, _workshop, 20);
            var trash = new ContaminationItem(3, EContaminationType.Trash, 3, EZoneId.Workshop, _workshop);
            var stagedTrash = new ContaminationItem(4, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, _entrance);
            ContaminationRegistry registry = CreateScoredRegistry(debris, clutter, trash, stagedTrash);
            var calculator = new CleanlinessCalculator(registry);
            var workers = new Dictionary<EWorkerType, IWorkerBody>
            {
                { EWorkerType.Orc, new WorkerBody(_entrance, EFloor.Floor1) },
                { EWorkerType.Player, new WorkerBody(_floor2Lobby, EFloor.Floor2) }
            };

            Assert.That(registry.TryPickUp(1, EWorkerType.Orc), Is.True);
            Assert.That(registry.TryPickUp(2, EWorkerType.Player), Is.True);
            Assert.That(registry.TryStartDigest(3), Is.True);
            Assert.That(registry.DispatchDischargeBatch(), Is.EqualTo(1));

            Assert.That(registry.DropAllCarried(workers), Is.EqualTo(2));
            Assert.That(registry.DropAllCarried(workers), Is.Zero);

            Assert.That(registry.FindItem(1).Zone, Is.EqualTo(EZoneId.Floor1Lobby));
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.InDischargeArea));
            Assert.That(registry.FindItem(2).Zone, Is.EqualTo(EZoneId.Floor2Lobby));
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(registry.FindItem(3).State, Is.EqualTo(EItemState.Digesting));
            Assert.That(registry.FindItem(4).State, Is.EqualTo(EItemState.Dispatched));
            Assert.That(calculator.GetFacilityCleanliness(), Is.EqualTo(100f * 6f / 19f).Within(0.001f));
        }

        private static ContaminationRegistry CreateRegistry(params ContaminationItem[] items)
        {
            return new ContaminationRegistry(CreateZoneMap(), items);
        }

        private static ContaminationRegistry CreateScoredRegistry(params ContaminationItem[] items)
        {
            var allItems = new List<ContaminationItem>(items);
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                if (items.Any(item => item.Zone == zone)) { continue; }
                allItems.Add(new ContaminationItem(100 + (int)zone, EContaminationType.FloorStain, 1, zone, Vector3.zero));
            }
            return new ContaminationRegistry(CreateZoneMap(), allItems);
        }

        private static ZoneMap CreateZoneMap()
        {
            var layout = new FacilityLayout { DischargeArea = new Bounds(_entrance, new Vector3(2f, 2f, 2f)) };
            layout.Zones.Add(CreateZone(EZoneId.Floor1Lobby, EFloor.Floor1, _entrance, _floor1Lobby));
            layout.Zones.Add(CreateZone(EZoneId.Floor2Lobby, EFloor.Floor2, _floor2Lobby));
            layout.Zones.Add(CreateZone(EZoneId.Workshop, EFloor.Floor2, _workshop));
            layout.HomeSlots.Add(new HomeSlot { SlotId = 20, Zone = EZoneId.Workshop, Position = _workshop + Vector3.right });
            layout.HomeSlots.Add(new HomeSlot { SlotId = 21, Zone = EZoneId.Workshop, Position = _workshop + Vector3.left });
            return new ZoneMap(layout);
        }

        private static ZoneArea CreateZone(EZoneId zone, EFloor floor, params Vector3[] positions)
        {
            var area = new ZoneArea { Zone = zone, Floor = floor };
            foreach (Vector3 position in positions)
            {
                area.Areas.Add(new Bounds(position, new Vector3(2f, 2f, 2f)));
            }
            return area;
        }

        private sealed class WorkerBody : IWorkerBody
        {
            public Vector3 Position { get; }
            public EFloor Floor { get; }

            public WorkerBody(Vector3 position, EFloor floor)
            {
                Position = position;
                Floor = floor;
            }
        }
    }
}
