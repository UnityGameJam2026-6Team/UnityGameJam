using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>Art 에디터가 해당 유닛 FBX의 실제 클립과 보행 속도를 프리팹에 저장한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationSet : MonoBehaviour
    {
        [Header("자세와 이동")]
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _walk;
        [SerializeField] private AnimationClip _run;

        [Header("집기와 운반")]
        [SerializeField] private AnimationClip _pickUp;
        [SerializeField] private AnimationClip _pickUpHeavy;
        [SerializeField] private AnimationClip _carry;
        [SerializeField] private AnimationClip _carryHeavy;
        [SerializeField] private bool _leftHandCarry;
        [Tooltip("가벼운 물건 집기 클립에서 손이 물건에 닿는 시각과 들어 올리기가 끝나는 시각(초)")]
        [SerializeField] private float _pickUpContact;
        [SerializeField] private float _pickUpEnd;
        [Tooltip("무거운 잔해 집기 클립에서 양손이 닿는 시각과 들어 올리기가 끝나는 시각(초)")]
        [SerializeField] private float _pickUpHeavyContact;
        [SerializeField] private float _pickUpHeavyEnd;

        [Header("도구와 특수 행동")]
        [SerializeField] private AnimationClip _mopWalk;
        [SerializeField] private AnimationClip _mop;
        [SerializeField] private AnimationClip _eat;
        [SerializeField] private AnimationClip _dispatch;

        [Header("클립에서 측정한 이동 속도 (m/s)")]
        [SerializeField] private float _walkSpeed;
        [SerializeField] private float _runSpeed;
        [SerializeField] private float _carrySpeed;
        [SerializeField] private float _carryHeavySpeed;
        [SerializeField] private float _mopWalkSpeed;

        public AnimationClip Idle => _idle;
        public AnimationClip Walk => _walk;
        public AnimationClip Run => _run;
        public AnimationClip PickUp => _pickUp;
        public AnimationClip PickUpHeavy => _pickUpHeavy;
        public AnimationClip Carry => _carry;
        public AnimationClip CarryHeavy => _carryHeavy;
        public bool LeftHandCarry => _leftHandCarry;
        public float PickUpContact => _pickUpContact;
        public float PickUpEnd => _pickUpEnd;
        public float PickUpHeavyContact => _pickUpHeavyContact;
        public float PickUpHeavyEnd => _pickUpHeavyEnd;
        public AnimationClip MopWalk => _mopWalk;
        public AnimationClip Mop => _mop;
        public AnimationClip Eat => _eat;
        public AnimationClip Dispatch => _dispatch;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float CarrySpeed => _carrySpeed;
        public float CarryHeavySpeed => _carryHeavySpeed;
        public float MopWalkSpeed => _mopWalkSpeed;
    }
}
