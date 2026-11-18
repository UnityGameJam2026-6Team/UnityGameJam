using NUnit.Framework;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec MinionAgent 규칙 6: 상태로 자세(Pose) 고르기.</summary>
    public class MinionPoseResolverTest
    {
        private const float Moving = 2f;
        private const float Still = 0f;

        [Test]
        public void 대화_중에_멈춰_있으면_Talk이다()
        {
            Assert.AreEqual(EPose.Talk, MinionPoseResolver.Resolve(Still, EMinionActivity.Talking, null, false, false));
            Assert.AreEqual(EPose.Talk, MinionPoseResolver.Resolve(Still, EMinionActivity.AwaitingResponse, null, false, false));
        }

        [Test]
        public void 소화_중이면_Eat이다()
        {
            Assert.AreEqual(EPose.Eat, MinionPoseResolver.Resolve(Moving, EMinionActivity.MovingToRoom, null, false, true));
        }

        [Test]
        public void 무거운_잔해를_들고_움직이면_CarryHeavy다()
        {
            Assert.AreEqual(EPose.CarryHeavy, MinionPoseResolver.Resolve(Moving, EMinionActivity.CarryingWaste, EContaminationType.HeavyDebris, false, false));
        }

        [Test]
        public void 물건을_들고_움직이면_Carry다()
        {
            Assert.AreEqual(EPose.Carry, MinionPoseResolver.Resolve(Moving, EMinionActivity.CarryingWaste, EContaminationType.Trash, false, false));
        }

        [Test]
        public void 빈손으로_움직이면_Walk이다()
        {
            Assert.AreEqual(EPose.Walk, MinionPoseResolver.Resolve(Moving, EMinionActivity.MovingToRoom, null, false, false));
        }

        [Test]
        public void 배출하러_움직이면_Dispatch다()
        {
            Assert.AreEqual(EPose.Dispatch, MinionPoseResolver.Resolve(Moving, EMinionActivity.Dispatching, null, false, false));
        }

        [Test]
        public void 작업_중에_걸레를_들고_멈추면_Mop이다()
        {
            Assert.AreEqual(EPose.Mop, MinionPoseResolver.Resolve(Still, EMinionActivity.Working, null, true, false));
        }

        [Test]
        public void 할_일_없이_멈춰_있으면_Idle이다()
        {
            Assert.AreEqual(EPose.Idle, MinionPoseResolver.Resolve(Still, EMinionActivity.LobbyIdle, null, false, false));
        }

        [Test]
        public void 대화_중이라도_움직이면_Talk이_아니다()
        {
            Assert.AreEqual(EPose.Walk, MinionPoseResolver.Resolve(Moving, EMinionActivity.Talking, null, false, false));
        }

        [Test]
        public void 자세_값은_클립_목록_순서와_같다()
        {
            Assert.AreEqual(0, (int)EPose.Idle);
            Assert.AreEqual(7, (int)EPose.Dispatch);
        }
    }
}
