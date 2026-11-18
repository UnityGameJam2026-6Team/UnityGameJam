using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    public sealed class ProposalCard : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _dialogue;
        [SerializeField] private TMP_Text _seconds;
        [SerializeField] private GameObject _hint;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Color _proposalColor;
        [SerializeField] private Color _targetColor;
        [SerializeField] private Color _noticeColor;
        private int _messageId = -1;

        public float Show(MessageEntry message, float elapsed, bool isTarget, float bottom)
        {
            bool isProposal = message.Kind == EMessageKind.Proposal;
            var rect = (RectTransform)transform;
            if (_messageId != message.MessageId)
            {
                _messageId = message.MessageId;
                string worker = MinionStatusPanel.WorkerName(message.Minion);
                string subject = message.ProposedZone.HasValue ? ZoneDisplayName.GetFullName(message.ProposedZone.Value) : "안내";
                _title.text = $"{worker}  ·  {subject}";
                _dialogue.text = message.Text;
                float margins = -_dialogue.rectTransform.sizeDelta.y;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(_dialogue.preferredHeight) + margins);
            }
            rect.anchoredPosition = new Vector2(0f, bottom);
            _background.color = isTarget ? _targetColor : isProposal ? _proposalColor : _noticeColor;
            _hint.SetActive(isTarget);
            _seconds.gameObject.SetActive(isProposal);
            _fill.gameObject.SetActive(isProposal);
            if (isProposal)
            {
                _seconds.SetText("{0}초", Mathf.Ceil(message.RemainingTime));
                float duration = Mathf.Max(0f, elapsed - message.ShownAt) + message.RemainingTime;
                float fraction = duration > 0f ? Mathf.Clamp01(message.RemainingTime / duration) : 0f;
                _fill.anchorMax = new Vector2(fraction, _fill.anchorMax.y);
            }
            return rect.rect.height;
        }
    }
}
