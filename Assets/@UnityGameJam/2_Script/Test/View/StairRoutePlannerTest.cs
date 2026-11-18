using NUnit.Framework;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec MinionAgent 규칙 3: 지나갈 층 목록.</summary>
    public class StairRoutePlannerTest
    {
        [Test]
        public void 같은_층이면_계단을_거치지_않는다()
        {
            CollectionAssert.IsEmpty(StairRoutePlanner.FloorsToPass(EFloor.Floor2, EFloor.Floor2));
        }

        [Test]
        public void 일층에서_삼층은_계단을_두_번_지난다()
        {
            CollectionAssert.AreEqual(new[] { EFloor.Floor2, EFloor.Floor3 }, StairRoutePlanner.FloorsToPass(EFloor.Floor1, EFloor.Floor3));
        }

        [Test]
        public void 삼층에서_일층은_이층을_거쳐_내려간다()
        {
            CollectionAssert.AreEqual(new[] { EFloor.Floor2, EFloor.Floor1 }, StairRoutePlanner.FloorsToPass(EFloor.Floor3, EFloor.Floor1));
        }

        [Test]
        public void 바로_위층은_계단을_한_번_지난다()
        {
            CollectionAssert.AreEqual(new[] { EFloor.Floor2 }, StairRoutePlanner.FloorsToPass(EFloor.Floor1, EFloor.Floor2));
        }
    }
}
