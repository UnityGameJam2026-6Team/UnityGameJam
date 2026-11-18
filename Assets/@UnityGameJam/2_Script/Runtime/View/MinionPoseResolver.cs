using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>애니메이터 Pose 값. 아트 클립 목록(ArtKeyRule.AllowedClipNames) 순서와 같다(U201).</summary>
    public enum EPose
    {
        Idle = 0,
        Walk = 1,
        Carry = 2,
        CarryHeavy = 3,
        Mop = 4,
        Eat = 5,
        Talk = 6,
        Dispatch = 7,
    }

    /// <summary>캐릭터 상태로 자세를 고른다(Spec MinionAgent 규칙 6).</summary>
    public static class MinionPoseResolver
    {
        public const float MovingSpeed = 0.1f;

        public static EPose Resolve(float speed, EMinionActivity activity, EContaminationType? carriedType, bool isHoldingMop, bool isDigesting)
        {
            bool isMoving = speed > MovingSpeed;
            bool isTalking = activity == EMinionActivity.Talking || activity == EMinionActivity.AwaitingResponse;

            if (isTalking && !isMoving) { return EPose.Talk; }
            if (isDigesting) { return EPose.Eat; }
            if (isMoving)
            {
                if (activity == EMinionActivity.Dispatching) { return EPose.Dispatch; }
                if (carriedType == EContaminationType.HeavyDebris) { return EPose.CarryHeavy; }
                return carriedType.HasValue ? EPose.Carry : EPose.Walk;
            }
            return activity == EMinionActivity.Working && isHoldingMop ? EPose.Mop : EPose.Idle;
        }
    }
}
