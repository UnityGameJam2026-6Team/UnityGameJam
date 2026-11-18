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
        [SerializeField] private float _verticalMargins = 104f;
        private int _messageId = -1;
        private float _width = -1f;
        private bool _isTarget;
        private RectTransform _rect;
        private UnityEngine.UI.LayoutElement _layout;
        private Action<int, bool> _respond;

        public int MessageId => _messageId;

        private void Awake()
        {
            if (_rect != null) { return; }
            _rect = (RectTransform)transform;
            _layout = GetComponent<UnityEngine.UI.LayoutElement>();
            if (_approveButton != null) { _approveButton.onClick.AddListener(Approve); }
            if (_holdButton != null) { _holdButton.onClick.AddListener(Hold); }
        }

        public void Bind(Action<int, bool> respond) { _respond = respond; }
        private void Approve() { if (_isTarget) { _respond?.Invoke(_messageId, true); } }
        private void Hold() { if (_isTarget) { _respond?.Invoke(_messageId, false); } }

        private void OnDisable()
        {
            _isTarget = false;
            _messageId = -1;
        }

        public float Show(MessageEntry message, EZoneId? reportedZone, float elapsed, bool isTarget, float bottom)
        {
            if (_rect == null) { Awake(); }
            bool isProposal = message.Kind == EMessageKind.Proposal;
            _isTarget = isProposal && isTarget;
            float width = _rect.rect.width;
            if (_messageId != message.MessageId || !Mathf.Approximately(_width, width))
            {
                _messageId = message.MessageId;
                _width = width;
                string worker = MinionStatusPanel.WorkerName(message.Minion);
                string subject = message.ProposedZone.HasValue ? ZoneDisplayName.GetFullName(message.ProposedZone.Value) : "안내";
                _title.text = $"{worker}  ·  {subject}";
                string dialogue = FormatDialogue(message.Text, reportedZone);
                _dialogue.text = dialogue;
                float height = Mathf.Ceil(_dialogue.GetPreferredValues(dialogue, Mathf.Max(40f, width - 32f), Mathf.Infinity).y) + _verticalMargins;
                if (_layout != null) { _layout.preferredHeight = height; }
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            if (_layout == null) { _rect.anchoredPosition = new Vector2(0f, bottom); }
            _background.color = _isTarget ? _targetColor : isProposal ? _proposalColor : _noticeColor;
            _hint.SetActive(_isTarget);
            if (_approveButton != null) { _approveButton.gameObject.SetActive(_isTarget); }
            if (_holdButton != null) { _holdButton.gameObject.SetActive(_isTarget); }
            _seconds.gameObject.SetActive(isProposal);
            _fill.gameObject.SetActive(isProposal);
            if (isProposal)
            {
                _seconds.SetText("{0}초", Mathf.Ceil(message.RemainingTime));
                float duration = Mathf.Max(0f, elapsed - message.ShownAt) + message.RemainingTime;
                float fraction = duration > 0f ? Mathf.Clamp01(message.RemainingTime / duration) : 0f;
                _fill.anchorMax = new Vector2(fraction, _fill.anchorMax.y);
            }
            return _layout != null ? _layout.preferredHeight : _rect.rect.height;
        }

        internal static string FormatDialogue(string text, EZoneId? reportedZone)
        {
            if (!reportedZone.HasValue) { return text; }
            string room = ZoneDisplayName.GetFullName(reportedZone.Value);
            return $"{room}에서 지금 할 수 있는 일이 없어.\n{text}";
        }
    }
}
