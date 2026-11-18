using UnityEngine;

namespace UnityGameJam.View
{
    public enum ECharacterAnimation
    {
        Idle, Walk, Run, PickUp, PickUpHeavy, Carry, CarryHeavy, MopWalk, Mop, Eat, Dispatch
    }

    /// <summary>Animator Controller의 동작에서 측정한 이동 속도와 집기 시각을 저장한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationSet : MonoBehaviour
    {
        [Header("집기와 운반")]
        [SerializeField] private bool _leftHandCarry;
        [Tooltip("가벼운 물건 집기 클립에서 손이 물건에 닿는 시각과 들어 올리기가 끝나는 시각(초)")]
        [SerializeField] private float _pickUpContact;
        [SerializeField] private float _pickUpEnd;
        [Tooltip("무거운 잔해 집기 클립에서 양손이 닿는 시각과 들어 올리기가 끝나는 시각(초)")]
        [SerializeField] private float _pickUpHeavyContact;
        [SerializeField] private float _pickUpHeavyEnd;

        [Header("클립에서 측정한 이동 속도 (m/s)")]
        [SerializeField] private float _walkSpeed;
        [SerializeField] private float _runSpeed;
        [SerializeField] private float _carrySpeed;
        [SerializeField] private float _carryHeavySpeed;
        [SerializeField] private float _mopWalkSpeed;

        public bool LeftHandCarry => _leftHandCarry;
        public float PickUpContact => _pickUpContact;
        public float PickUpEnd => _pickUpEnd;
        public float PickUpHeavyContact => _pickUpHeavyContact;
        public float PickUpHeavyEnd => _pickUpHeavyEnd;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float CarrySpeed => _carrySpeed;
        public float CarryHeavySpeed => _carryHeavySpeed;
        public float MopWalkSpeed => _mopWalkSpeed;
    }
}
