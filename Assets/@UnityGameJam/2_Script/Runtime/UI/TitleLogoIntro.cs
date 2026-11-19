using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonKing.UI
{
    /// <summary>
    /// "마왕님 행차하십니다!" title logo: pop-out intro + idle emphasis.
    ///   1. 마왕님 slams in from the screen (stamp + shake)
    ///   2. 행차하십니다! springs up under it
    ///   3. the broom/mop bar stretches open from below
    ///   4. the castle crown drops onto the top and squashes
    ///   5. the whole logo gives one last punch, then floats/breathes and punches again every few seconds.
    /// Every part animates relative to the pose it has in the prefab, so parts can be moved freely in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleLogoIntro : MonoBehaviour
    {
        [Header("Parts (back to front)")]
        public RectTransform shaker;
        public RectTransform crown;
        public RectTransform banner;
        public RectTransform line2;
        public RectTransform line1;
        public RectTransform line1Shadow;
        public RectTransform line2Shadow;

        [Header("Playback")]
        public bool playOnEnable = true;
        [Tooltip("Keeps animating while Time.timeScale = 0 (pause menus).")]
        public bool useUnscaledTime = true;
        [Min(0f)] public float startDelay = 0.15f;
        [Min(0.1f)] public float speed = 1f;

        [Header("Feel")]
        [Tooltip("How big 마왕님 starts before it slams down.")]
        public float slamStartScale = 2.6f;
        public float shakeStrength = 1f;
        [Tooltip("Idle: 마왕님 punches again every N seconds. 0 = off.")]
        public float idlePunchEvery = 5f;
        public float idleFloat = 6f;
        public float idleBreath = 0.015f;

        [Header("Shadow (under the two text lines)")]
        public Vector2 shadowOffset = new Vector2(10f, -12f);
        [Range(0f, 1f)] public float shadowAlpha = 0.4f;

        [Header("Events")]
        [Tooltip("마왕님 hits the screen (stamp SFX).")]
        public UnityEvent onSlam;
        [Tooltip("행차하십니다! lands.")]
        public UnityEvent onLine2;
        [Tooltip("The crown lands.")]
        public UnityEvent onCrownLand;
        public UnityEvent onIntroFinished;

        [Tooltip("For capture tools: drive time with Advance(dt) instead of Update.")]
        public bool manualAdvance;

        // timeline (seconds, before speed)
        const float T_LINE1 = 0f, T_LINE1_FALL = 0.14f;
        const float T_LINE2 = 0.30f;
        const float T_BANNER = 0.46f;
        const float T_CROWN = 0.62f, T_CROWN_FALL = 0.22f;
        const float T_PUNCH = 1.08f;
        const float T_END = 1.75f;

        struct Pose { public Vector2 pos; public Vector3 scale; public float rot; }
        Pose _crown, _banner, _line1, _line2, _shaker;
        CanvasGroup _gCrown, _gBanner, _gLine1, _gLine2;
        Graphic _line1ShadowGraphic, _line2ShadowGraphic;
        bool _captured, _playing, _finished, _evSlam, _evLine2, _evCrown;
        float _t, _idleT, _nextPunch;

        // ------------------------------------------------------------------ API
        public bool IsPlaying { get { return _playing && !_finished; } }

        public void Play()
        {
            Capture();
            _t = -startDelay;
            _idleT = 0f;
            _nextPunch = idlePunchEvery;
            _playing = true; _finished = false;
            _evSlam = _evLine2 = _evCrown = false;
            Apply();
        }

        /// <summary>Jump straight to the finished logo (e.g. when the player skips).</summary>
        public void Skip()
        {
            Capture();
            if (!_playing) { _playing = true; _evSlam = _evLine2 = _evCrown = true; }
            _t = T_END;
            Apply();
        }

        /// <summary>Hide every part (call Play() later to bring it in).</summary>
        public void Hide()
        {
            Capture();
            _playing = false; _finished = false;
            SetAlpha(_gCrown, 0f); SetAlpha(_gBanner, 0f); SetAlpha(_gLine1, 0f); SetAlpha(_gLine2, 0f);
        }

        public void Advance(float dt) { Step(dt); }

        // ------------------------------------------------------------------ lifecycle
        void Awake() { Capture(); }

        void OnEnable()
        {
            if (playOnEnable) Play(); else if (!_playing) RestoreRest();
        }

        void OnDisable() { if (_captured) RestoreRest(); }

        void Update()
        {
            if (manualAdvance) return;
            Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        void Step(float dt)
        {
            if (!_playing) return;
            dt *= speed;
            _t += dt;
            if (_t >= T_END)
            {
                _idleT += dt;
                if (!_finished) { _finished = true; onIntroFinished.Invoke(); }
            }
            Apply();
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;
            _shaker = Grab(shaker); _crown = Grab(crown); _banner = Grab(banner); _line1 = Grab(line1); _line2 = Grab(line2);
            _gCrown = Group(crown); _gBanner = Group(banner); _gLine1 = Group(line1); _gLine2 = Group(line2);
            if (line1Shadow != null) { _line1ShadowGraphic = line1Shadow.GetComponent<Graphic>(); }
            if (line2Shadow != null) { _line2ShadowGraphic = line2Shadow.GetComponent<Graphic>(); }
        }

        static Pose Grab(RectTransform rt)
        {
            if (rt == null) return new Pose { scale = Vector3.one };
            return new Pose { pos = rt.anchoredPosition, scale = rt.localScale, rot = rt.localEulerAngles.z };
        }

        static CanvasGroup Group(RectTransform rt)
        {
            if (rt == null) return null;
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            g.interactable = false; g.blocksRaycasts = false;
            return g;
        }

        void RestoreRest()
        {
            Set(shaker, _shaker, Vector2.zero, Vector2.one, 0f);
            Set(crown, _crown, Vector2.zero, Vector2.one, 0f);
            Set(banner, _banner, Vector2.zero, Vector2.one, 0f);
            Set(line1, _line1, Vector2.zero, Vector2.one, 0f);
            Set(line2, _line2, Vector2.zero, Vector2.one, 0f);
            SetAlpha(_gCrown, 1f); SetAlpha(_gBanner, 1f); SetAlpha(_gLine1, 1f); SetAlpha(_gLine2, 1f);
            SetShadow(line1Shadow, _line1ShadowGraphic, 0f);
            SetShadow(line2Shadow, _line2ShadowGraphic, 0f);
        }

        // ------------------------------------------------------------------ curves
        // damped "hit" wobble: starts at 0, swings positive first
        static float Wobble(float t, float amp, float freq, float decay)
        {
            if (t <= 0f) return 0f;
            return amp * Mathf.Exp(-decay * t) * Mathf.Sin(2f * Mathf.PI * freq * t);
        }

        // underdamped spring step 0 -> 1 (overshoots once or twice)
        static float Spring(float t, float freq, float zeta)
        {
            if (t <= 0f) return 0f;
            float w = 2f * Mathf.PI * freq, wd = w * Mathf.Sqrt(1f - zeta * zeta);
            return 1f - Mathf.Exp(-zeta * w * t) * (Mathf.Cos(wd * t) + zeta * w / wd * Mathf.Sin(wd * t));
        }

        static float Shake(float t, float amp, float seed)
        {
            if (t <= 0f) return 0f;
            return amp * Mathf.Exp(-11f * t) * Mathf.Sin(2f * Mathf.PI * (21f + seed * 3f) * t + seed);
        }

        // ------------------------------------------------------------------ pose
        void Apply()
        {
            float t = _t;

            // --- 1. 마왕님: slam from the screen
            float a1 = t - T_LINE1;
            Vector2 s1 = Vector2.one; float r1 = 0f, al1 = 0f, h1 = 0f;
            if (a1 >= 0f)
            {
                al1 = Mathf.Clamp01(a1 / 0.06f);
                if (a1 < T_LINE1_FALL)
                {
                    float u = a1 / T_LINE1_FALL; u *= u;
                    float s = Mathf.Lerp(slamStartScale, 1f, u);
                    s1 = new Vector2(s, s); r1 = Mathf.Lerp(-9f, 0f, u); h1 = s - 1f;
                }
                else
                {
                    float k = a1 - T_LINE1_FALL;
                    float squash = Wobble(k, 0.16f, 2.6f, 7f);
                    s1 = new Vector2(1f + squash * 0.55f, 1f - squash);
                    r1 = Wobble(k, 2.5f, 2.1f, 6f);
                    if (!_evSlam) { _evSlam = true; onSlam.Invoke(); }
                }
            }

            // --- 2. 행차하십니다!: spring up underneath
            float a2 = t - T_LINE2;
            Vector2 s2 = Vector2.zero, o2 = Vector2.zero; float r2 = 0f, al2 = 0f;
            if (a2 >= 0f)
            {
                float sp = Spring(a2, 2.3f, 0.42f);
                // y overshoots less than x so the line doesn't dive behind 마왕님
                s2 = new Vector2(Mathf.LerpUnclamped(0.2f, 1f, sp), Mathf.LerpUnclamped(0.25f, 1f, Spring(a2, 2.2f, 0.58f)));
                o2 = new Vector2(0f, (1f - sp) * -60f);
                r2 = (1f - Spring(a2, 1.8f, 0.4f)) * 7f;
                al2 = Mathf.Clamp01(a2 / 0.07f);
                if (!_evLine2 && a2 > 0.12f) { _evLine2 = true; onLine2.Invoke(); }
            }

            // --- 3. broom & mop bar: stretch open from below
            float a3 = t - T_BANNER;
            Vector2 s3 = Vector2.zero, o3 = Vector2.zero; float al3 = 0f, r3 = 0f;
            if (a3 >= 0f)
            {
                float sp = Spring(a3, 2.0f, 0.45f);
                s3 = new Vector2(Mathf.LerpUnclamped(0.15f, 1f, sp), Mathf.LerpUnclamped(0.5f, 1f, Spring(a3, 2.4f, 0.4f)));
                o3 = new Vector2(0f, (1f - Spring(a3, 1.7f, 0.5f)) * -110f);
                al3 = Mathf.Clamp01(a3 / 0.1f);
            }

            // --- 4. castle crown: drop + squash
            float a4 = t - T_CROWN;
            Vector2 s4 = Vector2.one, o4 = Vector2.zero; float al4 = 0f, r4 = 0f;
            if (a4 >= 0f)
            {
                al4 = Mathf.Clamp01(a4 / 0.05f);
                if (a4 < T_CROWN_FALL)
                {
                    float u = a4 / T_CROWN_FALL;
                    o4 = new Vector2(0f, 340f * (1f - u * u));
                    s4 = new Vector2(0.94f, 1.08f);
                }
                else
                {
                    float k = a4 - T_CROWN_FALL;
                    float squash = Wobble(k, 0.17f, 3.0f, 8f);
                    s4 = new Vector2(1f + squash * 0.6f, 1f - squash);
                    o4 = new Vector2(0f, Mathf.Max(0f, Wobble(k - 0.09f, 26f, 2.2f, 9f)));
                    r4 = Wobble(k, 1.6f, 1.7f, 5f);
                    if (!_evCrown) { _evCrown = true; onCrownLand.Invoke(); }
                }
            }

            // --- 5. final punch + idle
            float punch = Wobble(t - T_PUNCH, 0.07f, 1.9f, 5.5f);
            float idleW = Mathf.Clamp01(_idleT / 0.6f);
            float it = _idleT;
            float floatY = idleFloat * idleW * Mathf.Sin(2f * Mathf.PI * it / 2.6f);
            float breath = idleBreath * idleW * Mathf.Sin(2f * Mathf.PI * it / 1.3f);
            float idlePunch = 0f;
            if (_finished && idlePunchEvery > 0f)
            {
                while (_idleT > _nextPunch + 1.2f) _nextPunch += idlePunchEvery;
                idlePunch = Wobble(_idleT - _nextPunch, 0.09f, 2.4f, 6f);
            }
            s1 += new Vector2(punch * 0.6f + breath + idlePunch * 0.7f, punch * 0.6f + breath + idlePunch);
            r1 += idlePunch * 18f * Mathf.Sin(2f * Mathf.PI * 3f * Mathf.Max(0f, _idleT - _nextPunch));
            s2 += new Vector2(-breath * 0.6f, -breath * 0.6f);
            r3 += 0.6f * idleW * Mathf.Sin(2f * Mathf.PI * it / 3.2f);
            o4 += new Vector2(0f, 4f * idleW * Mathf.Sin(2f * Mathf.PI * (it + 0.4f) / 2.6f));
            r4 += 0.8f * idleW * Mathf.Sin(2f * Mathf.PI * (it + 0.9f) / 3.1f);

            // --- shake (sum of hits)
            float hit1 = t - (T_LINE1 + T_LINE1_FALL), hit2 = t - (T_LINE2 + 0.12f), hit3 = t - (T_CROWN + T_CROWN_FALL);
            float hitIdle = _finished && idlePunchEvery > 0f ? _idleT - _nextPunch : -1f;
            Vector2 shake = new Vector2(
                Shake(hit1, 18f, 0.3f) + Shake(hit2, 6f, 1.7f) + Shake(hit3, 9f, 2.9f) + Shake(hitIdle, 3f, 4.1f),
                Shake(hit1, 14f, 1.1f) + Shake(hit2, 5f, 2.3f) + Shake(hit3, 11f, 0.7f) + Shake(hitIdle, 2f, 5.3f)) * shakeStrength;

            Set(shaker, _shaker, shake + new Vector2(0f, floatY), Vector2.one * (1f + punch * 0.4f), 0f);
            Set(line1, _line1, Vector2.zero, s1, r1);
            Set(line2, _line2, o2, s2, r2);
            Set(banner, _banner, o3, s3, r3);
            Set(crown, _crown, o4, s4, r4);
            SetAlpha(_gLine1, al1); SetAlpha(_gLine2, al2); SetAlpha(_gBanner, al3); SetAlpha(_gCrown, al4);
            SetShadow(line1Shadow, _line1ShadowGraphic, h1);
            SetShadow(line2Shadow, _line2ShadowGraphic, 0f);
        }

        void SetShadow(RectTransform sh, Graphic graphic, float height)
        {
            if (sh == null) { return; }
            // the higher the letters "fly" above the screen, the further and fainter the shadow
            Vector2 position = shadowOffset * (1f + height * 2.2f);
            if (sh.anchoredPosition != position) { sh.anchoredPosition = position; }
            if (graphic == null) { return; }

            Color color = graphic.color;
            float alpha = shadowAlpha / (1f + height * 1.5f);
            if (color.a == alpha) { return; }
            color.a = alpha;
            graphic.color = color;
        }

        static void Set(RectTransform rt, Pose p, Vector2 offset, Vector2 scale, float rot)
        {
            if (rt == null) { return; }
            Vector2 position = p.pos + offset;
            Vector3 localScale = new Vector3(p.scale.x * scale.x, p.scale.y * scale.y, p.scale.z);
            Quaternion rotation = Quaternion.Euler(0f, 0f, p.rot + rot);
            if (rt.anchoredPosition != position) { rt.anchoredPosition = position; }
            if (rt.localScale != localScale) { rt.localScale = localScale; }
            if (!rt.localRotation.Equals(rotation)) { rt.localRotation = rotation; }
        }

        static void SetAlpha(CanvasGroup g, float a)
        {
            if (g != null && g.alpha != a) { g.alpha = a; }
        }
    }
}
