using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    public sealed class ProposalStackView : MonoBehaviour
    {
        [SerializeField] private ProposalCard _template;
        [SerializeField] private InputActionReference _approve;
        [SerializeField] private InputActionReference _hold;
        [SerializeField] private float _spacing = 8f;

        private readonly List<ProposalCard> _cards = new List<ProposalCard>();

        private void OnEnable()
        {
            _approve.action.Enable();
            _hold.action.Enable();
        }

        private void OnDisable()
        {
            _approve.action.Disable();
            _hold.action.Disable();
        }

        public void Show(IMatchSession session, float elapsed, RectTransform choices, bool isTalking)
        {
            bool isRunning = session.MatchState == EMatchState.Running;
            IReadOnlyList<MessageEntry> messages = session.ActiveMessages;
            int count = isRunning ? messages.Count : 0;
            bool canRespond = false;
            Vector2 origin = choices.anchoredPosition;
            if (isTalking) { origin.y += choices.rect.height + _spacing; }
            ((RectTransform)transform).anchoredPosition = origin;

            // 최신 메시지를 아래에 두고, 이전 메시지를 실제 카드 높이만큼 위로 민다.
            float bottom = 0f;
            for (int i = count - 1; i >= 0; i--)
            {
                while (_cards.Count <= i) { _cards.Add(Instantiate(_template, transform)); }
                MessageEntry message = messages[i];
                bool isTarget = !isTalking && message.IsInputTarget;
                canRespond |= isTarget;
                ProposalCard card = _cards[i];
                card.gameObject.SetActive(true);
                bottom += card.Show(message, elapsed, isTarget, bottom) + _spacing;
            }
            for (int i = count; i < _cards.Count; i++) { _cards[i].gameObject.SetActive(false); }

            if (!canRespond) { return; }
            if (_approve.action.WasPressedThisFrame()) { session.ApproveOldestProposal(); }
            else if (_hold.action.WasPressedThisFrame()) { session.HoldOldestProposal(); }
        }
    }
}
