using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DemonKing.UI.Demo
{
    /// <summary>
    /// Demo only. Space: next speaker · B: next line (bump) · N: name plate on/off · H: hide all.
    /// </summary>
    public class SpeechBubbleDemoDriver : MonoBehaviour
    {
        public SpeechBubbleUI[] bubbles = new SpeechBubbleUI[0];
        int current = -1;

        void Update()
        {
            if (bubbles == null || bubbles.Length == 0) return;
            if (Down(0)) Next();
            if (Down(1) && current >= 0) bubbles[current].Show();
            if (Down(2) && current >= 0) bubbles[current].SetShowNamePlate(!bubbles[current].showNamePlate);
            if (Down(3)) { foreach (var b in bubbles) b.Hide(); current = -1; }
        }

        public void Next()
        {
            if (current >= 0) bubbles[current].Hide();
            current = (current + 1) % bubbles.Length;
            bubbles[current].Show();
        }

        static bool Down(int k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; if (kb == null) return false;
            switch (k) { case 0: return kb.spaceKey.wasPressedThisFrame; case 1: return kb.bKey.wasPressedThisFrame; case 2: return kb.nKey.wasPressedThisFrame; default: return kb.hKey.wasPressedThisFrame; }
#else
            switch (k) { case 0: return Input.GetKeyDown(KeyCode.Space); case 1: return Input.GetKeyDown(KeyCode.B); case 2: return Input.GetKeyDown(KeyCode.N); default: return Input.GetKeyDown(KeyCode.H); }
#endif
        }
    }
}
