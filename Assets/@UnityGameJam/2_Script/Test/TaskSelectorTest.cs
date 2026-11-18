using System;
using System.Linq;
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
    public class TaskSelectorTest
    {
        private const string StatsPath = "Assets/@UnityGameJam/3_Data/ScriptableObject";
        private WorkerStatsSO[] _stats;
        private WorkerCapability _capability;

        [SetUp]
        public void SetUp()
        {
            _stats = Enum.GetValues(typeof(EWorkerType)).Cast<EWorkerType>()
                .Select(worker => AssetDatabase.LoadAssetAtPath<WorkerStatsSO>($"{StatsPath}/WorkerStats_{worker}.asset"))
                .ToArray();
            _capability = new WorkerCapability(_stats);
        }

        [TestCase(EWorkerType.Player, true, true, true, true, false)]
        [TestCase(EWorkerType.Goblin, true, true, false, true, false)]
        [TestCase(EWorkerType.Orc, true, true, true, true, false)]
        [TestCase(EWorkerType.Skeleton, false, false, false, true, false)]
        [TestCase(EWorkerType.Slime, false, true, true, false, true)]
        [TestCase(EWorkerType.Hellhound, false, false, false, false, false)]
        public void 작업자별로_가능한_오염_유형을_능력_데이터에서_읽는다(EWorkerType worker, bool canHandleClutter,
            bool canHandleTrash, bool canHandleHeavyDebris, bool canMop, bool canDigest)
        {
            Assert.That(_capability.CanHandle(worker, EContaminationType.ClutterItem), Is.EqualTo(canHandleClutter));
            Assert.That(_capability.CanHandle(worker, EContaminationType.Trash), Is.EqualTo(canHandleTrash));
            Assert.That(_capability.CanHandle(worker, EContaminationType.HeavyDebris), Is.EqualTo(canHandleHeavyDebris));
            Assert.That(_capability.CanHandle(worker, EContaminationType.FloorStain), Is.EqualTo(canMop));
            Assert.That(_capability.CanDigest(worker, EContaminationType.Trash), Is.EqualTo(canDigest));
            Assert.That(_capability.CanDigest(worker, EContaminationType.HeavyDebris), Is.EqualTo(canDigest));
            Assert.That(_capability.CanDigest(worker, EContaminationType.ClutterItem), Is.False);
            Assert.That(_capability.CanDigest(worker, EContaminationType.FloorStain), Is.False);
        }

        [Test]
        public void 작업자_데이터가_누락되거나_중복되면_초기화에_실패한다()
        {
            Assert.Throws<ArgumentException>(() => new WorkerCapability(_stats.Skip(1)));
            Assert.Throws<ArgumentException>(() => new WorkerCapability(_stats.Append(_stats[0])));
        }

        [Test]
        public void 유형을_먼저_비교하고_같은_유형이면_가까운_개체를_고른다()
        {
            ContaminationRegistry registry = CreateRegistry(
                CreateItem(1, EContaminationType.ClutterItem, 8f),
                CreateItem(2, EContaminationType.ClutterItem, 6f),
                CreateItem(3, EContaminationType.HeavyDebris, 5f),
                CreateItem(4, EContaminationType.Trash, 1f),
                CreateItem(5, EContaminationType.FloorStain, 0f));
            var selector = new TaskSelector(registry, _capability);

            Assert.That(selector.FindTask(EWorkerType.Orc, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(2));
            Assert.That(registry.TryPickUp(2, EWorkerType.Player), Is.True);
            Assert.That(registry.TryPickUp(1, EWorkerType.Goblin), Is.True);

            Assert.That(selector.FindTask(EWorkerType.Orc, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(3));
            Assert.That(selector.FindTask(EWorkerType.Goblin, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(4));
            Assert.That(selector.FindTask(EWorkerType.Skeleton, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(5));
        }

        [Test]
        public void 작업_유형과_거리가_같으면_아이디가_작은_개체를_고른다()
        {
            ContaminationRegistry registry = CreateRegistry(CreateItem(8, EContaminationType.Trash, 2f),
                CreateItem(4, EContaminationType.Trash, -2f));
            var selector = new TaskSelector(registry, _capability);

            Assert.That(selector.FindTask(EWorkerType.Orc, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(4));
        }

        [Test]
        public void 걸레를_든_졸개는_얼룩만_고른다()
        {
            ContaminationRegistry registry = CreateRegistry(
                CreateItem(1, EContaminationType.Trash, 0f),
                CreateItem(2, EContaminationType.FloorStain, 9f));
            var selector = new TaskSelector(registry, _capability);

            Assert.That(selector.FindTask(EWorkerType.Goblin, EZoneId.Workshop, Vector3.zero, true).Id, Is.EqualTo(2));
            Assert.That(selector.FindTask(EWorkerType.Goblin, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(1));
            Assert.That(registry.TryClaim(2, EWorkerType.Skeleton), Is.True);
            Assert.That(selector.FindTask(EWorkerType.Goblin, EZoneId.Workshop, Vector3.zero, true), Is.Null);
        }

        [Test]
        public void 다른_구역과_다른_작업자의_선점과_처리_중인_개체를_건너뛴다()
        {
            ContaminationRegistry registry = CreateRegistry(
                CreateItem(1, EContaminationType.ClutterItem, 0f, EZoneId.Lounge),
                CreateItem(2, EContaminationType.ClutterItem, 0f),
                CreateItem(3, EContaminationType.ClutterItem, 0f),
                CreateItem(4, EContaminationType.HeavyDebris, 0f),
                CreateItem(5, EContaminationType.Trash, 0f, EZoneId.Floor1Lobby),
                CreateItem(6, EContaminationType.FloorStain, 0f),
                CreateItem(7, EContaminationType.Trash, 5f),
                CreateItem(8, EContaminationType.Trash, 3f));
            var selector = new TaskSelector(registry, _capability);
            Assert.That(registry.TryClaim(2, EWorkerType.Goblin), Is.True);
            Assert.That(registry.TryPickUp(3, EWorkerType.Player), Is.True);
            Assert.That(registry.TryStartDigest(4), Is.True);
            Assert.That(registry.DispatchDischargeBatch(), Is.EqualTo(1));
            Assert.That(registry.TryClaim(6, EWorkerType.Skeleton), Is.True);
            Assert.That(registry.TryCompleteMop(6, EWorkerType.Skeleton), Is.True);
            Assert.That(registry.TryClaim(8, EWorkerType.Orc), Is.True);

            Assert.That(selector.FindTask(EWorkerType.Orc, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(8));
            Assert.That(registry.TryCompleteDigest(4), Is.True);
            Assert.That(selector.FindTask(EWorkerType.Slime, EZoneId.Workshop, Vector3.zero, false).Id, Is.EqualTo(7));
            Assert.That(selector.FindTask(EWorkerType.Slime, EZoneId.Floor1Lobby, Vector3.zero, false), Is.Null);
            Assert.That(selector.FindTask(EWorkerType.Skeleton, EZoneId.Workshop, Vector3.zero, false), Is.Null);
        }

        [Test]
        public void 작업을_고르는_것만으로_개체를_선점하지_않는다()
        {
            ContaminationRegistry registry = CreateRegistry(CreateItem(1, EContaminationType.Trash, 0f));
            var selector = new TaskSelector(registry, _capability);
            int changes = 0;
            registry.ItemChanged += _ => changes++;

            ContaminationItem target = selector.FindTask(EWorkerType.Goblin, EZoneId.Workshop, Vector3.zero, false);

            Assert.That(target, Is.SameAs(registry.FindItem(1)));
            Assert.That(target.ClaimedBy, Is.Null);
            Assert.That(target.State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void 현관_적치_폐기물은_다시_운반하지_않고_슬라임은_먹을_수_있다()
        {
            ContaminationRegistry registry = CreateRegistry(CreateItem(1, EContaminationType.Trash, 0f, EZoneId.Floor1Lobby));
            var selector = new TaskSelector(registry, _capability);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.InDischargeArea));

            Assert.That(selector.FindTask(EWorkerType.Goblin, EZoneId.Floor1Lobby, Vector3.zero, false), Is.Null);
            Assert.That(selector.FindTask(EWorkerType.Orc, EZoneId.Floor1Lobby, Vector3.zero, false), Is.Null);
            Assert.That(selector.FindTask(EWorkerType.Hellhound, EZoneId.Floor1Lobby, Vector3.zero, false), Is.Null);
            Assert.That(selector.FindTask(EWorkerType.Slime, EZoneId.Workshop, Vector3.zero, false), Is.Null);

            ContaminationItem target = selector.FindTask(EWorkerType.Slime, EZoneId.Floor1Lobby, Vector3.zero, false);
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(registry.TryStartDigest(target.Id), Is.True);
            Assert.That(registry.DispatchDischargeBatch(), Is.Zero);
        }

        private static ContaminationItem CreateItem(int id, EContaminationType type, float x,
            EZoneId zone = EZoneId.Workshop)
        {
            return new ContaminationItem(id, type, 1, zone, new Vector3(x, 0f, 0f));
        }

        private static ContaminationRegistry CreateRegistry(params ContaminationItem[] items)
        {
            var layout = new FacilityLayout { DischargeArea = new Bounds(Vector3.zero, Vector3.one) };
            return new ContaminationRegistry(new ZoneMap(layout), items);
        }
    }
}
