using System;
using TMPro;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    public sealed class ProposalCard : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _background;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _dialogue;
        [SerializeField] private TMP_Text _seconds;
        [SerializeField] private GameObject _hint;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Color _proposalColor;
        [SerializeField] private Color _targetColor;
        [SerializeField] private Color _noticeColor;
        [SerializeField] private UnityEngine.UI.Button _approveButton;
        [SerializeField] private UnityEngine.UI.Button _holdButton;
        [SerializeField] private float _verticalMargins = 146f;
        [Header("등장과 응답 연출")]
        [SerializeField] private RectTransform _visual;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private UnityEngine.UI.Image _stamp;
        [SerializeField] private Sprite _approveStamp;
        [SerializeField] private Sprite _holdStamp;
        [SerializeField, Min(0.01f)] private float _enterSeconds = 0.24f;
        [SerializeField, Min(0.01f)] private float _exitSeconds = 0.26f;
        [SerializeField, Min(0.01f)] private float _stampSeconds = 0.14f;
        [SerializeField, Min(0f)] private float _stampHoldSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _slideDistance = 48f;
        private int _messageId = -1;
        private float _width = -1f;
        private bool _isTarget;
        private bool _hasStamp;
        private float _enteredAt;
        private float _dismissedAt;
        private Vector2 _dismissOffset;
        private float _dismissAlpha;
        private RectTransform _rect;
        private UnityEngine.UI.LayoutElement _layout;
        private Action<int, bool> _respond;
        private TMP_Text _approveLabel;
        private TMP_Text _holdLabel;
        private int _controlMode = -1;

        public int MessageId => _messageId;
        public bool IsDismissing { get; private set; }
        public float Height => _layout != null ? _layout.preferredHeight : _rect.rect.height;

        private void Awake()
        {
            if (_rect != null) { return; }
            _rect = (RectTransform)transform;
            _layout = GetComponent<UnityEngine.UI.LayoutElement>();
            _approveLabel = _approveButton != null ? _approveButton.GetComponentInChildren<TMP_Text>(true) : null;
            _holdLabel = _holdButton != null ? _holdButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (_approveButton != null) { _approveButton.onClick.AddListener(Approve); }
            if (_holdButton != null) { _holdButton.onClick.AddListener(Hold); }
        }

        public void Bind(Action<int, bool> respond) { _respond = respond; }
        private void Approve() { if (_isTarget && !IsDismissing) { _respond?.Invoke(_messageId, true); } }
        private void Hold() { if (_isTarget && !IsDismissing) { _respond?.Invoke(_messageId, false); } }

        private void OnDisable()
        {
            _isTarget = false;
            _messageId = -1;
            IsDismissing = false;
            _hasStamp = false;
            if (_stamp != null) { _stamp.gameObject.SetActive(false); }
        }

        public float Show(MessageEntry message, EZoneId? reportedZone, float elapsed, bool isTarget, float bottom)
        {
            if (_rect == null) { Awake(); }
            bool isNewMessage = _messageId != message.MessageId;
            int mode = GameControlSettings.UseSpaceAndMouse ? 1 : 0;
            if (_controlMode != mode)
            {
                if (_approveLabel != null) { _approveLabel.text = mode == 1 ? "확인" : "확인 K / Y"; }
                if (_holdLabel != null) { _holdLabel.text = mode == 1 ? "보류" : "보류 L / N"; }
                _controlMode = mode;
            }
            bool isProposal = message.Kind == EMessageKind.Proposal;
            _isTarget = isProposal && isTarget && !IsDismissing;
            float width = _rect.rect.width;
            if (isNewMessage || !Mathf.Approximately(_width, width))
            {
                _messageId = message.MessageId;
                _width = width;
                string worker = MinionStatusPanel.WorkerName(message.Minion);
                string subject = message.ProposedZone.HasValue ? ZoneDisplayName.GetFullName(message.ProposedZone.Value) : "안내";
                _title.text = $"{worker}  ·  {subject}";
                string dialogue = FormatDialogue(message.Text, reportedZone);
                _dialogue.text = dialogue;
                float height = Mathf.Ceil(_dialogue.GetPreferredValues(dialogue, Mathf.Max(40f, width - 40f), Mathf.Infinity).y) + _verticalMargins;
                if (_layout != null) { _layout.preferredHeight = height; }
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            if (isNewMessage)
            {
                _enteredAt = Time.unscaledTime;
                IsDismissing = false;
                _hasStamp = false;
                if (_stamp != null) { _stamp.gameObject.SetActive(false); }
                if (_visual != null) { _visual.anchoredPosition = Vector2.down * _slideDistance; }
                if (_canvasGroup != null) { _canvasGroup.alpha = 0f; }
            }
            if (_layout == null) { _rect.anchoredPosition = new Vector2(0f, bottom); }
            _background.color = _isTarget ? _targetColor : isProposal ? _proposalColor : _noticeColor;
            _hint.SetActive(_isTarget);
            if (_approveButton != null)
            {
                _approveButton.gameObject.SetActive(isProposal);
                _approveButton.interactable = _isTarget;
            }
            if (_holdButton != null)
            {
                _holdButton.gameObject.SetActive(isProposal);
                _holdButton.interactable = _isTarget;
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = _isTarget;
                _canvasGroup.blocksRaycasts = _isTarget;
            }
            _seconds.gameObject.SetActive(isProposal);
            _fill.gameObject.SetActive(isProposal);
            if (isProposal)
            {
                _seconds.SetText("{0}초", Mathf.Ceil(message.RemainingTime));
                float duration = Mathf.Max(0f, elapsed - message.ShownAt) + message.RemainingTime;
                float fraction = duration > 0f ? Mathf.Clamp01(message.RemainingTime / duration) : 0f;
                _fill.anchorMax = new Vector2(fraction, _fill.anchorMax.y);
            }
            return Height;
        }

        public void ShowResponse(bool approve)
        {
            if (IsDismissing) { return; }
            // 등장 직후 응답해도 투명한 상태로 도장이 찍히지 않게 한다.
            if (_visual != null) { _visual.anchoredPosition = Vector2.zero; }
            if (_canvasGroup != null) { _canvasGroup.alpha = 1f; }
            if (_stamp != null && _visual != null)
            {
                var button = (RectTransform)(approve ? _approveButton : _holdButton).transform;
                _stamp.sprite = approve ? _approveStamp : _holdStamp;
                _stamp.rectTransform.anchoredPosition = _visual.InverseTransformPoint(button.TransformPoint(button.rect.center));
                _stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, approve ? -12f : 12f);
                _stamp.rectTransform.localScale = Vector3.one * 1.6f;
                _stamp.color = new Color(1f, 1f, 1f, 0f);
                _stamp.gameObject.SetActive(true);
                _stamp.transform.SetAsLastSibling();
                _hasStamp = true;
            }
            BeginDismiss();
        }

        public void BeginDismiss()
        {
            if (IsDismissing) { return; }
            IsDismissing = true;
            _isTarget = false;
            _dismissedAt = Time.unscaledTime;
            _dismissOffset = _visual != null ? _visual.anchoredPosition : Vector2.zero;
            _dismissAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void LateUpdate()
        {
            if (_messageId < 0) { return; }
            if (IsDismissing)
            {
                float elapsed = Time.unscaledTime - _dismissedAt;
                float delay = _hasStamp ? _stampSeconds + _stampHoldSeconds : 0f;
                if (_hasStamp)
                {
                    float stampProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _stampSeconds));
                    _stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, EaseOut(stampProgress));
                    _stamp.color = new Color(1f, 1f, 1f, Mathf.Clamp01(stampProgress * 3f));
                }
                float progress = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.01f, _exitSeconds));
                if (_visual != null) { _visual.anchoredPosition = _dismissOffset + Vector2.down * (_slideDistance * EaseOut(progress)); }
                if (_canvasGroup != null) { _canvasGroup.alpha = _dismissAlpha * (1f - progress); }
                if (progress >= 1f) { gameObject.SetActive(false); }
                return;
            }
            float enterProgress = Mathf.Clamp01((Time.unscaledTime - _enteredAt) / Mathf.Max(0.01f, _enterSeconds));
            if (_visual != null) { _visual.anchoredPosition = Vector2.down * (_slideDistance * (1f - EaseOut(enterProgress))); }
            if (_canvasGroup != null) { _canvasGroup.alpha = EaseOut(enterProgress); }
        }

        private static float EaseOut(float progress) => 1f - Mathf.Pow(1f - progress, 3f);

        internal static string FormatDialogue(string text, EZoneId? reportedZone)
        {
            if (!reportedZone.HasValue) { return text; }
            string room = ZoneDisplayName.GetFullName(reportedZone.Value);
            return $"{room}에서 지금 할 수 있는 일이 없어.\n{text}";
        }
    }
}
