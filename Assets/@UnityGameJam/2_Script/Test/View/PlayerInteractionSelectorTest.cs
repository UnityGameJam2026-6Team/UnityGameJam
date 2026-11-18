using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec Player 규칙 4: E키가 고르는 행동의 우선순위와 반경.</summary>
    public class PlayerInteractionSelectorTest
    {
        private const float Radius = 1.5f;
        private static readonly Vector3 Toilet = new Vector3(10f, 0f, 0f);
        private static readonly Vector3 Slot = new Vector3(20f, 0f, 0f);
        private static readonly Vector3 Hellhound = new Vector3(30f, 0f, 0f);

        private PlayerInteractionSelector _selector;
        private List<ItemSnapshot> _items;

        [SetUp]
        public void SetUp()
        {
            var layout = new FacilityLayout();
            layout.Toilets.Add(new ToiletStation { Floor = EFloor.Floor2, MopPickupPosition = Toilet });
            layout.HomeSlots.Add(new HomeSlot { SlotId = 7, Zone = EZoneId.Lounge, Position = Slot });
            _selector = new PlayerInteractionSelector(layout);
            _items = new List<ItemSnapshot>();
        }

        [Test]
        public void 작은_물건을_들고_제자리_옆이면_제자리_정리를_고른다()
        {
            _items.Add(Item(1, EContaminationType.Trash, Slot + Vector3.right * 0.5f));

            InteractionChoice choice = Select(Slot + Vector3.left, EContaminationType.ClutterItem, false);

            Assert.AreEqual(EInteractionKind.PlaceAtHome, choice.Kind);
        }

        [Test]
        public void 빈손으로_화장실_옆이면_걸레를_빌린다()
        {
            InteractionChoice choice = Select(Toilet + Vector3.forward, null, false);

            Assert.AreEqual(EInteractionKind.BorrowMop, choice.Kind);
            Assert.AreEqual(EFloor.Floor2, choice.Floor);
        }

        [Test]
        public void 걸레를_들고_화장실_옆이면_반납한다()
        {
            Assert.AreEqual(EInteractionKind.ReturnMop, Select(Toilet, null, true).Kind);
        }

        [Test]
        public void 물건을_들고_화장실_옆이면_걸레를_빌리지_않는다()
        {
            Assert.AreEqual(EInteractionKind.None, Select(Toilet, EContaminationType.Trash, false).Kind);
        }

        [Test]
        public void 헬하운드_옆이면_배출을_명령한다()
        {
            Assert.AreEqual(EInteractionKind.DispatchHellhound, Select(Hellhound + Vector3.left, null, false).Kind);
        }

        [Test]
        public void 졸개_옆이면_대화를_고른다()
        {
            var minions = new List<(EWorkerType, Vector3)> { (EWorkerType.Orc, new Vector3(40f, 0f, 0f)) };

            InteractionChoice choice = _selector.Select(Context(new Vector3(40f, 0f, 1f), null, false), _items, minions);

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

            Assert.AreEqual(EInteractionKind.None, Select(new Vector3(50f, 0f, 0f), EContaminationType.Trash, false).Kind);
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

        private InteractionChoice Select(Vector3 position, EContaminationType? carried, bool isHoldingMop)
        {
            return _selector.Select(Context(position, carried, isHoldingMop), _items, new List<(EWorkerType, Vector3)>());
        }

        private static PlayerInteractionContext Context(Vector3 position, EContaminationType? carried, bool isHoldingMop)
        {
            return new PlayerInteractionContext(position, Radius, carried, isHoldingMop, Hellhound);
        }

        private static ItemSnapshot Item(int id, EContaminationType type, Vector3 position, EItemState state = EItemState.OnFloor)
        {
            return new ItemSnapshot(id, type, state, position, null);
        }
    }
}
