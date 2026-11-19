using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameSfxPlayer : MonoBehaviour
    {
        [SerializeField] private SfxLibrarySO _library;
        private static GameSfxPlayer _instance;
        private readonly Dictionary<ESfxCue, SfxLibrarySO.Cue> _cues = new();
        private readonly Dictionary<ESfxCue, int> _lastVariant = new();
        private readonly Dictionary<ESfxCue, int> _lastFrame = new();
        private readonly Dictionary<EWorkerType, AudioSource> _scrubs = new();
        private readonly List<AudioListener> _previousListeners = new();
        private AudioSource[] _global;
        private AudioSource[] _world;
        private AudioSource[] _steps;
        private AudioSource _ambient;
        private AudioListener _listener;
        private MatchBootstrap _bootstrap;
        private float _lastImpact = float.NegativeInfinity;
        private AudioClip _shoutSound;
        private AudioClip _surgeSound;

        public SfxLibrarySO Library => _library;
        internal static GameSfxPlayer Instance => _instance;

        private void Awake()
        {
            _instance = this;
            foreach (SfxLibrarySO.Cue cue in _library.Cues) { _cues[cue.Kind] = cue; }
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            _global = CreatePool("Global", 4, 0f);
            _world = CreatePool("World", _library.WorldVoices, 1f);
            _steps = CreatePool("Step", _library.StepVoices, 1f);
            _ambient = CreateSource("RoomTone", 0f);
        }

        private void OnDestroy()
        {
            if (_shoutSound != null) { Destroy(_shoutSound); }
            if (_surgeSound != null) { Destroy(_surgeSound); }
            if (_listener != null) { _listener.enabled = false; }
            if (_instance == this) { _instance = null; }
            foreach (AudioListener listener in _previousListeners)
            {
                if (listener != null) { listener.enabled = true; }
            }
        }

        internal void BeginMatch()
        {
            // 리스너를 플레이어 높이에 맞춰 탑다운 카메라 높이에 따른 과도한 거리 감쇠를 피한다.
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (!listener.enabled || listener == _listener) { continue; }
                listener.enabled = false;
                _previousListeners.Add(listener);
            }
            if (_listener == null)
            {
                var listenerObject = new GameObject("PlayerAudioListener");
                listenerObject.transform.SetParent(transform, false);
                _listener = listenerObject.AddComponent<AudioListener>();
            }
            UpdateListener();
            Play(ESfxCue.RoundStart);
            PlaySource(_ambient, ESfxCue.RoomTone, -1, true);
        }

        private void LateUpdate()
        {
            UpdateListener();
            if (_bootstrap?.Player == null) { return; }
            foreach (KeyValuePair<EWorkerType, AudioSource> pair in _scrubs)
            {
                if (pair.Value.isPlaying && !CanHear(pair.Value.transform.position, _bootstrap.Player.Floor))
                {
                    pair.Value.Stop();
                }
            }
        }

        private void UpdateListener()
        {
            if (_listener != null && _bootstrap?.Player != null)
            {
                _listener.transform.SetPositionAndRotation(_bootstrap.Player.Position, Quaternion.identity);
            }
        }

        public static void Play(ESfxCue cue)
        {
            if (_instance == null || cue == ESfxCue.None) { return; }
            if (_instance._lastFrame.TryGetValue(cue, out int frame) && frame == Time.frameCount) { return; }
            _instance._lastFrame[cue] = Time.frameCount;
            int priority = cue == ESfxCue.TimeUp ? 0
                : cue == ESfxCue.OneMinute || cue == ESfxCue.Heartbeat || cue == ESfxCue.TimerNudge ? 16
                : cue == ESfxCue.Success || cue == ESfxCue.Failure || cue == ESfxCue.Proposal ? 32 : 48;
            AudioSource source = _instance.FindVoice(_instance._global);
            if (source == null)
            {
                source = _instance._global[0];
                foreach (AudioSource candidate in _instance._global)
                {
                    if (candidate.priority > source.priority) { source = candidate; }
                }
                if (source.priority < priority) { return; }
            }
            source.priority = priority;
            _instance.PlaySource(source, cue);
        }

        internal static void PlayCommandSkill(ECommandSkill skill)
        {
            if (_instance == null) { return; }
            AudioSource source = _instance.FindVoice(_instance._global);
            if (source == null) { return; }
            bool shout = skill == ECommandSkill.Shout;
            AudioClip clip = shout
                ? (_instance._shoutSound ??= CreateCommandSound(true))
                : (_instance._surgeSound ??= CreateCommandSound(false));
            source.clip = clip;
            source.priority = 24;
            source.loop = false;
            source.pitch = 1f;
            source.volume = .86f;
            source.Play();
        }

        private static AudioClip CreateCommandSound(bool shout)
        {
            const int rate = 24000;
            float duration = shout ? .72f : .55f;
            var data = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float time = t / duration;
                float phase = 2f * Mathf.PI * (shout ? t * (96f - 30f * t) : t * (230f + 640f * t));
                float envelope = Mathf.Min(1f, t / .03f) * (1f - time) * (1f - time);
                float tone = Mathf.Sin(phase) + .42f * Mathf.Sin(2f * phase) + .21f * Mathf.Sin(3f * phase);
                float attack = Mathf.Sin(2f * Mathf.PI * (54f - 34f * t) * t) * Mathf.Exp(-15f * t);
                data[i] = envelope * (tone * .43f + .35f * attack);
            }
            AudioClip clip = AudioClip.Create(shout ? "Shout" : "Surge", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        internal void PlayWorld(ESfxCue cue, Vector3 position, EFloor floor, int variant = -1)
        {
            if (!CanHear(position, floor)) { return; }
            if (ActiveWorldVoices() >= _library.WorldVoices) { return; }
            bool isStep = cue >= ESfxCue.LightStep && cue <= ESfxCue.HoundStep;
            bool isImpact = cue >= ESfxCue.SoftPickup && cue <= ESfxCue.HeavyPlace;
            if (isImpact && Time.unscaledTime - _lastImpact < _library.ImpactInterval) { return; }
            AudioSource source = FindVoice(isStep ? _steps : _world);
            if (source == null) { return; }
            source.transform.position = position;
            if (PlaySource(source, cue, variant) && isImpact) { _lastImpact = Time.unscaledTime; }
        }

        internal bool CanHear(Vector3 position, EFloor floor)
        {
            return _bootstrap?.Player != null && _bootstrap.CanHearAt(position, floor)
                && Vector3.Distance(position, _bootstrap.Player.Position) <= _library.MaximumDistance;
        }

        internal void SetScrub(EWorkerType worker, bool isWorking, Vector3 position, EFloor floor)
        {
            if (!_scrubs.TryGetValue(worker, out AudioSource source))
            {
                if (!isWorking) { return; }
                source = CreateSource(worker + "Scrub", 1f);
                _scrubs.Add(worker, source);
            }
            source.transform.position = position;
            bool canPlay = isWorking && CanHear(position, floor);
            if (canPlay && worker != EWorkerType.Player)
            {
                int count = 0;
                foreach (KeyValuePair<EWorkerType, AudioSource> pair in _scrubs)
                {
                    if (pair.Key != worker && pair.Key != EWorkerType.Player && pair.Value.isPlaying) { count++; }
                }
                canPlay = count < _library.MinionScrubVoices;
            }
            if (!canPlay) { source.Stop(); }
            else if (!source.isPlaying && (worker == EWorkerType.Player || ActiveWorldVoices() < _library.WorldVoices))
            {
                PlaySource(source, ESfxCue.MopScrub, -1, true);
            }
        }

        internal void EndRound()
        {
            StopAll();
            Play(ESfxCue.TimeUp);
        }

        public static void ShowResult(bool isSuccess)
        {
            if (_instance == null) { return; }
            foreach (AudioSource source in _instance._global) { source.Stop(); }
            Play(isSuccess ? ESfxCue.Success : ESfxCue.Failure);
        }

        internal static float PlayRestartConfirmation()
        {
            if (_instance == null) { return 0f; }
            _instance.StopAll();
            AudioSource source = _instance._global[0];
            return _instance.PlaySource(source, ESfxCue.Confirm) ? source.clip.length : 0f;
        }

        private void StopAll()
        {
            foreach (AudioSource source in _global) { source.Stop(); }
            foreach (AudioSource source in _world) { source.Stop(); }
            foreach (AudioSource source in _steps) { source.Stop(); }
            foreach (AudioSource source in _scrubs.Values) { source.Stop(); }
            _ambient.Stop();
        }

        private AudioSource[] CreatePool(string name, int count, float spatial)
        {
            var sources = new AudioSource[count];
            for (int i = 0; i < count; i++) { sources[i] = CreateSource(name + i, spatial); }
            return sources;
        }

        private AudioSource CreateSource(string name, float spatial)
        {
            var voice = new GameObject(name);
            voice.transform.SetParent(transform, false);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            source.dopplerLevel = 0f;
            source.priority = name == "RoomTone" ? 224 : name.StartsWith("Step") ? 160 : spatial > 0f ? 96 : 32;
            source.minDistance = _library.MinimumDistance;
            source.maxDistance = _library.MaximumDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private AudioSource FindVoice(AudioSource[] voices)
        {
            foreach (AudioSource voice in voices)
            {
                if (!voice.isPlaying) { return voice; }
            }
            return null;
        }

        private int ActiveWorldVoices()
        {
            int count = 0;
            foreach (AudioSource source in _world) { if (source.isPlaying) { count++; } }
            foreach (AudioSource source in _steps) { if (source.isPlaying) { count++; } }
            foreach (AudioSource source in _scrubs.Values) { if (source.isPlaying) { count++; } }
            return count;
        }

        private bool PlaySource(AudioSource source, ESfxCue kind, int variant = -1, bool loop = false)
        {
            if (!_cues.TryGetValue(kind, out SfxLibrarySO.Cue cue) || cue.Clips.Length == 0) { return false; }
            int index = variant >= 0 ? Mathf.Min(variant, cue.Clips.Length - 1) : Random.Range(0, cue.Clips.Length);
            if (variant < 0 && cue.Clips.Length > 1 && _lastVariant.TryGetValue(kind, out int last) && index == last)
            {
                index = (index + 1) % cue.Clips.Length;
            }
            _lastVariant[kind] = index;
            source.clip = cue.Clips[index];
            source.volume = cue.Volume;
            source.loop = loop;
            source.Play();
            return true;
        }
    }
}
