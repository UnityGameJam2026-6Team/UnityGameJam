using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    public class CarryAnimationStateTest
    {
        [Test]
        public void 운반자_상태가_바뀔_때에만_한_번씩_집고_내려놓는다()
        {
            var calls = new List<(bool, bool)>();
            var state = new CarryAnimationState(EWorkerType.Orc, (pickup, heavy) => calls.Add((pickup, heavy)));
            var held = new ItemSnapshot(1, EContaminationType.HeavyDebris, EItemState.Carried, Vector3.zero, EWorkerType.Orc);
            state.Observe(held);
            state.Observe(held);
            state.Observe(new ItemSnapshot(2, EContaminationType.Trash, EItemState.Carried, Vector3.zero, EWorkerType.Player));
            Assert.That(state.CarriedType, Is.EqualTo(EContaminationType.HeavyDebris));
            state.Observe(new ItemSnapshot(1, EContaminationType.HeavyDebris, EItemState.InDischargeArea, Vector3.zero, null));
            Assert.That(state.CarriedType, Is.Null);
            CollectionAssert.AreEqual(new[] { (true, true), (false, true) }, calls);
        }

        [Test]
        public void 걸레를_빌리고_반납할_때만_가벼운_취급_동작을_시작한다()
        {
            var calls = new List<(bool, bool)>();
            var state = new CarryAnimationState(EWorkerType.Skeleton, (pickup, heavy) => calls.Add((pickup, heavy)));
            state.ObserveMop(false);
            state.ObserveMop(true);
            state.ObserveMop(true);
            state.ObserveMop(false);
            CollectionAssert.AreEqual(new[] { (true, false), (false, false) }, calls);
        }
    }
}
