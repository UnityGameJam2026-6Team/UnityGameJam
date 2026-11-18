using TMPro;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    internal sealed class MinionSpeechBubble
    {
        private readonly Canvas _canvas;
        private readonly RectTransform _panel;
        private readonly TextMeshProUGUI _label;

        public MinionSpeechBubble(TMP_Text fontSource)
        {
            _canvas = fontSource.canvas.rootCanvas;
            _panel = new GameObject("MinionSpeechBubble", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<RectTransform>();
            _panel.SetParent(_canvas.transform, false);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.sizeDelta = new Vector2(360f, 84f);
            var background = _panel.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.16f, 0.12f, 0.22f, 0.95f);
            background.raycastTarget = false;
            var tail = new GameObject("Tail", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<UnityEngine.UI.Image>();
            tail.rectTransform.SetParent(_panel, false);
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            tail.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            tail.rectTransform.sizeDelta = new Vector2(13f, 13f);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.color = new Color(0.16f, 0.12f, 0.22f, 1f);
            tail.raycastTarget = false;

            _label = new GameObject("Speech", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            RectTransform text = _label.rectTransform;
            text.SetParent(_panel, false);
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(14f, 8f);
            text.offsetMax = new Vector2(-14f, -8f);
            _label.font = fontSource.font;
            _label.fontSize = 24f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(1f, 0.97f, 0.9f);
            _label.raycastTarget = false;
            Hide();
        }

        public void Show(MatchBootstrap bootstrap, (EWorkerType Speaker, string Text)? speech)
        {
            if (!speech.HasValue) { Hide(); return; }
            foreach (MinionAgent minion in bootstrap.Minions)
            {
                if (minion.Worker != speech.Value.Speaker || !bootstrap.CanSeeMinion(minion)) { continue; }
                Camera camera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
                if (camera == null) { break; }
                Vector3 screen = camera.WorldToScreenPoint(minion.Position + Vector3.up * 1.85f);
                if (screen.z <= 0f || !camera.pixelRect.Contains(screen)) { break; }
                var canvasRect = (RectTransform)_canvas.transform;
                Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, canvasCamera, out Vector2 origin)) { break; }
                Rect bounds = canvasRect.rect;
                float halfWidth = _panel.rect.width * 0.5f;
                origin.x = Mathf.Clamp(origin.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
                origin.y = Mathf.Clamp(origin.y + 10f, bounds.yMin, bounds.yMax - _panel.rect.height);
                _panel.localPosition = origin;
                _label.text = speech.Value.Text;
                _panel.gameObject.SetActive(true);
                return;
            }
            Hide();
        }

        public void Hide() => _panel.gameObject.SetActive(false);

        public void Destroy() => Object.Destroy(_panel.gameObject);
    }
}
