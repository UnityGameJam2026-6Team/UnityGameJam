using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Minion;

namespace UnityGameJam.Test
{
    public class MopStationServiceTest
    {
        private const string RulePath = "Assets/@UnityGameJam/3_Data/ScriptableObject/GameRule.asset";

        [Test]
        public void 현재_층_걸레가_없으면_가장_가까운_다른_층을_고른다()
        {
            MopStationService service = CreateService();
            Assert.That(service.FindAvailableStation(EFloor.Floor2).Floor, Is.EqualTo(EFloor.Floor2));

            Assert.That(service.TryBorrow(EWorkerType.Player, new WorkerBody(EFloor.Floor2)), Is.True);
            Assert.That(service.TryBorrow(EWorkerType.Goblin, new WorkerBody(EFloor.Floor2)), Is.True);
            Assert.That(service.FindAvailableStation(EFloor.Floor2).Floor, Is.EqualTo(EFloor.Floor1));

            Assert.That(service.TryBorrow(EWorkerType.Orc, new WorkerBody(EFloor.Floor1)), Is.True);
            Assert.That(service.TryBorrow(EWorkerType.Skeleton, new WorkerBody(EFloor.Floor1)), Is.True);
            Assert.That(service.FindAvailableStation(EFloor.Floor2).Floor, Is.EqualTo(EFloor.Floor3));
        }

        [Test]
        public void 모든_걸레가_대여되면_추가로_빌릴_수_없다()
        {
            MopStationService service = CreateService();
            Assert.That(service.AvailableCount, Is.EqualTo(6));
            int index = 0;
            foreach (EWorkerType worker in Enum.GetValues(typeof(EWorkerType)))
            {
                Assert.That(service.TryBorrow(worker, new WorkerBody((EFloor)(index++ / 2))), Is.True);
            }

            Assert.That(service.AvailableCount, Is.Zero);
            Assert.That(service.FindAvailableStation(EFloor.Floor2), Is.Null);
            Assert.That(service.TryBorrow(EWorkerType.Player, new WorkerBody(EFloor.Floor2)), Is.False);
        }

        [Test]
        public void 걸레는_한_개만_빌리고_빌린_곳에_반납한다()
        {
            MopStationService service = CreateService();
            var origin = new WorkerBody(EFloor.Floor2);
            var otherStation = new WorkerBody(EFloor.Floor1);
            var outside = new WorkerBody(EFloor.Floor2, Vector3.right * 20f);

            Assert.That(service.TryBorrow(EWorkerType.Player, outside), Is.False);
            Assert.That(service.TryBorrow(EWorkerType.Player, origin), Is.True);
            Assert.That(service.GetBorrowedFrom(EWorkerType.Player).Floor, Is.EqualTo(EFloor.Floor2));
            Assert.That(service.TryBorrow(EWorkerType.Player, otherStation), Is.False);
            Assert.That(service.TryReturn(EWorkerType.Player, otherStation), Is.False);
            Assert.That(service.TryReturn(EWorkerType.Player, outside), Is.False);
            Assert.That(service.GetAvailableCount(EFloor.Floor2), Is.EqualTo(1));

            Assert.That(service.TryReturn(EWorkerType.Player, origin), Is.True);
            Assert.That(service.TryReturn(EWorkerType.Player, origin), Is.False);
            Assert.That(service.GetBorrowedFrom(EWorkerType.Player), Is.Null);
            Assert.That(service.GetAvailableCount(EFloor.Floor2), Is.EqualTo(2));
        }

        [Test]
        public void 마지막_걸레가_도착_전에_대여되면_나중에_도착한_작업자는_다른_층을_찾는다()
        {
            MopStationService service = CreateService();
            var station = new WorkerBody(EFloor.Floor2);
            Assert.That(service.TryBorrow(EWorkerType.Player, station), Is.True);
            Assert.That(service.FindAvailableStation(EFloor.Floor2).Floor, Is.EqualTo(EFloor.Floor2));

            Assert.That(service.TryBorrow(EWorkerType.Orc, station), Is.True);
            Assert.That(service.TryBorrow(EWorkerType.Goblin, station), Is.False);
            Assert.That(service.FindAvailableStation(EFloor.Floor2).Floor, Is.EqualTo(EFloor.Floor1));
            Assert.That(service.GetBorrowedFrom(EWorkerType.Goblin), Is.Null);
        }

        private static MopStationService CreateService()
        {
            var layout = new FacilityLayout();
            foreach (EFloor floor in Enum.GetValues(typeof(EFloor)))
            {
                layout.Toilets.Add(new ToiletStation { Floor = floor, MopPickupPosition = Vector3.zero });
            }
            GameRuleSO rules = AssetDatabase.LoadAssetAtPath<GameRuleSO>(RulePath);
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.MopsPerToilet, Is.EqualTo(2));
            return new MopStationService(layout, rules);
        }

        private sealed class WorkerBody : IWorkerBody
        {
            public Vector3 Position { get; }
            public EFloor Floor { get; }

            public WorkerBody(EFloor floor, Vector3 position = default)
            {
                Floor = floor;
                Position = position;
            }
        }
    }
}
