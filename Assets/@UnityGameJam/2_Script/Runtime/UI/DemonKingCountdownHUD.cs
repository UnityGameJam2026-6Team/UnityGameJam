using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonKing.UI
{
    /// <summary>
    /// "마왕 도착까지" countdown HUD: layered frame (plate, wings, horns, crest), New Rocker digit timer and the Demon King skull.
    ///
    /// Default: runs its own 3:00 timer (autoRun). To drive it from the game instead, turn autoRun off and call
    ///     hud.StartRound()                        // intro drop + reset triggers
    ///     hud.NotifyRemaining(remainingSeconds);  // every frame
    ///
    /// Phases (by remaining time):
    ///   3:00~2:01  wings flap only
    ///   2:00~1:01  the frame floats (bobbing)
    ///   1:00       Demon King skull comes down and hurries the player
    ///   last minute, every 10 s (50, 40, 30, 20, 10): the whole frame rocks like a seesaw twice
    ///              (both ends go up/down, nothing detaches), wings beat, digits punch
    ///   last 3 s   no shaking, heartbeat pulse every second
    ///   0:00       the skull arrives and stays
    /// </summary>
    [DisallowMultipleComponent]
    public class DemonKingCountdownHUD : MonoBehaviour
    {
        public enum Fx { None, Intro, Urge, TimeUp }

        [Header("Timer")]
        [Tooltip("Count down by itself from Round Seconds when enabled (turn off to drive it with NotifyRemaining).")]
        public bool autoRun = true;
        public float roundSeconds = 180f;
        public bool playIntroOnEnable = true;
        public bool useUnscaledTime = false;

        [Header("Phases")]
        public int floatBelowSeconds = 120;
        public int urgeAtSeconds = 60;
        public int shakeEverySeconds = 10;
        [Tooltip("Last-minute shake: how far the whole frame tilts (degrees) when it rocks.")]
        public float shakeAngle = 3.5f;
        public int heartbeatFromSeconds = 3;
        public float flapAngle = 9f;
        public float flapSpeed = 7.5f;
        public float floatHeight = 9f;
        public float floatSpeed = 1.7f;

        [Header("Intro - drops from above into the HUD slot")]
        public float introDropHeight = 520f;

        [Header("Urge - skull comes down, frame is dragged to the screen and hurries the player")]
        public float urgeDuration = 3.4f;
        public float urgeFrameScale = 1.3f;
        [Tooltip("Frame position during the urge, relative to the screen center (canvas units).")]
        public Vector2 urgeFramePosition = new Vector2(0f, -185f);
        [Tooltip("Skull position during the urge, relative to the screen center (canvas units).")]
        public Vector2 urgeSkullPosition = new Vector2(0f, 190f);
        [Range(0f, 1f)] public float urgeDim = 0.6f;

        [Header("Events")]
        public UnityEvent onIntroFinished = new UnityEvent();
        public UnityEvent onUrge = new UnityEvent();
        public IntEvent onShake = new IntEvent();
        public IntEvent onHeartbeat = new IntEvent();
        public UnityEvent onTimeUp = new UnityEvent();
        [System.Serializable] public class IntEvent : UnityEvent<int> { }

        [Header("Prefab references")]
        public RectTransform hud;
        public RectTransform content;
        public RectTransform plate;
        public RectTransform wingL, wingR, hornL, hornR, crest;
        public RectTransform textSlot;
        public CanvasGroup textGroup;
        public RectTransform timerRoot;
        public Image[] digitImages = new Image[4];   // M M : S S  (red digits with purple drips)
        public Image colonImage;
        public Sprite[] digitSprites = new Sprite[10];
        [Tooltip("Purple liquid coat on top of the red digits. It drains downward as time runs out, revealing the red.")]
        public Image[] digitCoatImages = new Image[4];
        public Image colonCoatImage;
        public Sprite[] digitCoatSprites = new Sprite[10];
        public Material coatMaterial;
        [Tooltip("Remaining seconds when the purple starts to drain.")]
        public float drainStartSeconds = 180f;
        [Tooltip("Remaining seconds when the purple has drained down to the drips.")]
        public float drainEndSeconds = 0f;
        [Tooltip("Liquid level (sprite UV, 0 bottom - 1 top) when full / when drained.")]
        public float coatFullLevel = 0.9f, coatDrainedLevel = 0.36f;
        public Image dim;
        public RectTransform skull;
        public RectTransform skullJaw;
        [Tooltip("How far (canvas units) the jaw travels up when it snaps shut.")]
        public float jawTravel = 24f;

        // ---------------------------------------------------------------- state
        struct Rest { public Vector2 pos; public Vector3 scale; public float rot; }
        Rest rWingL, rWingR, rHornL, rHornR, rCrest, rPlate, rJaw, rSkull, rTimer;
        bool inited;
        Fx fx = Fx.None;
        float fxTime = -1f, shakeTime = -1f, beatTime = -1f, clock;
        int lastShown = int.MinValue, shownNow = -1;
        bool timeUpFired, hidden, running;
        float remaining;
        readonly int[] shownDigits = { -1, -1, -1, -1 };
        readonly float[] digitPunch = { -1f, -1f, -1f, -1f };

        public Fx CurrentFx { get { return fx; } }
        public float Remaining { get { return remaining; } }
        public bool IsRunning { get { return running; } }

        // ---------------------------------------------------------------- API
        /// <summary>Intro drop + (re)start the built-in timer from roundSeconds when autoRun is on.</summary>
        public void StartRound()
        {
            Init();
            ResetAutoTriggers();
            remaining = roundSeconds;
            running = autoRun;
            NotifyRemaining(remaining);
            PlayIntro();
        }

        /// <summary>Jump the built-in timer to a time without firing effects for the skipped part.</summary>
        public void JumpTo(float seconds)
        {
            Init();
            ResetAutoTriggers();
            remaining = Mathf.Max(0f, seconds);
            running = autoRun && remaining > 0f;
            NotifyRemaining(remaining);
        }

        public void PauseTimer() { running = false; }
        public void ResumeTimer() { running = autoRun && remaining > 0f; }

        public void PlayIntro() { Begin(Fx.Intro); }
        public void PlayUrge() { Begin(Fx.Urge); }
        public void PlayTimeUp() { Begin(Fx.TimeUp); }
        public void PlayShake() { Init(); shakeTime = 0f; Punch(); }
        public void PlayHeartbeat() { Init(); beatTime = 0f; }

        /// <summary>Feed the remaining time (seconds). Updates the digits and fires the phase effects.</summary>
        public void NotifyRemaining(float remainingSeconds)
        {
            Init();
            remaining = Mathf.Max(0f, remainingSeconds);
            int shown = Mathf.Max(0, Mathf.CeilToInt(remaining));
            shownNow = shown;
            SetDigits(shown);

            if (lastShown != int.MinValue && shown < lastShown)
            {
                for (int s = lastShown - 1; s >= shown; s--)   // handle every crossed second once
                {
                    if (s == urgeAtSeconds) { PlayUrge(); onUrge.Invoke(); }
                    else if (s < urgeAtSeconds && s > heartbeatFromSeconds && shakeEverySeconds > 0 && s % shakeEverySeconds == 0) { PlayShake(); onShake.Invoke(s); }
                    if (s <= heartbeatFromSeconds && s > 0) { PlayHeartbeat(); onHeartbeat.Invoke(s); }
                    if (s == 0 && !timeUpFired) { timeUpFired = true; PlayTimeUp(); onTimeUp.Invoke(); }
                }
            }
            if (shown > 0) timeUpFired = false;
            lastShown = shown;
        }

        /// <summary>Forget the last reported time (round restart / timer jump) so no effects fire for the jump.</summary>
        public void ResetAutoTriggers() { lastShown = int.MinValue; timeUpFired = false; shakeTime = -1f; beatTime = -1f; }

        public void ShowImmediate() { Init(); hidden = false; fx = Fx.None; fxTime = -1f; Evaluate(); }
        public void HideImmediate() { Init(); hidden = true; fx = Fx.None; fxTime = -1f; Evaluate(); }

        public void Advance(float dt)
        {
            Init();
            clock += dt;
            if (running)
            {
                float r = Mathf.Max(0f, remaining - dt);
                NotifyRemaining(r);
                if (r <= 0f) running = false;
            }
            if (fxTime >= 0f) fxTime += dt;
            if (shakeTime >= 0f) { shakeTime += dt; if (shakeTime > 0.8f) shakeTime = -1f; }
            if (beatTime >= 0f) { beatTime += dt; if (beatTime > 0.9f) beatTime = -1f; }
            for (int i = 0; i < 4; i++) if (digitPunch[i] >= 0f) { digitPunch[i] += dt; if (digitPunch[i] > 0.4f) digitPunch[i] = -1f; }
            Evaluate();
        }

        // ---------------------------------------------------------------- lifecycle
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Init();
            if (autoRun) StartRound();
            else if (playIntroOnEnable) PlayIntro();
            else ShowImmediate();
        }

        void Update() { Advance(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }

        void Init()
        {
            if (inited) return;
            inited = true;
            rWingL = R(wingL); rWingR = R(wingR); rHornL = R(hornL); rHornR = R(hornR); rCrest = R(crest);
            rPlate = R(plate); rJaw = R(skullJaw); rSkull = R(skull); rTimer = R(timerRoot);
            if (rSkull.scale == Vector3.zero) rSkull.scale = Vector3.one;
            if (rTimer.scale == Vector3.zero) rTimer.scale = Vector3.one;
            if (coatMaterial != null)
            {
                coatInstance = new Material(coatMaterial) { name = coatMaterial.name + " (Instance)" };
                if (digitCoatImages != null) foreach (var img in digitCoatImages) if (img != null) img.material = coatInstance;
                if (colonCoatImage != null) colonCoatImage.material = coatInstance;
            }
        }

        Material coatInstance;
        static readonly int ID_Level = Shader.PropertyToID("_Level");
        static readonly int ID_Clock = Shader.PropertyToID("_T");

        void OnDestroy()
        {
            if (coatInstance == null) return;
            if (Application.isPlaying) Destroy(coatInstance); else DestroyImmediate(coatInstance);
        }

        static Rest R(RectTransform t)
        {
            var r = new Rest();
            if (t == null) return r;
            r.pos = t.anchoredPosition; r.scale = t.localScale; r.rot = t.localEulerAngles.z;
            if (r.rot > 180f) r.rot -= 360f;
            return r;
        }

        void Begin(Fx kind)
        {
            Init();
            hidden = false;
            fx = kind;
            fxTime = 0f;
            Evaluate();
        }

        void SetDigits(int shown)
        {
            if (digitImages == null || digitSprites == null || digitSprites.Length < 10) return;
            int m = Mathf.Min(99, shown / 60), s = shown % 60;
            int[] d = { m / 10, m % 10, s / 10, s % 10 };
            for (int i = 0; i < 4 && i < digitImages.Length; i++)
            {
                if (digitImages[i] == null || shownDigits[i] == d[i]) continue;
                if (shownDigits[i] >= 0) digitPunch[i] = 0f;
                shownDigits[i] = d[i];
                digitImages[i].sprite = digitSprites[d[i]];
                if (digitCoatImages != null && i < digitCoatImages.Length && digitCoatImages[i] != null && digitCoatSprites != null && digitCoatSprites.Length >= 10)
                    digitCoatImages[i].sprite = digitCoatSprites[d[i]];
            }
        }

        void Punch() { for (int i = 0; i < 4; i++) digitPunch[i] = 0f; }

        // ---------------------------------------------------------------- evaluation
        void Evaluate()
        {
            if (content == null) return;
            float t = fxTime;
            int shown = shownNow < 0 ? Mathf.CeilToInt(roundSeconds) : shownNow;

            Vector2 cPos = Vector2.zero; float cScale = 1f, cRot = 0f;
            float wing = 0f, hornS = 1f, crestY = 0f, squash = 0f;
            float dimA = 0f, textA = hidden ? 0f : 1f, timerScale = 1f;
            Color digitTint = Color.white;
            bool skullOn = false; Vector2 skullPos = Vector2.zero; float skullRot = 0f, jawClose = 0f, skullScale = 1f;

            content.gameObject.SetActive(!hidden);

            // ---- phase idle
            if (shown > floatBelowSeconds)
            {
                wing += flapAngle * Mathf.Sin(clock * flapSpeed);                     // 3:00 wings only
            }
            else if (shown > urgeAtSeconds)
            {
                cPos.y += floatHeight * Mathf.Sin(clock * floatSpeed);               // 2:xx floating
                cRot += 1.2f * Mathf.Sin(clock * floatSpeed * 0.5f + 0.7f);
                wing += flapAngle * 0.45f * Mathf.Sin(clock * flapSpeed * 0.6f);
            }
            else
            {
                wing += flapAngle * 0.3f * Mathf.Sin(clock * flapSpeed * 0.8f);      // last minute: tense idle
            }

            Vector2 screenCenter = ScreenCenterInHud();
            Vector2 offTop = new Vector2(0f, CanvasHalfHeight() + 420f);

            if (fx == Fx.Intro && t >= 0f)
            {
                cPos.y += introDropHeight * (1f - Spring(t, 2.1f, 0.42f));
                squash = 0.09f * Impulse(t - 0.2f, 4.5f, 0.32f);
                wing = Mathf.Lerp(70f, wing, Spring(t - 0.14f, 2.4f, 0.38f));
                hornS = Spring(t - 0.22f, 3.2f, 0.38f);
                crestY = 90f * (1f - Spring(t - 0.26f, 3f, 0.34f));
                textA = Range01(t, 0.35f, 0.6f);
                if (t > 1.8f) End(onIntroFinished);
            }
            else if (fx == Fx.Urge && t >= 0f)
            {
                float D = urgeDuration;
                float k = Spring(t - 0.08f, 1.9f, 0.68f) * (1f - Spring(t - (D - 0.75f), 1.9f, 0.7f));
                cPos = Vector2.LerpUnclamped(cPos, screenCenter + urgeFramePosition, k);
                cScale = Mathf.LerpUnclamped(1f, urgeFrameScale, k);
                dimA = urgeDim * Mathf.Clamp01(Range01(t, 0f, 0.25f) - Range01(t, D - 0.5f, D));
                skullOn = true;
                skullPos = Vector2.LerpUnclamped(offTop, urgeSkullPosition, Spring(t, 1.6f, 0.5f));
                skullPos = Vector2.LerpUnclamped(skullPos, offTop, Spring(t - (D - 0.85f), 1.6f, 0.75f));
                float[] knocks = { 0.75f, 0.97f, 1.19f, 1.75f, 1.97f, 2.19f };
                float knock = 0f, chatter = 0f;
                foreach (float kt in knocks) { knock += Impulse(t - kt, 4.6f, 0.32f); chatter = Mathf.Max(chatter, Bump(t, kt - 0.04f, 0.16f)); }
                cPos.y -= 30f * knock * k;
                squash += 0.06f * knock;
                skullRot = 3f * Mathf.Sin(t * 26f) * Mathf.Clamp01(chatter);
                jawClose = chatter;
                skullPos.y -= 10f * Mathf.Max(0f, knock);
                wing += 16f * Mathf.Sin(t * 30f) * Mathf.Clamp01(Range01(t, 0.6f, 0.8f) - Range01(t, 2.3f, 2.6f));
                hornS = 1f + 0.18f * Mathf.Max(0f, knock);
                if (t > D + 0.4f) End(null);
            }
            else if (fx == Fx.TimeUp && t >= 0f)
            {
                float k = Spring(t - 0.05f, 1.9f, 0.68f);
                cPos = Vector2.LerpUnclamped(cPos, screenCenter + urgeFramePosition, k);
                cScale = Mathf.LerpUnclamped(1f, urgeFrameScale, k);
                dimA = urgeDim * Range01(t, 0f, 0.3f);
                skullOn = true;
                skullPos = Vector2.LerpUnclamped(offTop, urgeSkullPosition, Spring(t, 1.5f, 0.38f));
                float slam = Impulse(t - 0.32f, 3.8f, 0.3f);
                skullPos.y -= 14f * slam;
                skullScale = 1f + 0.03f * slam;
                jawClose = 0.5f + 0.5f * Mathf.Sin(t * 9f) * Range01(t, 0.4f, 0.6f);
                cPos.y -= 20f * Impulse(t - 0.35f, 4f, 0.3f);
                wing += 20f * Impulse(t - 0.35f, 2.2f, 0.25f);
                digitTint = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.6f), 0.5f + 0.5f * Mathf.Sin(t * 6f));
            }

            // ---- every 10 s in the last minute: the whole frame rocks like a seesaw twice, quickly
            //      (plate, horns, crown and wings move as one piece, so both ends go up/down together with the plate)
            if (shakeTime >= 0f)
            {
                float st = shakeTime;
                float env = st < 0.42f ? Mathf.Sin(Mathf.PI * st / 0.42f) : 0f;
                float wave = Mathf.Sin(st * 2f * Mathf.PI * 4.8f);        // two full up/down cycles in ~0.42 s
                cRot += shakeAngle * wave * env;
                cPos.y += 5f * env;                                        // small lift so it reads as a jolt
                cScale *= 1f + 0.04f * env;
                wing += 18f * wave * env;
                timerScale *= 1f + 0.16f * Impulse(st, 3.2f, 0.35f);
                digitTint = Color.Lerp(digitTint, new Color(1f, 0.62f, 0.68f), env);
            }

            // ---- last seconds: heartbeat (lub-dub), no shaking
            if (beatTime >= 0f)
            {
                float b = Beat(beatTime, 0f, 0.16f) + 0.6f * Beat(beatTime, 0.2f, 0.16f);
                cScale *= 1f + 0.07f * b;
                timerScale *= 1f + 0.22f * b;
                digitTint = Color.Lerp(digitTint, new Color(1f, 0.45f, 0.5f), Mathf.Clamp01(b));
            }

            // ---- apply
            content.anchoredPosition = cPos;
            content.localScale = new Vector3(cScale, cScale, 1f);
            content.localEulerAngles = new Vector3(0, 0, cRot);

            Apply(plate, rPlate, Vector2.zero, 0f, new Vector2(1f + squash * 0.6f, 1f - squash));
            Apply(wingL, rWingL, Vector2.zero, wing, Vector2.one);
            Apply(wingR, rWingR, Vector2.zero, -wing, Vector2.one);
            Apply(hornL, rHornL, Vector2.zero, 0f, new Vector2(hornS, hornS));
            Apply(hornR, rHornR, Vector2.zero, 0f, new Vector2(hornS, hornS));
            Apply(crest, rCrest, new Vector2(0f, crestY), 0f, Vector2.one);
            if (textGroup != null) textGroup.alpha = textA;

            if (timerRoot != null)
            {
                timerRoot.localScale = new Vector3(rTimer.scale.x * timerScale, rTimer.scale.y * timerScale, 1f);
                // purple liquid drains downward as the Demon King gets closer, revealing the red digits
                float span = Mathf.Max(0.01f, drainStartSeconds - drainEndSeconds);
                float drain = Mathf.Clamp01((drainStartSeconds - remaining) / span);
                if (fx == Fx.TimeUp) drain = 1f;
                if (coatInstance != null)
                {
                    coatInstance.SetFloat(ID_Level, Mathf.Lerp(coatFullLevel, coatDrainedLevel, drain));
                    coatInstance.SetFloat(ID_Clock, clock);
                }
                for (int i = 0; digitImages != null && i < digitImages.Length; i++)
                {
                    var img = digitImages[i];
                    if (img == null) continue;
                    float p = digitPunch[i] >= 0f ? 1f + 0.14f * (1f - Ease(Range01(digitPunch[i], 0f, 0.3f))) : 1f;
                    img.rectTransform.localScale = new Vector3(p, p, 1f);
                    img.color = digitTint;
                    if (digitCoatImages != null && i < digitCoatImages.Length && digitCoatImages[i] != null) digitCoatImages[i].color = digitTint;
                }
                if (colonImage != null)
                {
                    float blink = 0.75f + 0.25f * Mathf.Cos((remaining % 1f) * Mathf.PI * 2f);
                    var c = digitTint; c.a = blink; colonImage.color = c;
                    if (colonCoatImage != null) colonCoatImage.color = c;
                }
            }

            if (dim != null) { Color c = dim.color; c.a = dimA; dim.color = c; dim.enabled = dimA > 0.001f; }
            if (skull != null)
            {
                skull.gameObject.SetActive(skullOn && !hidden);
                skull.anchoredPosition = skullPos;
                skull.localEulerAngles = new Vector3(0, 0, skullRot);
                skull.localScale = new Vector3(rSkull.scale.x * skullScale, rSkull.scale.y * skullScale, 1f);
                if (skullJaw != null) skullJaw.anchoredPosition = rJaw.pos + new Vector2(0f, jawTravel * Mathf.Clamp01(jawClose));
            }
        }

        void End(UnityEvent e) { fx = Fx.None; fxTime = -1f; if (e != null) e.Invoke(); }

        static void Apply(RectTransform t, Rest r, Vector2 dPos, float dRot, Vector2 sMul)
        {
            if (t == null) return;
            t.anchoredPosition = r.pos + dPos;
            t.localEulerAngles = new Vector3(0, 0, r.rot + dRot);
            t.localScale = new Vector3(r.scale.x * sMul.x, r.scale.y * sMul.y, 1f);
        }

        Vector2 ScreenCenterInHud()
        {
            var root = transform as RectTransform;
            if (root == null || hud == null) return Vector2.zero;
            Vector3 local = hud.InverseTransformPoint(root.TransformPoint(root.rect.center));
            return new Vector2(local.x, local.y);
        }

        float CanvasHalfHeight()
        {
            var root = transform as RectTransform;
            return root != null ? root.rect.height * 0.5f : 540f;
        }

        // ---------------------------------------------------------------- motion helpers
        static float Spring(float t, float freq, float damping)
        {
            if (t <= 0f) return 0f;
            float w = 2f * Mathf.PI * freq, z = Mathf.Clamp(damping, 0.01f, 0.99f);
            float wd = w * Mathf.Sqrt(1f - z * z);
            return 1f - Mathf.Exp(-z * w * t) * (Mathf.Cos(wd * t) + (z * w / wd) * Mathf.Sin(wd * t));
        }

        static float Impulse(float t, float freq, float damping)
        {
            if (t <= 0f) return 0f;
            float w = 2f * Mathf.PI * freq, z = Mathf.Clamp(damping, 0.01f, 0.99f);
            float wd = w * Mathf.Sqrt(1f - z * z);
            return Mathf.Exp(-z * w * t) * Mathf.Sin(wd * t) * 1.25f;
        }

        /// <summary>Fast-rise / slower-fall pulse used for the heartbeat.</summary>
        static float Beat(float t, float start, float dur)
        {
            float x = (t - start) / dur;
            if (x <= 0f || x >= 2.2f) return 0f;
            return x < 0.35f ? x / 0.35f : Mathf.Exp(-(x - 0.35f) * 2.4f);
        }

        static float Bump(float t, float start, float dur)
        {
            if (t < start || t > start + dur) return 0f;
            return Mathf.Sin(Mathf.PI * (t - start) / dur);
        }

        static float Ease(float x) { x = 1f - x; return 1f - x * x * x; }
        static float Range01(float t, float a, float b) { return b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a)); }
    }
}
