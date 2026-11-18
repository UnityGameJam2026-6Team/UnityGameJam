using System;
using UnityEngine;

namespace UnityGameJam.View
{
    public enum ESfxCue
    {
        None, Navigate, Confirm, Cancel, Unavailable, PanelOpen, PanelClose, Proposal, Notice, ProposalExpire,
        TitleStamp, TitleTextPop, TitleCrownLand, RoundStart, OneMinute, TimerNudge, Heartbeat, TimeUp,
        Success, Failure, LightStep, HeavyStep, SkeletonStep, SlimeStep, HoundStep,
        SoftPickup, SoftPlace, HardPickup, HardPlace, HeavyPickup, HeavyPlace, Organized,
        MopBorrow, MopReturn, MopScrub, StainClear, Swallow, DigestDone, HoundDispatch, HoundReturn, RoomTone
    }

    [CreateAssetMenu(menuName = "UnityGameJam/SFX Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrarySO : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            public ESfxCue Kind;
            public AudioClip[] Clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 0.6f;
        }

        [SerializeField] private Cue[] _cues = Array.Empty<Cue>();
        [SerializeField, Min(1)] private int _worldVoices = 8;
        [SerializeField, Min(1)] private int _stepVoices = 4;
        [SerializeField, Min(1)] private int _minionScrubVoices = 2;
        [SerializeField, Min(0.1f)] private float _minimumDistance = 1.5f;
        [SerializeField, Min(1f)] private float _maximumDistance = 12f;
        [SerializeField, Min(0.05f)] private float _stepDistance = 0.8f;
        [SerializeField, Min(0.05f)] private float _slimeStepDistance = 0.65f;
        [SerializeField, Min(0.05f)] private float _impactInterval = 0.06f;

        public Cue[] Cues => _cues;
        public int WorldVoices => _worldVoices;
        public int StepVoices => _stepVoices;
        public int MinionScrubVoices => _minionScrubVoices;
        public float MinimumDistance => _minimumDistance;
        public float MaximumDistance => _maximumDistance;
        public float StepDistance => _stepDistance;
        public float SlimeStepDistance => _slimeStepDistance;
        public float ImpactInterval => _impactInterval;
    }
}
