using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>규칙이 정한 동작을 캐릭터의 Animator Controller 상태에 연결한다.</summary>
    [DefaultExecutionOrder(20)]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int PoseId = Animator.StringToHash("Pose");
        private static readonly int[] _stateHashes = Array.ConvertAll((ECharacterAnimation[])Enum.GetValues(typeof(ECharacterAnimation)),
            state => Animator.StringToHash($"Base Layer.{state}"));

        [SerializeField] private ArtSlot _artSlot;

        private CharacterAnimationSet _set;
        private Animator _animator;
        private readonly Dictionary<ECharacterAnimation, AnimationClip> _clips = new();
        private readonly List<AnimatorClipInfo> _clipInfo = new();
        private bool _hasController;
        private float _originalAnimatorSpeed;
        private AnimationClip _loop;
        private float _loopTime;
        private Vector3 _cycleTravel;
        private Transform _hips;
        private Transform _leftHand;
        private Transform _rightHand;
        private ArmPose _holdingArm;
        private ArmPose _supportArm;
        private bool _sprint;
        private bool _holdingMop;
        private bool _useMopWalk;
        private EContaminationType? _carried;
        private float _speed;
        private float _eatStarted;
        private UnitMotionTuning _tuning;
        private AnimationClip _handling;
        private ECharacterAnimation _handlingState;
        private bool _handlingHeavy;
        private float _handlingStart;
        private UnitHandlingTiming _handlingTiming;
        private bool _isCinematicSampled;
        private float _actionRate = 1f;

        public EPose CurrentPose { get; private set; }
        public AnimationClip CurrentClip { get; private set; }
        public float ClipTime { get; private set; }
        public bool IsPickingUp { get; private set; }
        private float HandlingElapsed => (Time.time - _handlingStart) * _actionRate;
        public bool IsHandling => _handling != null && HandlingElapsed < _handlingTiming.Duration;
        public float HandlingProgress => _handlingTiming.Duration > 0f
            ? Mathf.Clamp01(HandlingElapsed / _handlingTiming.Duration) : 1f;
        public bool GripHasItem => IsHandling && _handlingTiming.GripHasItem(HandlingElapsed);

        private void OnEnable()
        {
            _useMopWalk = !TryGetComponent<MinionAgent>(out _);
            if (_artSlot == null) { _artSlot = GetComponentInChildren<ArtSlot>(true); }
            if (_artSlot == null) { return; }
            _set = _artSlot.AnimationSet;
            _animator = _artSlot.Animator;
            if (_set == null || _animator == null || _animator.runtimeAnimatorController == null) { return; }

            _animator.applyRootMotion = false;
            _animator.Update(0f);
            if (!HasState(ECharacterAnimation.Idle)) { return; }
            _hasController = true;
            _originalAnimatorSpeed = _animator.speed;
            // 손 접촉과 물건의 표시 시점이 같도록 상태의 시간을 직접 맞춘다.
            _animator.speed = 0f;

            Transform[] bones = _animator.GetComponentsInChildren<Transform>(true);
            _hips = FindBone(bones, "Hips");
            _leftHand = FindBone(bones, "LeftHand");
            _rightHand = FindBone(bones, "RightHand");
            _holdingArm = SampleArm(FindBone(bones, _set.LeftHandCarry ? "LeftArm" : "RightArm"), ECharacterAnimation.Carry);
            AnimationClip heavyPickup = GetClip(ECharacterAnimation.PickUpHeavy);
            if (_set.LeftHandCarry && heavyPickup != null && heavyPickup == GetClip(ECharacterAnimation.PickUp))
            {
                _supportArm = SampleArm(FindBone(bones, "RightArm"), ECharacterAnimation.CarryHeavy);
            }
            AnimationClip idle = GetClip(ECharacterAnimation.Idle);
            if (idle != null) { Sample(ECharacterAnimation.Idle, idle, 0f); }
        }

        private void OnDisable()
        {
            if (_hasController && _animator != null) { _animator.speed = _originalAnimatorSpeed; }
            _hasController = false;
            _isCinematicSampled = false;
            _clips.Clear();
            CurrentClip = null;
            _handling = null;
            _loop = null;
            _holdingArm = null;
            _supportArm = null;
        }

        public void SetMotionTuning(UnitMotionTuning tuning)
        {
            float now = Time.time;
            if (IsHandling)
            {
                float progress = HandlingProgress;
                _handlingTiming = tuning.ResolveHandling(_set, _handling.length, IsPickingUp, _handlingHeavy);
                _handlingStart = now - progress * _handlingTiming.Duration / _actionRate;
            }
            if (CurrentPose == EPose.Eat)
            {
                float eatTime = (now - _eatStarted) * _tuning.EatAnimationRate;
                _eatStarted = now - eatTime / tuning.EatAnimationRate;
            }
            _tuning = tuning;
        }

        internal void SetActionRate(float rate)
        {
            float now = Time.time;
            if (_handling != null) { _handlingStart = now - HandlingElapsed / rate; }
            if (CurrentPose == EPose.Eat) { _eatStarted = now - (now - _eatStarted) * _actionRate / rate; }
            _actionRate = rate;
        }

        public void SetPose(EPose pose, float speed = 0f, bool running = false,
            EContaminationType? carried = null, bool holdingMop = false)
        {
            if (pose == EPose.Eat && CurrentPose != EPose.Eat) { _eatStarted = Time.time; }
            CurrentPose = pose;
            _speed = speed;
            _sprint = running;
            _carried = carried;
            _holdingMop = holdingMop;
            if (!_hasController && _animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetInteger(PoseId, (int)pose);
            }
        }

        /// <summary>실제 모델 배율에서 측정한 발걸음 속도(m/s). 보행이 없는 슬라임 등은 게임의 기본 속도를 사용한다.</summary>
        public float GetLocomotionSpeed(float ruleSpeed, bool running = false, EContaminationType? carried = null, bool holdingMop = false)
        {
            if (_set == null) { return ruleSpeed; }
            float measured;
            if (holdingMop) { measured = _useMopWalk ? _set.MopWalkSpeed : _set.WalkSpeed; }
            else if (carried == EContaminationType.HeavyDebris) { measured = _set.CarryHeavySpeed; }
            else if (carried.HasValue && !running) { measured = _set.CarrySpeed; }
            else { measured = running ? _set.RunSpeed : _set.WalkSpeed; }
            return measured > 0f ? measured : ruleSpeed;
        }

        public float GetAdjustedMoveSpeed(float baseSpeed, float ruleSpeed, bool running = false,
            EContaminationType? carried = null, bool holdingMop = false)
        {
            return _tuning.ResolveMoveSpeed(GetLocomotionSpeed(baseSpeed),
                GetLocomotionSpeed(baseSpeed, running, carried, holdingMop), baseSpeed, ruleSpeed, running, carried, holdingMop);
        }

        public float BeginHandling(bool pickingUp, bool heavy = false)
        {
            if (!_hasController || !_animator.isActiveAndEnabled) { return 0f; }
            ECharacterAnimation state = heavy ? ECharacterAnimation.PickUpHeavy : ECharacterAnimation.PickUp;
            AnimationClip clip = GetClip(state);
            if (clip == null) { return 0f; }
            IsPickingUp = pickingUp;
            _handlingHeavy = heavy;
            _handling = clip;
            _handlingState = state;
            _handlingStart = Time.time;
            _handlingTiming = _tuning.ResolveHandling(_set, clip.length, pickingUp, heavy);
            _loop = null;
            return _handlingTiming.Duration;
        }

        /// <summary>물건은 지정된 한 손, 무거운 잔해는 두 손의 중점, 걸레(type=null)는 오른손을 따른다.</summary>
        public bool TryGetGrip(EContaminationType? type, out Vector3 position, out Quaternion rotation)
        {
            rotation = transform.rotation;
            position = transform.position;
            Transform hand = type.HasValue && _set != null && _set.LeftHandCarry ? _leftHand : _rightHand;
            if (type == EContaminationType.HeavyDebris)
            {
                if (_leftHand == null || _rightHand == null) { return false; }
                position = (_leftHand.position + _rightHand.position) * 0.5f;
            }
            else
            {
                if (hand == null) { return false; }
                position = hand.position;
                if (type.HasValue)
                {
                    // 손목에서 손바닥 바깥으로 옮겨 모델이 허벅지에 겹치지 않게 한다.
                    float width = _leftHand != null && _rightHand != null && _hips != null
                        ? Vector3.Distance(hand.position, _hips.position) * 0.12f : 0f;
                    position += (transform.right * (_set.LeftHandCarry ? -1f : 1f) - transform.forward) * width;
                }
            }
            return true;
        }

        internal void SampleCinematicState(ECharacterAnimation state, float seconds)
        {
            if (!_hasController || GetClip(state) is not AnimationClip clip) { return; }
            _isCinematicSampled = true;
            Sample(state, clip, Mathf.Clamp(seconds, 0f, clip.length));
        }

        internal void SampleCinematicClip(AnimationClip clip, float seconds)
        {
            if (!_hasController || clip == null) { return; }
            _isCinematicSampled = true;
            clip.SampleAnimation(_animator.gameObject, Mathf.Clamp(seconds, 0f, clip.length));
        }

        private void LateUpdate()
        {
            if (_isCinematicSampled || !_hasController || !_animator.isActiveAndEnabled) { return; }
            if (IsHandling)
            {
                float time = _handlingTiming.ClipTime(HandlingElapsed);
                Sample(_handlingState, _handling, time);
                if (_handlingHeavy && _supportArm != null)
                {
                    // 오크가 물건을 들어 올리는 동안 오른팔을 두 손 운반 자세로 가져온다.
                    _supportArm.Apply(Mathf.SmoothStep(0f, 1f, time / Mathf.Max(0.01f, _handlingTiming.Contact)));
                }
                return;
            }
            _handling = null;

            if (CurrentPose == EPose.Eat && GetClip(ECharacterAnimation.Eat) is AnimationClip eat)
            {
                float eatTime = (Time.time - _eatStarted) * _tuning.EatAnimationRate * _actionRate;
                if (eatTime < eat.length)
                {
                    _loop = null;
                    Sample(ECharacterAnimation.Eat, eat, eatTime);
                    return;
                }
            }

            bool moving = CurrentPose == EPose.Walk || CurrentPose == EPose.Dispatch ||
                CurrentPose == EPose.Carry || CurrentPose == EPose.CarryHeavy;
            bool heavy = _carried == EContaminationType.HeavyDebris || CurrentPose == EPose.CarryHeavy;
            var (state, animationRate) = SelectLoop(moving, heavy);
            AnimationClip clip = GetClip(state);
            if (clip == null) { return; }
            if (clip != _loop)
            {
                _loop = clip;
                _loopTime = 0f;
                _cycleTravel = moving ? MeasureCycleTravel(state, clip) : Vector3.zero;
            }

            bool holdHeavy = !moving && heavy && state == ECharacterAnimation.CarryHeavy;
            if (holdHeavy) { _loopTime = 0f; }
            else
            {
                if (moving && _speed > 0f)
                {
                    float reference = GetLocomotionSpeed(_speed, _sprint || CurrentPose == EPose.Dispatch, _carried, _holdingMop);
                    animationRate *= _speed / reference;
                }
                else if (state == ECharacterAnimation.Mop) { animationRate *= _actionRate; }
                _loopTime = Mathf.Repeat(_loopTime + Time.deltaTime * animationRate, Mathf.Max(0.001f, clip.length));
            }
            Sample(state, clip, _loopTime);
            if (moving && _hips != null && clip.length > 0f)
            {
                _hips.position -= transform.TransformVector(_cycleTravel * (_loopTime / clip.length));
            }
            if (!moving && !heavy && _carried.HasValue) { _holdingArm?.Apply(1f); }
        }

        private (ECharacterAnimation State, float Rate) SelectLoop(bool moving, bool heavy)
        {
            if (CurrentPose == EPose.Mop && HasState(ECharacterAnimation.Mop)) { return (ECharacterAnimation.Mop, _tuning.MopAnimationRate); }
            if (CurrentPose == EPose.Dispatch && HasState(ECharacterAnimation.Dispatch)) { return (ECharacterAnimation.Dispatch, _tuning.DispatchAnimationRate); }
            if (heavy && HasState(ECharacterAnimation.CarryHeavy)) { return (ECharacterAnimation.CarryHeavy, _tuning.HeavyCarryAnimationRate); }
            if (!moving) { return (ECharacterAnimation.Idle, _tuning.IdleAnimationRate); }
            if (_holdingMop && _useMopWalk && HasState(ECharacterAnimation.MopWalk))
            {
                return (ECharacterAnimation.MopWalk, _tuning.MopWalkAnimationRate);
            }
            if (_sprint && HasState(ECharacterAnimation.Run)) { return (ECharacterAnimation.Run, _tuning.RunAnimationRate); }
            if ((_carried.HasValue || CurrentPose == EPose.Carry) && HasState(ECharacterAnimation.Carry)) { return (ECharacterAnimation.Carry, _tuning.CarryAnimationRate); }
            return HasState(ECharacterAnimation.Walk) ? (ECharacterAnimation.Walk, _tuning.WalkAnimationRate)
                : (ECharacterAnimation.Idle, _tuning.IdleAnimationRate);
        }

        private Vector3 MeasureCycleTravel(ECharacterAnimation state, AnimationClip clip)
        {
            if (_hips == null || clip == null) { return Vector3.zero; }
            Sample(state, clip, 0f);
            Vector3 start = transform.InverseTransformPoint(_hips.position);
            Sample(state, clip, clip.length);
            Vector3 delta = transform.InverseTransformPoint(_hips.position) - start;
            delta.y = 0f;
            return delta;
        }

        private bool HasState(ECharacterAnimation state) => _animator.HasState(0, _stateHashes[(int)state]);

        private AnimationClip GetClip(ECharacterAnimation state)
        {
            if (_clips.TryGetValue(state, out AnimationClip clip)) { return clip; }
            if (!HasState(state)) { return null; }
            PlayAt(state, 0f);
            _clipInfo.Clear();
            _animator.GetCurrentAnimatorClipInfo(0, _clipInfo);
            clip = _clipInfo.Count > 0 ? _clipInfo[0].clip : null;
            if (clip != null) { _clips.Add(state, clip); }
            return clip;
        }

        private void Sample(ECharacterAnimation state, AnimationClip clip, float time)
        {
            CurrentClip = clip;
            ClipTime = time;
            float normalizedTime = clip.length > 0f ? Mathf.Min(time / clip.length, 0.99999f) : 0f;
            PlayAt(state, normalizedTime);
        }

        private void PlayAt(ECharacterAnimation state, float normalizedTime)
        {
            _animator.Play(_stateHashes[(int)state], 0, normalizedTime);
            _animator.Update(0f);
        }

        private ArmPose SampleArm(Transform upperArm, ECharacterAnimation state)
        {
            if (upperArm == null) { return null; }
            AnimationClip clip = GetClip(state);
            if (clip == null) { return null; }
            Sample(state, clip, 0f);
            return new ArmPose(upperArm);
        }

        private static Transform FindBone(Transform[] bones, string suffix)
        {
            foreach (Transform bone in bones)
            {
                if (bone.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { return bone; }
            }
            return null;
        }

        private sealed class ArmPose
        {
            private readonly Transform[] _bones;
            private readonly Quaternion[] _rotations;

            public ArmPose(Transform upperArm)
            {
                _bones = upperArm.GetComponentsInChildren<Transform>(true);
                _rotations = new Quaternion[_bones.Length];
                for (int i = 0; i < _bones.Length; i++) { _rotations[i] = _bones[i].localRotation; }
            }

            public void Apply(float weight)
            {
                for (int i = 0; i < _bones.Length; i++)
                {
                    _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _rotations[i], weight);
                }
            }
        }
    }
}
