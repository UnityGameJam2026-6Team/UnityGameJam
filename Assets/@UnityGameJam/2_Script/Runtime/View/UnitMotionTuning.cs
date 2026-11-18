using System;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    [Serializable]
    public struct UnitMotionTuning
    {
        [Header("이동 속도 배율")]
        [SerializeField] private float _walkRate;
        [SerializeField] private float _runRate;
        [SerializeField] private float _carryRate;
        [SerializeField] private float _heavyCarryRate;
        [SerializeField] private float _mopWalkRate;

        [Header("애니메이션 배속")]
        [SerializeField] private float _idleAnimationRate;
        [SerializeField] private float _walkAnimationRate;
        [SerializeField] private float _runAnimationRate;
        [SerializeField] private float _carryAnimationRate;
        [SerializeField] private float _heavyCarryAnimationRate;
        [SerializeField] private float _mopWalkAnimationRate;
        [SerializeField] private float _mopAnimationRate;
        [SerializeField] private float _pickupRate;
        [SerializeField] private float _dropRate;
        [SerializeField] private float _eatAnimationRate;
        [SerializeField] private float _dispatchAnimationRate;

        public float IdleAnimationRate => NormalizeRate(_idleAnimationRate);
        public float WalkAnimationRate => NormalizeRate(_walkAnimationRate);
        public float RunAnimationRate => NormalizeRate(_runAnimationRate);
        public float CarryAnimationRate => NormalizeRate(_carryAnimationRate);
        public float HeavyCarryAnimationRate => NormalizeRate(_heavyCarryAnimationRate);
        public float MopWalkAnimationRate => NormalizeRate(_mopWalkAnimationRate);
        public float MopAnimationRate => NormalizeRate(_mopAnimationRate);
        public float EatAnimationRate => NormalizeRate(_eatAnimationRate);
        public float DispatchAnimationRate => NormalizeRate(_dispatchAnimationRate);

        public float ResolveMoveSpeed(float walkSpeed, float measuredSpeed, float baseSpeed, float ruleSpeed,
            bool running, EContaminationType? carried, bool holdingMop)
        {
            if (baseSpeed <= 0f || ruleSpeed <= 0f) { return ruleSpeed; }
            float ruleMultiplier = ruleSpeed / baseSpeed;
            float rate = holdingMop ? _mopWalkRate : carried switch
            {
                EContaminationType.HeavyDebris => _heavyCarryRate,
                not null => _carryRate,
                null => running ? _runRate : _walkRate
            };
            float speed = carried == EContaminationType.HeavyDebris && ruleMultiplier < 1f
                ? walkSpeed * NormalizeRate(_walkRate) : measuredSpeed;
            return speed * ruleMultiplier * NormalizeRate(rate);
        }

        public UnitHandlingTiming ResolveHandling(CharacterAnimationSet clips, bool pickingUp, bool heavy)
        {
            AnimationClip clip = heavy ? clips.PickUpHeavy : clips.PickUp;
            if (clip == null) { return default; }
            float measuredEnd = heavy ? clips.PickUpHeavyEnd : clips.PickUpEnd;
            float measuredContact = heavy ? clips.PickUpHeavyContact : clips.PickUpContact;
            float end = Mathf.Min(clip.length, PositiveOr(measuredEnd, clip.length));
            float contact = PositiveOr(measuredContact, end * 0.5f);
            return new UnitHandlingTiming(end, contact, NormalizeRate(pickingUp ? _pickupRate : _dropRate), pickingUp);
        }

        public static float NormalizeRate(float value) => Mathf.Clamp(PositiveOr(value, 1f), 0.05f, 10f);

        private static float PositiveOr(float value, float fallback) => float.IsFinite(value) && value > 0f ? value : fallback;
    }

    public readonly struct UnitHandlingTiming
    {
        private readonly float _end;
        private readonly float _rate;
        private readonly bool _isPickup;

        public UnitHandlingTiming(float end, float contact, float rate, bool isPickup)
        {
            _end = end;
            _rate = rate;
            _isPickup = isPickup;
            Contact = Mathf.Clamp(contact, 0f, end);
        }

        public float Contact { get; }
        public float Duration => _rate > 0f ? _end / _rate : 0f;
        public float ContactSeconds => _rate > 0f ? (_isPickup ? Contact : _end - Contact) / _rate : 0f;

        public float ClipTime(float elapsed)
        {
            float current = Mathf.Clamp(elapsed * _rate, 0f, _end);
            return _isPickup ? current : _end - current;
        }

        public bool GripHasItem(float elapsed) => ClipTime(elapsed) >= Contact;
    }
}
