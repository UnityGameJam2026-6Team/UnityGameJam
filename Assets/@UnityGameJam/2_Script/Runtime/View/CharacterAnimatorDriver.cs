using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>
    /// 자세(Pose)를 모델의 애니메이터에 넣는다(U201). 화이트박스처럼 모델이나 컨트롤러가 없으면 아무것도 하지 않는다.
    /// 애니메이터는 ArtSlot이 찾아 준다(Visual 아래를 경로로 찾지 않는다).
    /// </summary>
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int PoseId = Animator.StringToHash("Pose");

        [SerializeField] private ArtSlot _artSlot;

        public EPose CurrentPose { get; private set; }

        public void SetPose(EPose pose)
        {
            CurrentPose = pose;
            Animator animator = _artSlot != null ? _artSlot.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null) { return; }
            animator.SetInteger(PoseId, (int)pose);
        }
    }
}
