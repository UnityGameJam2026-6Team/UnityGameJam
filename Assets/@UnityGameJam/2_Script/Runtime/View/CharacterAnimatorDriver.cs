using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>규칙이 정한 동작을 해당 유닛 FBX의 클립으로 재생한다. 몸의 실제 이동은 컨트롤러가 맡는다.</summary>
    [DefaultExecutionOrder(20)]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int PoseId = Animator.StringToHash("Pose");

        [SerializeField] private ArtSlot _artSlot;

        private CharacterAnimationSet _set;
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationClipPlayable _playable;
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
        private EContaminationType? _carried;
        private float _speed;
        private float _eatStarted;
        private UnitMotionTuning _tuning;
        private AnimationClip _handling;
        private bool _handlingHeavy;
        private float _handlingStart;
        private UnitHandlingTiming _handlingTiming;

        public EPose CurrentPose { get; private set; }
        public AnimationClip CurrentClip { get; private set; }
        public float ClipTime { get; private set; }
        public bool IsPickingUp { get; private set; }
        public bool IsHandling => _handling != null && Time.time - _handlingStart < _handlingTiming.Duration;
        public float HandlingProgress => _handlingTiming.Duration > 0f
            ? Mathf.Clamp01((Time.time - _handlingStart) / _handlingTiming.Duration) : 1f;
        public bool GripHasItem => IsHandling && _handlingTiming.GripHasItem(Time.time - _handlingStart);

        private void OnEnable()
        {
            if (_artSlot == null) { _artSlot = GetComponentInChildren<ArtSlot>(true); }
            if (_artSlot == null) { return; }
            _set = _artSlot.AnimationSet;
            _animator = _artSlot.Animator;
            if (_set == null || _set.Idle == null || _animator == null) { return; }

            _animator.applyRootMotion = false;
            _graph = PlayableGraph.Create(gameObject.name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _output = AnimationPlayableOutput.Create(_graph, "FBX", _animator);
            _graph.Play();

            Transform[] bones = _animator.GetComponentsInChildren<Transform>(true);
            _hips = FindBone(bones, "Hips");
            _leftHand = FindBone(bones, "LeftHand");
            _rightHand = FindBone(bones, "RightHand");
            if (_set.Carry != null)
            {
                _holdingArm = SampleArm(FindBone(bones, _set.LeftHandCarry ? "LeftArm" : "RightArm"), _set.Carry);
            }
            if (_set.LeftHandCarry && _set.PickUpHeavy != null && _set.PickUpHeavy == _set.PickUp)
            {
                _supportArm = SampleArm(FindBone(bones, "RightArm"), _set.CarryHeavy);
            }
            Sample(_set.Idle, 0f);
        }

        private void OnDisable()
        {
            if (_graph.IsValid()) { _graph.Destroy(); }
            CurrentClip = null;
            _handling = null;
            _loop = null;
            _holdingArm = null;
            _supportArm = null;
        }

        public void SetMotionTuning(UnitMotionTuning tuning) => _tuning = tuning;

        public void SetPose(EPose pose, float speed = 0f, bool running = false,
            EContaminationType? carried = null, bool holdingMop = false)
        {
            if (pose == EPose.Eat && CurrentPose != EPose.Eat) { _eatStarted = Time.time; }
            CurrentPose = pose;
            _speed = speed;
            _sprint = running;
            _carried = carried;
            _holdingMop = holdingMop;
            if (!_graph.IsValid() && _animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetInteger(PoseId, (int)pose);
            }
        }

        /// <summary>실제 모델 배율에서 측정한 발걸음 속도(m/s). 보행이 없는 슬라임 등은 게임의 기본 속도를 사용한다.</summary>
        public float GetLocomotionSpeed(float ruleSpeed, bool running = false, EContaminationType? carried = null, bool holdingMop = false)
        {
            if (_set == null) { return ruleSpeed; }
            float measured;
            if (holdingMop) { measured = _set.MopWalkSpeed; }
            else if (carried == EContaminationType.HeavyDebris) { measured = _set.CarryHeavySpeed; }
            else if (carried.HasValue) { measured = _set.CarrySpeed; }
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
            if (!_graph.IsValid()) { return 0f; }
            AnimationClip clip = heavy ? _set.PickUpHeavy : _set.PickUp;
            if (clip == null) { return 0f; }
            IsPickingUp = pickingUp;
            _handlingHeavy = heavy;
            _handling = clip;
            _handlingStart = Time.time;
            _handlingTiming = _tuning.ResolveHandling(_set, pickingUp, heavy);
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

        private void LateUpdate()
        {
            if (!_graph.IsValid() || !_animator.gameObject.activeInHierarchy) { return; }
            if (IsHandling)
            {
                float time = _handlingTiming.ClipTime(Time.time - _handlingStart);
                Sample(_handling, time);
                if (_handlingHeavy && _supportArm != null)
                {
                    // 오크가 물건을 들어 올리는 동안 오른팔을 두 손 운반 자세로 가져온다.
                    _supportArm.Apply(Mathf.SmoothStep(0f, 1f, time / Mathf.Max(0.01f, _handlingTiming.Contact)));
                }
                return;
            }
            _handling = null;

            if (CurrentPose == EPose.Eat && _set.Eat != null && Time.time - _eatStarted < _set.Eat.length)
            {
                _loop = null;
                Sample(_set.Eat, Time.time - _eatStarted);
                return;
            }

            bool moving = CurrentPose == EPose.Walk || CurrentPose == EPose.Dispatch ||
                CurrentPose == EPose.Carry || CurrentPose == EPose.CarryHeavy;
            bool heavy = _carried == EContaminationType.HeavyDebris || CurrentPose == EPose.CarryHeavy;
            var (clip, animationRate) = SelectLoop(moving, heavy);
            if (clip != _loop)
            {
                _loop = clip;
                _loopTime = 0f;
                _cycleTravel = moving ? MeasureCycleTravel(clip) : Vector3.zero;
            }

            bool holdHeavy = !moving && heavy && clip == _set.CarryHeavy;
            if (holdHeavy) { _loopTime = 0f; }
            else
            {
                if (moving && _speed > 0f)
                {
                    float reference = GetLocomotionSpeed(_speed, _sprint || CurrentPose == EPose.Dispatch, _carried, _holdingMop);
                    animationRate *= _speed / reference;
                }
                _loopTime = Mathf.Repeat(_loopTime + Time.deltaTime * animationRate, Mathf.Max(0.001f, clip.length));
            }
            Sample(clip, _loopTime);
            if (moving && _hips != null && clip.length > 0f)
            {
                _hips.position -= transform.TransformVector(_cycleTravel * (_loopTime / clip.length));
            }
            if (!moving && !heavy && _carried.HasValue) { _holdingArm?.Apply(1f); }
        }

        private (AnimationClip Clip, float Rate) SelectLoop(bool moving, bool heavy)
        {
            if (CurrentPose == EPose.Mop && _set.Mop != null) { return (_set.Mop, _tuning.MopAnimationRate); }
            if (CurrentPose == EPose.Dispatch && _set.Dispatch != null) { return (_set.Dispatch, 1f); }
            if (heavy && _set.CarryHeavy != null) { return (_set.CarryHeavy, _tuning.HeavyCarryAnimationRate); }
            if (!moving) { return (_set.Idle, _tuning.IdleAnimationRate); }
            if (_holdingMop && _set.MopWalk != null) { return (_set.MopWalk, _tuning.MopWalkAnimationRate); }
            if ((_carried.HasValue || CurrentPose == EPose.Carry) && _set.Carry != null) { return (_set.Carry, _tuning.CarryAnimationRate); }
            if (_sprint && _set.Run != null) { return (_set.Run, _tuning.RunAnimationRate); }
            return _set.Walk != null ? (_set.Walk, _tuning.WalkAnimationRate) : (_set.Idle, _tuning.IdleAnimationRate);
        }

        private Vector3 MeasureCycleTravel(AnimationClip clip)
        {
            if (_hips == null || clip == null) { return Vector3.zero; }
            Sample(clip, 0f);
            Vector3 start = transform.InverseTransformPoint(_hips.position);
            Sample(clip, clip.length);
            Vector3 delta = transform.InverseTransformPoint(_hips.position) - start;
            delta.y = 0f;
            return delta;
        }

        private void Sample(AnimationClip clip, float time)
        {
            if (clip != CurrentClip)
            {
                if (_playable.IsValid())
                {
                    _output.SetSourcePlayable(Playable.Null);
                    _playable.Destroy();
                }
                _playable = AnimationClipPlayable.Create(_graph, clip);
                _playable.SetSpeed(0d);
                _output.SetSourcePlayable(_playable);
                CurrentClip = clip;
            }
            ClipTime = time;
            _playable.SetTime(time);
            _graph.Evaluate(0f);
        }

        private ArmPose SampleArm(Transform upperArm, AnimationClip clip)
        {
            if (upperArm == null || clip == null) { return null; }
            Sample(clip, 0f);
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
