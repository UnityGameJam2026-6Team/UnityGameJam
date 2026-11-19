using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec Player 규칙 4: 상호작용 대상의 우선순위와 반경.</summary>
    public class PlayerInteractionSelectorTest
    {
        private const float Radius = 1.5f;
        private static readonly Vector3 Toilet = new Vector3(10f, 0f, 0f);
        private static readonly Vector3 Slot = new Vector3(20f, 0f, 0f);
        private static readonly Vector3 Hellhound = new Vector3(30f, 0f, 0f);

        private PlayerInteractionSelector _selector;
        private List<ItemSnapshot> _items;

        [TestCase(EContaminationType.Trash, EItemState.OnFloor, false, false, 1.5f, true)]
        [TestCase(EContaminationType.HeavyDebris, EItemState.OnFloor, false, false, 1f, true)]
        [TestCase(EContaminationType.ClutterItem, EItemState.OnFloor, false, false, 1f, true)]
        [TestCase(EContaminationType.Trash, EItemState.OnFloor, false, true, 1f, false)]
        [TestCase(EContaminationType.Trash, EItemState.OnFloor, false, false, 2f, false)]
        [TestCase(EContaminationType.Trash, EItemState.InDischargeArea, false, false, 1f, false)]
        [TestCase(EContaminationType.Trash, EItemState.Carried, false, false, 1f, false)]
        [TestCase(EContaminationType.Trash, EItemState.Resolved, false, false, 1f, false)]
        [TestCase(EContaminationType.FloorStain, EItemState.OnFloor, false, false, 1f, false)]
        [TestCase(EContaminationType.FloorStain, EItemState.OnFloor, true, false, 1f, true)]
        [TestCase(EContaminationType.HeavyDebris, EItemState.OnFloor, true, false, 1f, false)]
        public void 손과_도구_상태에_맞는_반경_안의_바닥_작업만_강조한다(EContaminationType type, EItemState state,
            bool holdsMop, bool carriesItem, float distance, bool expected)
        {
            ItemSnapshot? carried = carriesItem ? Item(2, EContaminationType.Trash, Vector3.zero, EItemState.Carried) : null;
            var context = new PlayerInteractionContext(Vector3.zero, EFloor.Floor1, Radius, carried, holdsMop, null, null);
            Assert.AreEqual(expected, PlayerInteractionSelector.CanHighlightItem(context, Item(1, type, Vector3.right * distance, state)));
        }

        [SetUp]
        public void SetUp()
        {
            var layout = new FacilityLayout();
            layout.Toilets.Add(new ToiletStation { Floor = EFloor.Floor2, MopPickupPosition = Toilet });
            layout.HomeSlots.Add(new HomeSlot { SlotId = 7, Zone = EZoneId.Lounge, Position = Slot });
            layout.HomeSlots.Add(new HomeSlot { SlotId = 8, Zone = EZoneId.Lounge, Position = Slot + Vector3.left });
            _selector = new PlayerInteractionSelector(layout);
            _items = new List<ItemSnapshot>();
        }

        [Test]
        public void 작은_물건을_들고_제자리_옆이면_제자리_정리를_고른다()
        {
            _items.Add(Item(1, EContaminationType.Trash, Slot + Vector3.right * 0.5f));

            InteractionChoice choice = Select(Slot + Vector3.left, EContaminationType.ClutterItem, false, 7);

            Assert.AreEqual(EInteractionKind.PlaceAtHome, choice.Kind);
            Assert.AreEqual(Slot, choice.TargetPosition);
        }

        [TestCase(7)]
        [TestCase(null)]
        public void 가까운_자리가_든_물건의_제자리가_아니면_정리를_고르지_않는다(int? homeSlotId)
        {
            InteractionChoice choice = Select(Slot + Vector3.left * 2f, EContaminationType.ClutterItem, false, homeSlotId);

            Assert.AreEqual(EInteractionKind.Drop, choice.Kind);
        }

        [Test]
        public void 빈손으로_화장실_옆이면_걸레를_빌린다()
        {
            InteractionChoice choice = Select(Toilet + Vector3.forward, null, false, floor: EFloor.Floor2);

            Assert.AreEqual(EInteractionKind.BorrowMop, choice.Kind);
            Assert.AreEqual(EFloor.Floor2, choice.Floor);
        }

        [Test]
        public void 걸레를_들고_화장실_옆이면_반납한다()
        {
            Assert.AreEqual(EInteractionKind.ReturnMop, Select(Toilet, null, true, floor: EFloor.Floor2, mopOrigin: EFloor.Floor2).Kind);
        }

        [Test]
        public void 다른_층에서_빌린_걸레는_이_층_걸레걸이에_반납을_표시하지_않는다()
        {
            Assert.AreEqual(EInteractionKind.None, Select(Toilet, null, true, floor: EFloor.Floor2, mopOrigin: EFloor.Floor1).Kind);
        }

        [Test]
        public void 좌표가_같아도_다른_층의_제자리에_정리를_표시하지_않는다()
        {
            Assert.AreEqual(EInteractionKind.Drop, Select(Slot, EContaminationType.ClutterItem, false, 7, EFloor.Floor2).Kind);
        }

        [Test]
        public void 물건을_들고_화장실_옆이면_걸레를_빌리지_않는다()
        {
            Assert.AreEqual(EInteractionKind.Drop, Select(Toilet, EContaminationType.Trash, false, floor: EFloor.Floor2).Kind);
        }

        [Test]
        public void 헬하운드_옆이면_대화_상대로_고른다()
        {
            InteractionChoice choice = Select(Hellhound + Vector3.left, null, false);
            Assert.AreEqual(EInteractionKind.TalkToMinion, choice.Kind);
            Assert.AreEqual(EWorkerType.Hellhound, choice.Minion);
        }

        [Test]
        public void 졸개_옆이면_대화를_고른다()
        {
            var minions = new List<(EWorkerType, Vector3)> { (EWorkerType.Orc, new Vector3(40f, 0f, 0f)) };

            var context = new PlayerInteractionContext(new Vector3(40f, 0f, 1f), EFloor.Floor1, Radius, null, false, null, Hellhound);
            InteractionChoice choice = _selector.Select(context, _items, minions);

            Assert.AreEqual(EInteractionKind.TalkToMinion, choice.Kind);
            Assert.AreEqual(EWorkerType.Orc, choice.Minion);
        }

        [Test]
        public void 걸레를_들고_얼룩_옆이면_닦기를_고른다()
        {
            _items.Add(Item(3, EContaminationType.FloorStain, new Vector3(50f, 0f, 0f)));

            InteractionChoice choice = Select(new Vector3(50f, 0f, 1f), null, true);

            Assert.AreEqual(EInteractionKind.StartMop, choice.Kind);
            Assert.AreEqual(3, choice.ItemId);
        }

        [Test]
        public void 빈손이면_가장_가까운_개체를_줍는다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(51f, 0f, 0f)));
            _items.Add(Item(2, EContaminationType.HeavyDebris, new Vector3(50.3f, 0f, 0f)));

            InteractionChoice choice = Select(new Vector3(50f, 0f, 0f), null, false);

            Assert.AreEqual(EInteractionKind.PickUp, choice.Kind);
            Assert.AreEqual(2, choice.ItemId);
        }

        [Test]
        public void 배출_구역에_놓인_개체도_주울_수_있다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(50.5f, 0f, 0f), EItemState.InDischargeArea));

            Assert.AreEqual(EInteractionKind.PickUp, Select(new Vector3(50f, 0f, 0f), null, false).Kind);
        }

        [Test]
        public void 무언가를_들고_있으면_줍지_않는다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(50.5f, 0f, 0f)));

            Assert.AreEqual(EInteractionKind.Drop, Select(new Vector3(50f, 0f, 0f), EContaminationType.Trash, false).Kind);
        }

        [Test]
        public void 걸레를_들고_있으면_줍지_않는다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(50.5f, 0f, 0f)));

            Assert.AreEqual(EInteractionKind.None, Select(new Vector3(50f, 0f, 0f), null, true).Kind);
        }

        [Test]
        public void 얼룩은_주울_수_없다()
        {
            _items.Add(Item(1, EContaminationType.FloorStain, new Vector3(50.5f, 0f, 0f)));

            Assert.AreEqual(EInteractionKind.None, Select(new Vector3(50f, 0f, 0f), null, false).Kind);
        }

        [Test]
        public void 운반_중이거나_처리된_개체는_줍지_않는다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(50.5f, 0f, 0f), EItemState.Carried));
            _items.Add(Item(2, EContaminationType.Trash, new Vector3(50.5f, 0f, 0f), EItemState.Resolved));

            Assert.AreEqual(EInteractionKind.None, Select(new Vector3(50f, 0f, 0f), null, false).Kind);
        }

        [Test]
        public void 반경_밖의_대상은_고르지_않는다()
        {
            _items.Add(Item(1, EContaminationType.Trash, new Vector3(52f, 0f, 0f)));

            Assert.AreEqual(EInteractionKind.None, Select(new Vector3(50f, 0f, 0f), null, false).Kind);
        }

        private InteractionChoice Select(Vector3 position, EContaminationType? carried, bool isHoldingMop, int? homeSlotId = null,
            EFloor floor = EFloor.Floor1, EFloor? mopOrigin = null)
        {
            ItemSnapshot? item = carried.HasValue
                ? new ItemSnapshot(1, carried.Value, EItemState.Carried, position, EWorkerType.Player, homeSlotId)
                : (ItemSnapshot?)null;
            var context = new PlayerInteractionContext(position, floor, Radius, item, isHoldingMop, mopOrigin, Hellhound);
            return _selector.Select(context, _items, new List<(EWorkerType, Vector3)>());
        }

        private static ItemSnapshot Item(int id, EContaminationType type, Vector3 position, EItemState state = EItemState.OnFloor)
        {
            return new ItemSnapshot(id, type, state, position, null);
        }
    }
}
