using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec Player 규칙 1·3·5를 가짜 판으로 확인한다(규칙 구현 전 B 레인 개발용).</summary>
    public class FakeMatchSessionTest
    {
        private sealed class FakeBody : IWorkerBody
        {
            public Vector3 Position { get; set; }
            public EFloor Floor => EFloor.Floor1;
        }

        private static readonly FakeMatchSettings _settings = new FakeMatchSettings(180f, 4f, 0.5f, 2f, 1);

        private FakeBody _body;
        private FakeMatchSession _session;

        [SetUp]
        public void SetUp()
        {
            var layout = new FacilityLayout();
            layout.Floors.Add(new FloorArea { Floor = EFloor.Floor1, Area = Box(0f, 20f, 0f, 10f) });
            var lobby = new ZoneArea { Zone = EZoneId.Floor1Lobby, Floor = EFloor.Floor1 };
            lobby.Areas.Add(Box(0f, 10f, 0f, 10f));
            var lounge = new ZoneArea { Zone = EZoneId.Lounge, Floor = EFloor.Floor1 };
            lounge.Areas.Add(Box(10f, 20f, 0f, 10f));
            layout.Zones.Add(lobby);
            layout.Zones.Add(lounge);
            layout.DischargeArea = Box(0f, 2f, 0f, 2f);
            layout.SpawnPoints.Add(new SpawnPoint { Zone = EZoneId.Floor1Lobby, Type = EContaminationType.HeavyDebris, Position = new Vector3(3f, 0f, 3f) });
            layout.SpawnPoints.Add(new SpawnPoint { Zone = EZoneId.Floor1Lobby, Type = EContaminationType.Trash, Position = new Vector3(5f, 0f, 5f) });
            layout.SpawnPoints.Add(new SpawnPoint { Zone = EZoneId.Lounge, Type = EContaminationType.FloorStain, Position = new Vector3(15f, 0f, 5f) });
            layout.SpawnPoints.Add(new SpawnPoint { Zone = EZoneId.Lounge, Type = EContaminationType.ClutterItem, Position = new Vector3(17f, 0f, 5f) });
            layout.HomeSlots.Add(new HomeSlot { SlotId = 0, Zone = EZoneId.Lounge, Position = new Vector3(18f, 0f, 8f) });

            layout.HomeSlots.Insert(0, new HomeSlot { SlotId = 8, Zone = EZoneId.Floor1Lobby, Position = new Vector3(5f, 0f, 5f) });
            _body = new FakeBody { Position = new Vector3(5f, 0f, 5f) };
            var setup = new MatchSetup { Layout = layout, Seed = 1, PlayerBody = _body };
            _session = new FakeMatchSession(setup, _settings);
            _session.StartMatch();
        }

        [Test]
        public void 무거운_잔해를_들면_이동_속도가_절반이_된다()
        {
            Assert.AreEqual(4f, _session.PlayerMoveSpeed, 0.001f);

            Assert.IsTrue(_session.TryPickUp(ItemOf(EContaminationType.HeavyDebris).ItemId));

            Assert.AreEqual(2f, _session.PlayerMoveSpeed, 0.001f);
        }

        [Test]
        public void 일반_쓰레기를_들면_속도가_그대로다()
        {
            _session.TryPickUp(ItemOf(EContaminationType.Trash).ItemId);

            Assert.AreEqual(4f, _session.PlayerMoveSpeed, 0.001f);
        }

        [Test]
        public void 닦기를_시작하면_진행도가_0에서_100으로_오른다()
        {
            int stain = ItemOf(EContaminationType.FloorStain).ItemId;
            Assert.IsTrue(_session.TryBorrowMop(EFloor.Floor1));
            Assert.IsTrue(_session.TryStartMop(stain));
            Assert.IsTrue(_session.IsPlayerWorking);
            Assert.AreEqual(0f, _session.PlayerWorkProgress, 0.001f);

            _session.Tick(1f);
            Assert.AreEqual(50f, _session.PlayerWorkProgress, 0.001f);

            _session.Tick(1f);
            Assert.IsFalse(_session.IsPlayerWorking);
            Assert.AreEqual(0f, _session.PlayerWorkProgress, 0.001f);
            Assert.AreEqual(EItemState.Resolved, _session.Items.Single(i => i.ItemId == stain).State);
        }

        [Test]
        public void 작업을_취소하면_진행도가_0이_되고_얼룩이_남는다()
        {
            int stain = ItemOf(EContaminationType.FloorStain).ItemId;
            _session.TryBorrowMop(EFloor.Floor1);
            _session.TryStartMop(stain);
            _session.Tick(1f);

            _session.CancelWork();

            Assert.IsFalse(_session.IsPlayerWorking);
            Assert.AreEqual(0f, _session.PlayerWorkProgress, 0.001f);
            Assert.AreEqual(EItemState.OnFloor, _session.Items.Single(i => i.ItemId == stain).State);
        }

        [Test]
        public void 내려놓으면_플레이어가_있는_구역의_바닥이_된다()
        {
            _session.TryPickUp(ItemOf(EContaminationType.Trash).ItemId);
            _body.Position = new Vector3(12f, 0f, 5f);

            Assert.IsTrue(_session.TryDrop());

            ItemSnapshot trash = ItemOf(EContaminationType.Trash);
            Assert.AreEqual(EItemState.OnFloor, trash.State);
            Assert.AreEqual(12f, trash.Position.x, 0.001f);
            Assert.AreEqual(100f * (1f - 4f / 7f), _session.GetZoneCleanliness(EZoneId.Floor1Lobby), 0.01f); // 잔해 4 / 시작 7
            Assert.AreEqual(0f, _session.GetZoneCleanliness(EZoneId.Lounge), 0.01f); // 시작 3보다 많아져 0으로 자름
        }

        [Test]
        public void 배출_구역에_내려놓고_헬하운드를_보내면_제거된다()
        {
            _session.TryPickUp(ItemOf(EContaminationType.Trash).ItemId);
            _body.Position = new Vector3(1f, 0f, 1f);
            _session.TryDrop();
            Assert.AreEqual(EItemState.InDischargeArea, ItemOf(EContaminationType.Trash).State);

            Assert.IsTrue(_session.DispatchHellhound());

            Assert.AreEqual(EItemState.Dispatched, ItemOf(EContaminationType.Trash).State);
        }

        [Test]
        public void 작은_물건을_제자리_옆에서_정리하면_처리된다()
        {
            _session.TryPickUp(ItemOf(EContaminationType.ClutterItem).ItemId);
            _body.Position = new Vector3(18f, 0f, 7f);

            Assert.IsTrue(_session.TryPlaceAtHome());

            Assert.AreEqual(EItemState.Resolved, ItemOf(EContaminationType.ClutterItem).State);
            Assert.IsNull(_session.PlayerCarriedItemId);
        }

        [Test]
        public void 시간이_다_되면_판이_끝난다()
        {
            _session.Tick(180f);

            Assert.AreEqual(EMatchState.Resolved, _session.MatchState);
            Assert.IsNotNull(_session.Result);
        }

        [Test]
        public void 작은_물건은_같은_구역의_제자리_슬롯과_짝을_짓는다()
        {
            ItemSnapshot item = ItemOf(EContaminationType.ClutterItem);
            Assert.AreEqual(0, item.HomeSlotId);
            Assert.IsNull(ItemOf(EContaminationType.Trash).HomeSlotId);
            Assert.IsTrue(_session.TryPickUp(item.ItemId));
            Assert.IsFalse(_session.TryPlaceAtHome());
            Assert.AreEqual(item.ItemId, _session.PlayerCarriedItemId);
            Assert.AreEqual(item.HomeSlotId, ItemOf(item.Type).HomeSlotId);
        }

        private ItemSnapshot ItemOf(EContaminationType type)
        {
            return _session.Items.First(i => i.Type == type);
        }

        private static Bounds Box(float minX, float maxX, float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, -0.5f, minZ), new Vector3(maxX, 3f, maxZ));
            return bounds;
        }
    }
}
