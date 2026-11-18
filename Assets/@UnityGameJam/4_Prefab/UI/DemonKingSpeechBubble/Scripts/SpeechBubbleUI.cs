using UnityEngine;
using UnityEngine.Events;

namespace DemonKing.UI
{
    /// <summary>
    /// Parchment speech bubble with a red wax name plate, shown above a talking character.
    /// The prefab root's pivot is the tip of the bubble's tail: put the root at the character's head
    /// (or set <see cref="followTarget"/>) and the bubble grows out of that point.
    /// Show() pops the paper out of the tail tip, then the name plate drops onto it with a bounce.
    /// Hide() shrinks it back into the tail. All motion is spring-driven in code (no Animator).
    /// Text is not part of this prefab: put the dialogue under <see cref="TextSlot"/> and the speaker's
    /// name under <see cref="NameSlot"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeechBubbleUI : MonoBehaviour
    {
        [Header("Parts (assigned by the builder; do not rename)")]
        [Tooltip("Everything that pops. Its pivot is the tail tip.")]
        public RectTransform bubble;
        public RectTransform paper;
        public RectTransform namePlate;
        [Tooltip("Put the speaker name text here.")]
        public RectTransform nameSlot;
        [Tooltip("Put the dialogue text here.")]
        public RectTransform textSlot;
        public CanvasGroup canvasGroup;

        [Header("Behaviour")]
        public bool startVisible = false;
        [Tooltip("Turn off for lines without a speaker name (narration, sounds).")]
        public bool showNamePlate = true;
        [Tooltip("Keeps animating while Time.timeScale = 0.")]
        public bool useUnscaledTime = false;
        [Tooltip("Hide automatically this many seconds after Show() (0 = stay until Hide()).")]
        public float autoHideAfter = 0f;

        [Header("Follow a character (Screen Space canvas)")]
        [Tooltip("Optional. The bubble's tail tip follows this transform's position + worldOffset on screen.")]
        public Transform followTarget;
        public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
        [Tooltip("Camera that sees the character. Empty = Camera.main.")]
        public Camera worldCamera;
        [Tooltip("Extra offset in canvas pixels after projecting to the screen.")]
        public Vector2 screenOffset = Vector2.zero;
        public bool hideWhenBehindCamera = true;

        [Header("World Space canvas")]
        [Tooltip("When the bubble sits on a World Space canvas, keep it facing the camera.")]
        public bool billboard = true;

        [Header("Pop in")]
        public float popFrequency = 3.0f;
        [Range(0.1f, 1.5f)] public float popDamping = 0.42f;
        [Tooltip("Starts tilted this much (degrees) and springs upright.")]
        public float popTilt = -9f;
        public float tiltFrequency = 2.4f;
        [Range(0.05f, 1.5f)] public float tiltDamping = 0.32f;
        [Tooltip("Stretch along the motion while growing fast (jelly).")]
        public float jelly = 0.035f;

        [Header("Pop out")]
        public float hideDuration = 0.18f;

        [Header("Name plate")]
        [Tooltip("The plate lands this long after the paper starts popping.")]
        public float nameDelay = 0.1f;
        [Tooltip("The plate drops from this many pixels above its place.")]
        public float nameDrop = 60f;
        public float nameFrequency = 3.4f;
        [Range(0.1f, 1.5f)] public float nameDamping = 0.36f;
        public float nameTilt = 7f;

        [Header("Idle")]
        [Tooltip("Gentle up-down float while shown (pixels). 0 = off.")]
        public float idleBob = 4f;
        public float idleBobSpeed = 0.9f;

        [Header("Bump (Show() while already shown = next line)")]
        public float bumpScale = 0.08f;
        public float bumpFrequency = 4.5f;
        [Range(0.05f, 1.5f)] public float bumpDamping = 0.3f;

        [Header("Events")]
        public UnityEvent onShowStarted = new UnityEvent();
        public UnityEvent onShown = new UnityEvent();
        public UnityEvent onHideStarted = new UnityEvent();
        public UnityEvent onHidden = new UnityEvent();

        /// <summary>Set true to drive the animation yourself with Advance(dt) (used by the preview capture).</summary>
        [System.NonSerialized] public bool manualAdvance;

        bool visible, hiding, shownFired = true, hiddenFired = true, inited;
        float stateTime, autoHideTimer;
        float scale, scaleVel, tilt, tiltVel, bump, bumpVel;
        float plateY, plateYVel, plateS, plateSVel, plateTilt, plateTiltVel;
        float hideFrom, bobTime, bobAmp;
        Vector2 plateHome;
        Canvas rootCanvas;

        public bool IsVisible { get { return visible; } }
        /// <summary>Parent for the dialogue text.</summary>
        public RectTransform TextSlot { get { return textSlot; } }
        /// <summary>Parent for the speaker name text.</summary>
        public RectTransform NameSlot { get { return nameSlot; } }

        // ------------------------------------------------------------------ public API
        /// <summary>Pops the bubble in. If it is already shown, gives it a little bump instead (use for the next line).</summary>
        public void Show()
        {
            Init();
            autoHideTimer = autoHideAfter;
            if (visible && !hiding) { Bump(); return; }
            bool fromHidden = !visible || scale < 0.05f;
            visible = true; hiding = false; stateTime = 0f; shownFired = false; hiddenFired = true;
            if (fromHidden)
            {
                scale = 0f; scaleVel = 0f; tilt = popTilt; tiltVel = 0f;
                plateS = 0f; plateSVel = 0f; plateY = nameDrop; plateYVel = 0f; plateTilt = nameTilt; plateTiltVel = 0f;
                bobTime = 0f; bobAmp = 0f;
            }
            ApplyNamePlateVisibility();
            onShowStarted.Invoke();
        }

        /// <summary>Show() and hide again after the given seconds.</summary>
        public void ShowFor(float seconds)
        {
            Show();
            autoHideTimer = Mathf.Max(0.01f, seconds);
        }

        public void Hide()
        {
            Init();
            if (!visible || hiding) return;
            hiding = true; stateTime = 0f; hideFrom = Mathf.Max(scale, 0.01f); hiddenFired = false; autoHideTimer = 0f;
            onHideStarted.Invoke();
        }

        /// <summary>Small squash-and-stretch kick, e.g. when the next line of dialogue appears.</summary>
        public void Bump()
        {
            bumpVel += bumpScale * bumpFrequency * 2f * Mathf.PI;
        }

        public void SetShowNamePlate(bool show)
        {
            showNamePlate = show;
            ApplyNamePlateVisibility();
        }

        /// <summary>Jump straight to shown / hidden without animation.</summary>
        public void SetVisibleImmediate(bool show)
        {
            Init();
            visible = show; hiding = false; stateTime = 10f; shownFired = hiddenFired = true; autoHideTimer = 0f;
            scale = show ? 1f : 0f; scaleVel = 0f; tilt = 0f; tiltVel = 0f; bump = 0f; bumpVel = 0f;
            plateS = show ? 1f : 0f; plateSVel = 0f; plateY = 0f; plateYVel = 0f; plateTilt = 0f; plateTiltVel = 0f;
            bobTime = 0f; bobAmp = show ? 1f : 0f;
            ApplyNamePlateVisibility();
            ApplyPose(1f);
        }

        /// <summary>Steps the animation by dt seconds (only needed when manualAdvance is true).</summary>
        public void Advance(float dt) { Step(dt); }

        // ------------------------------------------------------------------ Unity
        void Awake()
        {
            Init();
            SetVisibleImmediate(startVisible);
        }

        void Update()
        {
            if (manualAdvance) return;
            Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        void LateUpdate()
        {
            FollowAndFace();
        }

        void Init()
        {
            if (inited) return;
            inited = true;
            if (namePlate != null) plateHome = namePlate.anchoredPosition;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        void ApplyNamePlateVisibility()
        {
            if (namePlate != null && namePlate.gameObject.activeSelf != showNamePlate) namePlate.gameObject.SetActive(showNamePlate);
        }

        // ------------------------------------------------------------------ animation
        static void Spring(ref float x, ref float v, float target, float freq, float damp, float dt)
        {
            // semi-implicit damped spring, sub-stepped for stability
            float w = 2f * Mathf.PI * freq;
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                v += (-w * w * (x - target) - 2f * damp * w * v) * h;
                x += v * h;
            }
        }

        static float EaseInBack(float t)
        {
            const float c = 1.6f;
            return t * t * ((c + 1f) * t - c);
        }

        void Step(float dt)
        {
            if (dt <= 0f) return;
            Init();
            stateTime += dt;
            float alpha = 1f;

            if (visible && !hiding)
            {
                Spring(ref scale, ref scaleVel, 1f, popFrequency, popDamping, dt);
                Spring(ref tilt, ref tiltVel, 0f, tiltFrequency, tiltDamping, dt);
                if (stateTime >= nameDelay)
                {
                    Spring(ref plateS, ref plateSVel, 1f, nameFrequency, nameDamping, dt);
                    Spring(ref plateY, ref plateYVel, 0f, nameFrequency, nameDamping, dt);
                    Spring(ref plateTilt, ref plateTiltVel, 0f, nameFrequency * 0.8f, nameDamping, dt);
                }
                bobAmp = Mathf.MoveTowards(bobAmp, 1f, dt * 1.5f);
                bobTime += dt;
                if (!shownFired && stateTime > 0.6f) { shownFired = true; onShown.Invoke(); }
                if (autoHideTimer > 0f) { autoHideTimer -= dt; if (autoHideTimer <= 0f) Hide(); }
            }
            else if (hiding)
            {
                float t = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, hideDuration));
                scale = Mathf.Max(0f, hideFrom * (1f - EaseInBack(t)));
                scaleVel = 0f;
                tilt = Mathf.Lerp(0f, popTilt * 0.6f, t);
                alpha = 1f - t * t;
                if (t >= 1f)
                {
                    visible = false; hiding = false; scale = 0f; plateS = 0f; bobAmp = 0f;
                    if (!hiddenFired) { hiddenFired = true; onHidden.Invoke(); }
                }
            }
            else alpha = 0f;

            Spring(ref bump, ref bumpVel, 0f, bumpFrequency, bumpDamping, dt);
            ApplyPose(alpha);
        }

        void ApplyPose(float alpha)
        {
            if (bubble != null)
            {
                float s = Mathf.Max(0f, scale);
                // stretch upward while growing fast, squash a little when it overshoots back
                float j = Mathf.Clamp(scaleVel * jelly, -0.12f, 0.12f);
                float b = bump;
                bubble.localScale = new Vector3(s * (1f - j * 0.6f + b), s * (1f + j - b * 0.6f), 1f);
                bubble.localEulerAngles = new Vector3(0f, 0f, tilt);
                float bob = idleBob * bobAmp * Mathf.Sin(bobTime * idleBobSpeed * 2f * Mathf.PI);
                bubble.anchoredPosition = new Vector2(0f, bob);
            }
            if (namePlate != null)
            {
                float ps = Mathf.Max(0f, plateS);
                namePlate.localScale = new Vector3(ps, ps, 1f);
                namePlate.anchoredPosition = plateHome + new Vector2(0f, plateY);
                namePlate.localEulerAngles = new Vector3(0f, 0f, plateTilt);
            }
            if (canvasGroup != null) canvasGroup.alpha = visible || hiding ? alpha : 0f;
        }

        // ------------------------------------------------------------------ follow / billboard
        void FollowAndFace()
        {
            if (rootCanvas == null)
            {
                var c = GetComponentInParent<Canvas>();
                if (c == null) return;
                rootCanvas = c.rootCanvas;
            }
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            if (rootCanvas.renderMode == RenderMode.WorldSpace)
            {
                if (billboard) transform.rotation = cam.transform.rotation;
                return;
            }
            if (followTarget == null) return;
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            Vector3 sp = cam.WorldToScreenPoint(followTarget.position + worldOffset);
            if (hideWhenBehindCamera && sp.z < 0f)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                return;
            }
            Camera uiCam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, uiCam, out local))
                rt.localPosition = new Vector3(local.x + screenOffset.x, local.y + screenOffset.y, 0f);
        }

        // ------------------------------------------------------------------ editor helpers
        [ContextMenu("Preview: Shown pose")]
        void PreviewShown() { inited = false; Init(); SetVisibleImmediate(true); }

        [ContextMenu("Preview: Hidden pose")]
        void PreviewHidden() { inited = false; Init(); SetVisibleImmediate(false); }
    }
}
