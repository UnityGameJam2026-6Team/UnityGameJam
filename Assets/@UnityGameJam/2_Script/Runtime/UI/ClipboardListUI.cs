using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemonKing.UI
{
    /// <summary>
    /// Clipboard to-do list: a clipboard peeks out of the right screen edge with three sheets clipped on it.
    /// Open() slides it in and pulls the sheets off the board toward the left (they grow while coming out),
    /// leaving the board empty: the to-do sheet in front, two decorative sheets behind it with only their
    /// corners showing. Close() puts the sheets back on the board and slides it away.
    /// All motion is spring-driven in code (no Animator). Text is not part of this prefab:
    /// put the to-do list under <see cref="TodoTextSlot"/>.
    /// </summary>
    public class ClipboardListUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Parts (assigned by the builder; do not rename)")]
        public RectTransform clipboard;
        public RectTransform clip;
        [Tooltip("The front sheet that carries the to-do list.")]
        public ClipboardSheet todoSheet;
        [Tooltip("Decorative sheets behind the to-do sheet (only their corners show).")]
        public ClipboardSheet[] backSheets = new ClipboardSheet[0];
        public Button toggleButton;

        [Header("Behaviour")]
        public bool startOpen = false;
        [Tooltip("Clicking the clipboard opens / closes the list.")]
        public bool toggleOnClipboardClick = true;
        [Tooltip("Keeps animating while Time.timeScale = 0 (pause menus).")]
        public bool useUnscaledTime = true;

        [Header("Clipboard slide (X is measured from the right screen edge)")]
        [Tooltip("Pixels of the clipboard left visible at the right edge while closed.")]
        public float closedPeek = 86f;
        [Tooltip("Gap between the clipboard and the right edge while open.")]
        public float openRightMargin = 30f;
        [Tooltip("Extra pixels the closed clipboard slides out while the pointer is over it.")]
        public float peekHoverOut = 34f;
        public float slideFrequency = 2.4f;
        [Range(0.1f, 1.5f)] public float slideDamping = 0.55f;
        [Tooltip("Tilt (degrees) per pixel/second of slide speed. Makes the board lean into the motion.")]
        public float leanPerSpeed = 0.0025f;
        public float maxLean = 7f;

        [Header("Sheets on the board (closed) - local to the clipboard centre")]
        [Tooltip("Centre of the paper clipped on the board.")]
        public Vector2 sheetHome = new Vector2(0f, -14f);
        [Tooltip("Size of the sheets while clipped on the board (1 = full size when pulled out).")]
        public float sheetHomeScale = 0.78f;
        [Tooltip("Back sheets peek out under the top sheet like a stack while on the board.")]
        public Vector2[] backHomeOffsets = { new Vector2(8f, -8f), new Vector2(15f, -15f) };
        public float[] backHomeAngles = { -1.2f, 0.8f };

        [Header("To-do sheet pulled out (local to the clipboard centre)")]
        [Tooltip("The to-do sheet ends this far left of the paper's place on the board.")]
        public float sheetSlide = 560f;
        [Tooltip("... and this much down (negative = up).")]
        public float sheetDrop = -10f;
        [Tooltip("Tilt (CCW) of the to-do sheet when open.")]
        public float todoAngle = 2f;
        [Tooltip("Sheets lift a little while sliding out (arc).")]
        public float dealArc = 36f;

        [Header("Back sheets: offset / extra tilt relative to the to-do sheet")]
        public Vector2[] backOffsets = { new Vector2(-22f, -14f), new Vector2(-40f, -32f) };
        public float[] backAngles = { 5f, 10f };

        [Header("Sheet timing")]
        public float sheetsDelay = 0.34f;
        public float sheetStagger = 0.07f;
        public float closeStagger = 0.04f;
        [Tooltip("While closing, the clipboard waits this long after the last sheet starts going back.")]
        public float closeClipboardDelay = 0.22f;
        public float sheetFrequency = 2.8f;
        [Range(0.1f, 1.5f)] public float sheetDamping = 0.52f;
        [Tooltip("Screen-space offset of each sheet's flat drop shadow.")]
        public Vector2 sheetShadowOffset = new Vector2(12f, -16f);

        [Header("Skull pendant (swings around its ring)")]
        public float clipKickAngle = 9f;
        public float clipSquash = 0.05f;
        public float clipSwingFrequency = 1.7f;
        [Range(0.05f, 1f)] public float clipSwingDamping = 0.2f;
        [Tooltip("How much the pendant swings from the clipboard's own acceleration.")]
        public float clipInertia = 0.028f;
        public float clipMaxAngle = 28f;

        [Header("Events")]
        public UnityEvent onOpenStarted = new UnityEvent();
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onCloseStarted = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        /// <summary>Set true to drive the animation yourself with Advance(dt) (used by the preview capture).</summary>
        [System.NonSerialized] public bool manualAdvance;

        bool isOpen, settled = true, pointerOverBoard, landedKick;
        float stateTime;
        float slide, slideVel;
        float peek, peekVel;
        float clipAng, clipAngVel, clipPop, clipPopVel;

        public bool IsOpen { get { return isOpen; } }
        /// <summary>Put the to-do list text here.</summary>
        public RectTransform TodoTextSlot { get { return todoSheet != null ? todoSheet.textSlot : null; } }

        // sheet order: 0 = to-do sheet (front), then the back sheets
        int SheetCount { get { return (todoSheet != null ? 1 : 0) + (backSheets != null ? backSheets.Length : 0); } }
        ClipboardSheet SheetAt(int i) { return i == 0 ? todoSheet : backSheets[i - 1]; }

        // ------------------------------------------------------------------ public API
        public void Open()
        {
            if (isOpen) return;
            isOpen = true; stateTime = 0f; settled = false; landedKick = false;
            onOpenStarted.Invoke();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false; stateTime = 0f; settled = false;
            onCloseStarted.Invoke();
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }

        /// <summary>Jumps to the open / closed pose without animation.</summary>
        public void SetOpenImmediate(bool open)
        {
            isOpen = open; stateTime = 10f; settled = true; landedKick = true;
            slide = open ? 1f : 0f; slideVel = 0f; peek = peekVel = 0f;
            clipAng = clipAngVel = clipPop = clipPopVel = 0f;
            for (int i = 0; i < SheetCount; i++)
            {
                var s = SheetAt(i);
                if (s == null) continue;
                s.open = s.target = open ? 1f : 0f; s.openVel = 0f;
            }
            ApplyPose();
        }

        /// <summary>Little "look at me" bump, e.g. when a new to-do arrived while the list is closed.</summary>
        public void Nudge(float strength = 1f)
        {
            slideVel += (isOpen ? -0.9f : 1.6f) * strength;
            clipAngVel += clipKickAngle * 10f * strength;
        }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            if (Application.isPlaying && toggleButton != null) toggleButton.onClick.AddListener(OnClipboardClicked);
            for (int i = 0; i < SheetCount; i++) if (SheetAt(i) != null) SheetAt(i).owner = this;
            RestoreSiblingOrder();
            SetOpenImmediate(startOpen);
        }

        void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(OnClipboardClicked);
        }

        void OnClipboardClicked() { if (toggleOnClipboardClick) Toggle(); }

        internal void NotifySheetClicked(ClipboardSheet s) { if (!isOpen && toggleOnClipboardClick) Open(); }

        void Update()
        {
            if (manualAdvance) return;
            Advance(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        // the to-do sheet is the front-most (last sibling); the last back sheet is at the very back
        void RestoreSiblingOrder()
        {
            for (int i = SheetCount - 1; i >= 0; i--) { var s = SheetAt(i); if (s != null) s.transform.SetAsLastSibling(); }
        }

        public void OnPointerEnter(PointerEventData e) { pointerOverBoard = true; }
        public void OnPointerExit(PointerEventData e) { pointerOverBoard = false; }

        // ------------------------------------------------------------------ simulation
        static void Spring(ref float x, ref float v, float target, float freq, float damp, float dt)
        {
            float w = 2f * Mathf.PI * freq;
            int n = Mathf.Max(1, Mathf.CeilToInt(dt * 240f));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                v += (-2f * damp * w * v - w * w * (x - target)) * h;
                x += v * h;
            }
        }

        public void Advance(float dt)
        {
            if (clipboard == null) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            stateTime += dt;
            int n = SheetCount;

            // --- targets
            float slideTarget;
            if (isOpen)
            {
                slideTarget = 1f;
                for (int i = 0; i < n; i++) SheetAt(i).target = stateTime >= sheetsDelay + i * sheetStagger ? 1f : 0f;
            }
            else
            {
                for (int i = 0; i < n; i++) if (stateTime >= (n - 1 - i) * closeStagger) SheetAt(i).target = 0f;
                slideTarget = stateTime >= (n - 1) * closeStagger + closeClipboardDelay ? 0f : 1f;
                if (slide < 0.001f && slideVel <= 0f) slideTarget = 0f; // was never opened
            }

            // --- clipboard
            float prevSlide = slide;
            float travel = OpenX() - ClosedX();
            float prevPxVel = slideVel * travel - peekVel * peekHoverOut;
            Spring(ref slide, ref slideVel, slideTarget, slideFrequency, slideDamping, dt);
            Spring(ref peek, ref peekVel, (!isOpen && pointerOverBoard && slide < 0.2f) ? 1f : 0f, 5f, 0.45f, dt);
            if (isOpen && !landedKick && prevSlide < 0.97f && slide >= 0.97f)
            {
                landedKick = true;
                clipAngVel += clipKickAngle * 14f;
                clipPopVel += clipSquash * 16f;
            }

            // --- sheets
            for (int i = 0; i < n; i++)
            {
                var s = SheetAt(i);
                float before = s.open;
                // going back onto the board is critically damped so the sheets settle neatly in the clip
                Spring(ref s.open, ref s.openVel, s.target, sheetFrequency, s.target < 0.5f ? Mathf.Max(sheetDamping, 1f) : sheetDamping, dt);
                if (s.target < 0.5f && s.open < 0f) { s.open = 0f; s.openVel = 0f; }
                if (i == 0 && isOpen && before < 0.02f && s.open >= 0.02f) { clipPopVel -= clipSquash * 20f; clipAngVel -= clipKickAngle * 8f; }
                if (i == 0 && !isOpen && before > 0.03f && s.open <= 0.03f) { clipPopVel += clipSquash * 14f; clipAngVel += clipKickAngle * 5f; }
            }

            // --- skull pendant: lags behind the clipboard's acceleration, then swings out
            float pxVel = slideVel * travel - peekVel * peekHoverOut;
            clipAngVel -= (pxVel - prevPxVel) * clipInertia;
            Spring(ref clipAng, ref clipAngVel, 0f, clipSwingFrequency, clipSwingDamping, dt);
            clipAng = Mathf.Clamp(clipAng, -clipMaxAngle, clipMaxAngle);
            Spring(ref clipPop, ref clipPopVel, 0f, 6f, 0.35f, dt);

            ApplyPose();

            // --- events
            if (!settled)
            {
                bool done = Mathf.Abs(slide - slideTarget) < 0.01f && Mathf.Abs(slideVel) < 0.05f;
                for (int i = 0; i < n && done; i++)
                    done = Mathf.Abs(SheetAt(i).open - SheetAt(i).target) < 0.01f && Mathf.Abs(SheetAt(i).openVel) < 0.05f;
                if (isOpen) done &= n == 0 || SheetAt(n - 1).target > 0.5f;
                else done &= slideTarget < 0.5f;
                if (done)
                {
                    settled = true;
                    if (isOpen) onOpened.Invoke(); else onClosed.Invoke();
                }
            }
        }

        // ------------------------------------------------------------------ pose
        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        float HalfWidth() { return clipboard == null ? 0f : clipboard.rect.width * 0.5f * Mathf.Abs(clipboard.localScale.x); }
        float ClosedX() { return HalfWidth() - closedPeek; }
        float OpenX() { return -HalfWidth() - openRightMargin; }

        void ApplyPose()
        {
            if (clipboard == null) return;
            float closedX = ClosedX(), openX = OpenX();
            float x = Mathf.LerpUnclamped(closedX, openX, slide) - peekHoverOut * peek;
            var p = clipboard.anchoredPosition;
            clipboard.anchoredPosition = new Vector2(x, p.y);
            float pxSpeed = slideVel * (openX - closedX);
            clipboard.localEulerAngles = new Vector3(0f, 0f, Mathf.Clamp(pxSpeed * leanPerSpeed, -maxLean, maxLean));

            if (clip != null)
            {
                clip.localEulerAngles = new Vector3(0f, 0f, clipAng);
                clip.localScale = new Vector3(1f + clipPop * 0.5f, 1f - clipPop, 1f);
            }

            for (int i = 0; i < SheetCount; i++)
            {
                var s = SheetAt(i);
                if (s == null) continue;
                float t = s.open;
                Vector2 off = Vector2.zero, homeOff = Vector2.zero; float extra = 0f, homeAng = 0f;
                if (i > 0)
                {
                    int b = i - 1;
                    if (backOffsets != null && b < backOffsets.Length) off = backOffsets[b];
                    if (backAngles != null && b < backAngles.Length) extra = backAngles[b];
                    if (backHomeOffsets != null && b < backHomeOffsets.Length) homeOff = backHomeOffsets[b];
                    if (backHomeAngles != null && b < backHomeAngles.Length) homeAng = backHomeAngles[b];
                }
                Vector2 from = sheetHome + homeOff;
                Vector2 to = sheetHome + new Vector2(-sheetSlide, -sheetDrop) + off;
                Vector2 c = Vector2.LerpUnclamped(from, to, t);
                c.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * dealArc;
                float ang = Mathf.LerpUnclamped(homeAng, todoAngle + extra, t);
                float sc = Mathf.LerpUnclamped(sheetHomeScale, 1f, t);
                var rt = s.Rect;
                rt.anchoredPosition = c;
                rt.localEulerAngles = new Vector3(0f, 0f, ang);
                rt.localScale = new Vector3(sc, sc, 1f);
                // flat on the board the shadow is tight; lifted off it grows
                if (s.shadow != null) s.shadow.rectTransform.anchoredPosition = Rotate(sheetShadowOffset, -ang) * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t)) / sc;
            }
        }

        /// <summary>Editor helper: shows the open / closed pose outside Play mode (inspector ⋮ menu).</summary>
        public void ApplyPoseInEditor(bool open) { RestoreSiblingOrder(); SetOpenImmediate(open); }

        [ContextMenu("Preview: Open pose")]
        void PreviewOpen() { ApplyPoseInEditor(true); }

        [ContextMenu("Preview: Closed pose")]
        void PreviewClosed() { ApplyPoseInEditor(false); }
    }
}
