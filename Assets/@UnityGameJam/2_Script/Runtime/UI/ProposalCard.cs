using System;
using System.Collections;
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
        [SerializeField] private UnityEngine.UI.Image _decisionStamp;
        [SerializeField] private TMP_Text _decisionStampText;
        [SerializeField] private Sprite _approveStamp;
        [SerializeField] private Sprite _holdStamp;
        private const float TextTop = 55f;
        private const float TextInset = 22f;
        private const string RoomTextColor = "#71436C";
        private static readonly Color MinionPaperTint = new Color32(173, 210, 156, 255);
        private static readonly Color PlayerPaperTint = new Color32(164, 195, 229, 255);
        private static readonly string[] RoomNames = Array.ConvertAll((EZoneId[])Enum.GetValues(typeof(EZoneId)), ZoneDisplayName.GetFullName);
        private int _messageId = -1;
        private float _width = -1f;
        private bool _isTarget;
        private RectTransform _rect;
        private UnityEngine.UI.LayoutElement _layout;
        private CanvasGroup _opacity;
        private Vector2 _restPivot;
        private Action<int, bool> _respond;

        public int MessageId => _messageId;

        private void Awake()
        {
            if (_rect != null) { return; }
            _rect = (RectTransform)transform;
            _restPivot = _rect.pivot;
            _layout = GetComponent<UnityEngine.UI.LayoutElement>();
            _dialogue.richText = true;
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
            if (Application.isPlaying && _decisionStamp != null && _decisionStamp.gameObject.activeSelf)
            {
                Destroy(gameObject);
            }
        }

        public float Show(MessageEntry message, EZoneId? reportedZone, float elapsed, bool isTarget, float bottom)
        {
            if (_rect == null) { Awake(); }
            if (_opacity == null && !TryGetComponent(out _opacity)) { _opacity = gameObject.AddComponent<CanvasGroup>(); }
            bool isProposal = message.Kind == EMessageKind.Proposal;
            bool isNotice = message.Kind == EMessageKind.Notice;
            bool wasTarget = _isTarget;
            _isTarget = isProposal && isTarget;
            float width = _rect.rect.width;
            if (_messageId != message.MessageId || !Mathf.Approximately(_width, width) || wasTarget != _isTarget)
            {
                _messageId = message.MessageId;
                _width = width;
                string speaker = MinionStatusPanel.WorkerName(message.Minion);
                _title.text = message.ProposalKind switch
                {
                    EProposalKind.MinionCollaboration => $"{speaker}  ·  졸개 협업",
                    EProposalKind.PlayerAssistance => $"{speaker}  ·  도움 요청",
                    _ => message.ProposedZone is EZoneId zone ? $"{speaker}  ·  {ZoneDisplayName.GetFullName(zone)}" : speaker
                };
                string dialogue = isProposal && message.ProposalKind == EProposalKind.Move
                    ? FormatDialogue(message.Text, reportedZone) : message.Text;
                dialogue = HighlightRoomNames(dialogue);
                _dialogue.text = dialogue;
                float dialogueBottom = _isTarget ? _verticalMargins - TextTop : 16f;
                _dialogue.rectTransform.offsetMin = new Vector2(TextInset, dialogueBottom);
                _dialogue.rectTransform.offsetMax = new Vector2(-TextInset, -TextTop);
                float height = Mathf.Ceil(_dialogue.GetPreferredValues(dialogue, Mathf.Max(1f, width - TextInset * 2f), Mathf.Infinity).y)
                    + TextTop + dialogueBottom;
                if (_layout != null) { _layout.preferredHeight = height; }
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                _rect.pivot = isNotice && _layout != null ? new Vector2(_restPivot.x, 0f) : _restPivot;
            }
            float offset = isNotice ? 1f - Mathf.Clamp01((elapsed - message.ShownAt) / .35f) : 0f;
            float eased = offset * offset * offset;
            if (_layout == null) { _rect.anchoredPosition = new Vector2(0f, bottom - 16f * eased); }
            _rect.localScale = new Vector3(1f, _layout != null ? 1f - .18f * eased : 1f, 1f);
            _opacity.alpha = isNotice ? (1f - eased) * Mathf.Clamp01(message.RemainingTime / .25f) : 1f;
            Color paper = _isTarget ? _targetColor : isProposal ? _proposalColor : _noticeColor;
            _background.color = message.ProposalKind switch
            {
                EProposalKind.MinionCollaboration => Color.Lerp(paper, MinionPaperTint, .42f),
                EProposalKind.PlayerAssistance => Color.Lerp(paper, PlayerPaperTint, .42f),
                _ => paper
            };
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

        internal bool ShowDecisionStamp(RectTransform layer, bool approved)
        {
            Sprite sprite = approved ? _approveStamp : _holdStamp;
            if (_decisionStamp == null || _decisionStampText == null || sprite == null || _opacity == null) { return false; }

            _isTarget = false;
            _hint.SetActive(false);
            if (_approveButton != null) { _approveButton.gameObject.SetActive(false); }
            if (_holdButton != null) { _holdButton.gameObject.SetActive(false); }
            _seconds.gameObject.SetActive(false);
            _fill.gameObject.SetActive(false);
            _opacity.interactable = false;
            _opacity.blocksRaycasts = false;

            // 응답한 카드는 스크롤 영역 밖으로 옮겨 새 제안에 재사용되거나 잘리지 않게 한다.
            Vector2 size = _rect.rect.size;
            Vector3 position = _rect.position;
            _rect.SetParent(layer, true);
            _rect.anchorMin = _rect.anchorMax = new Vector2(.5f, .5f);
            _rect.sizeDelta = size;
            _rect.position = position;
            _rect.SetAsLastSibling();

            _decisionStamp.sprite = sprite;
            _decisionStampText.text = approved ? "승인" : "보류";
            _decisionStamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, approved ? -12f : 10f);
            _decisionStamp.gameObject.SetActive(true);
            return true;
        }

        internal IEnumerator AnimateDecisionStamp()
        {
            const float strike = .09f;
            const float release = .60f;
            float elapsed = 0f;
            while (elapsed < release)
            {
                float scale = elapsed < strike
                    ? Mathf.Lerp(2.15f, .88f, Mathf.Clamp01(elapsed / strike))
                    : Mathf.SmoothStep(.88f, 1f, (elapsed - strike) / .13f);
                _decisionStamp.rectTransform.localScale = Vector3.one * scale;
                _opacity.alpha = 1f - Mathf.InverseLerp(.37f, release, elapsed);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            _decisionStamp.gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private static string HighlightRoomNames(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            foreach (string room in RoomNames)
            {
                if (text.Contains(room)) { text = text.Replace(room, $"<color={RoomTextColor}>{room}</color>"); }
            }
            return text;
        }

        internal static string FormatDialogue(string text, EZoneId? reportedZone)
        {
            if (!reportedZone.HasValue) { return text; }
            string room = ZoneDisplayName.GetFullName(reportedZone.Value);
            return $"{room}에서 지금 할 수 있는 일이 없어.\n{text}";
        }
    }
}
