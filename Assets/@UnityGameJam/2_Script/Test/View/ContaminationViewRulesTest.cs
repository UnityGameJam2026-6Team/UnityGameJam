using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec ContaminationView 규칙 2·4·5.</summary>
    public class ContaminationViewRulesTest
    {
        [Test]
        public void 바닥과_배출_구역과_운반_중일_때만_보인다()
        {
            Assert.IsTrue(ContaminationViewRules.IsVisible(EItemState.OnFloor));
            Assert.IsTrue(ContaminationViewRules.IsVisible(EItemState.InDischargeArea));
            Assert.IsTrue(ContaminationViewRules.IsVisible(EItemState.Carried));
            Assert.IsFalse(ContaminationViewRules.IsVisible(EItemState.Digesting));
            Assert.IsFalse(ContaminationViewRules.IsVisible(EItemState.Resolved));
            Assert.IsFalse(ContaminationViewRules.IsVisible(EItemState.Dispatched));
        }

        [Test]
        public void 운반_중이면_운반자_머리_위에_있다()
        {
            Vector3 position = ContaminationViewRules.CarryPosition(new Vector3(3f, 0f, 4f), 1.7f, 0.3f);

            Assert.AreEqual(new Vector3(3f, 2f, 4f), position);
        }

        [Test]
        public void 프리팹은_ID를_개수로_나눈_나머지로_고른다()
        {
            Assert.AreEqual(1, ContaminationViewRules.ChoosePrefabIndex(5, 2));
            Assert.AreEqual(0, ContaminationViewRules.ChoosePrefabIndex(4, 1));
            Assert.AreEqual(-1, ContaminationViewRules.ChoosePrefabIndex(3, 0));
        }

        [Test]
        public void 작은_물건은_제자리_슬롯의_종류를_따른다()
        {
            var kinds = new Dictionary<int, string> { { 3, "Cup" } };

            Assert.AreEqual("Cup", ContaminationViewRules.ResolveClutterKind(3, kinds));
        }

        [Test]
        public void 제자리_슬롯이_없으면_종류를_정하지_않는다()
        {
            var kinds = new Dictionary<int, string> { { 3, "Cup" } };

            Assert.IsNull(ContaminationViewRules.ResolveClutterKind(null, kinds));
            Assert.IsNull(ContaminationViewRules.ResolveClutterKind(9, kinds));
        }
    }
}
