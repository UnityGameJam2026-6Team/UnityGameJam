using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    public sealed class ProposalStackView : MonoBehaviour
    {
        [SerializeField] private ProposalCard _template;
        [SerializeField] private InputActionReference _approve;
        [SerializeField] private InputActionReference _hold;
        [SerializeField] private float _spacing = 8f;
        [SerializeField] private UnityEngine.UI.ScrollRect[] _lanes = Array.Empty<UnityEngine.UI.ScrollRect>();
        [SerializeField] private float _maximumHeight = 420f;
        [SerializeField] private float _topReserve = 340f;
        private const int SharedLane = 4;
        private readonly EZoneId?[] _assignedZones = new EZoneId?[SharedLane];
        private readonly Dictionary<int, EZoneId?> _reportedZones = new Dictionary<int, EZoneId?>();
        private readonly List<ProposalCard> _cards = new List<ProposalCard>();
        private readonly float[] _heights = new float[SharedLane + 1];
        private readonly int[] _counts = new int[SharedLane + 1];
        private readonly int[] _lastMessage = new int[SharedLane + 1];
        private RectTransform _rect;
        private RectTransform _parent;
        private Vector2 _defaultOrigin;
        private float _sharedMaximumHeight;
        private IMatchSession _session;
        private bool _isTalking;
        private bool _enabledApprove;
        private bool _enabledHold;
        private int _targetId = -1;
        private int _lastTargetId = -1;
        private Action<int, bool> _response;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = _rect.parent as RectTransform;
            _defaultOrigin = _rect.anchoredPosition;
            _response = Respond;
            if (_lanes.Length > SharedLane && _lanes[SharedLane] != null)
            {
                _sharedMaximumHeight = ((RectTransform)_lanes[SharedLane].transform).rect.height;
            }
            Array.Fill(_lastMessage, -1);
        }

        private void OnEnable()
        {
            _enabledApprove = _approve != null && !_approve.action.enabled;
            _enabledHold = _hold != null && !_hold.action.enabled;
            if (_enabledApprove) { _approve.action.Enable(); }
            if (_enabledHold) { _hold.action.Enable(); }
        }

        private void OnDisable()
        {
            if (_enabledApprove) { _approve.action.Disable(); }
            if (_enabledHold) { _hold.action.Disable(); }
            _session = null;
            _targetId = -1;
            _lastTargetId = -1;
            _reportedZones.Clear();
            Array.Clear(_assignedZones, 0, _assignedZones.Length);
            Array.Fill(_lastMessage, -1);
        }

        internal static int LaneIndex(EWorkerType worker) => worker switch
        {
            EWorkerType.Goblin => 0,
            EWorkerType.Orc => 1,
            EWorkerType.Skeleton => 2,
            EWorkerType.Slime => 3,
            _ => SharedLane
        };

        public void Show(IMatchSession session, IReadOnlyList<MinionStatus> minions, float elapsed,
            RectTransform lowerBoundary, bool isTalking)
        {
            if (_session != session)
            {
                _reportedZones.Clear();
                Array.Clear(_assignedZones, 0, _assignedZones.Length);
            }
            _session = session;
            RememberAssignments(minions);
            _isTalking = isTalking;
            bool isRunning = session != null && session.MatchState == EMatchState.Running;
            IReadOnlyList<MessageEntry> messages = isRunning ? session.ActiveMessages : null;
            int count = messages != null ? messages.Count : 0;
            _targetId = -1;
            Vector2 origin = _defaultOrigin;
            if (_parent != null && lowerBoundary != null)
            {
                Vector3 top = lowerBoundary.TransformPoint(0f, lowerBoundary.rect.yMax, 0f);
                float anchor = _parent.rect.yMin + _parent.rect.height * _rect.anchorMin.y;
                origin.y = _parent.InverseTransformPoint(top).y - anchor + _spacing;
            }
            _rect.anchoredPosition = origin;
            Array.Clear(_heights, 0, _heights.Length);
            Array.Clear(_counts, 0, _counts.Length);
            float fallbackBottom = 0f;
            for (int i = 0; i < count; i++)
            {
                while (_cards.Count <= i)
                {
                    ProposalCard created = Instantiate(_template, transform);
                    created.Bind(_response);
                    _cards.Add(created);
                }
                MessageEntry message = messages[i];
                bool isTarget = !isTalking && message.IsInputTarget && message.Kind == EMessageKind.Proposal;
                if (isTarget) { _targetId = message.MessageId; }
                int laneIndex = LaneIndex(message.Minion);
                UnityEngine.UI.ScrollRect lane = laneIndex < _lanes.Length ? _lanes[laneIndex] : null;
                ProposalCard card = _cards[i];
                Transform parent = lane != null ? lane.content : transform;
                bool changed = card.MessageId != message.MessageId || card.transform.parent != parent;
                if (card.transform.parent != parent) { card.transform.SetParent(parent, false); }
                if (!card.gameObject.activeSelf) { card.gameObject.SetActive(true); }
                if (changed) { card.transform.SetAsLastSibling(); }
                var cardRect = (RectTransform)card.transform;
                if (lane != null) { cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lane.content.rect.width); }
                float height = card.Show(message, ReportedZone(message, laneIndex), elapsed, isTarget, fallbackBottom);
                _heights[laneIndex] += height + _spacing;
                _counts[laneIndex]++;
                fallbackBottom += height + _spacing;
            }
            for (int i = count; i < _cards.Count; i++)
            {
                if (_cards[i].gameObject.activeSelf) { _cards[i].gameObject.SetActive(false); }
            }
            float availableHeight = _parent != null ? Mathf.Max(0f, _parent.rect.height - origin.y - _topReserve) : _maximumHeight;
            float sharedHeight = Mathf.Min(_sharedMaximumHeight, Mathf.Max(0f, _heights[SharedLane] - _spacing));
            float tallestWorkerLane = 0f;
            for (int i = 0; i < SharedLane; i++)
            {
                tallestWorkerLane = Mathf.Max(tallestWorkerLane, _heights[i] - _spacing);
            }
            if (tallestWorkerLane > 0f) { sharedHeight = Mathf.Min(sharedHeight, availableHeight * 0.5f); }
            else { sharedHeight = Mathf.Min(sharedHeight, availableHeight); }
            float workerSpace = availableHeight - (sharedHeight > 0f ? sharedHeight + _spacing : 0f);
            float workerHeightLimit = Mathf.Clamp(workerSpace, 0f, _maximumHeight);
            float sharedBottom = tallestWorkerLane > 0f ? Mathf.Min(tallestWorkerLane, workerHeightLimit) + _spacing : 0f;

            for (int i = 0; i < _lanes.Length && i < _heights.Length; i++)
            {
                UnityEngine.UI.ScrollRect lane = _lanes[i];
                if (lane == null) { continue; }
                bool active = _counts[i] > 0;
                if (lane.gameObject.activeSelf != active) { lane.gameObject.SetActive(active); }
                if (!active) { _lastMessage[i] = -1; continue; }
                float height = i == SharedLane ? sharedHeight : Mathf.Min(workerHeightLimit, _heights[i] - _spacing);
                var laneRect = (RectTransform)lane.transform;
                if (i == SharedLane) { laneRect.anchoredPosition = new Vector2(0f, sharedBottom); }
                bool isHeightChanged = !Mathf.Approximately(laneRect.rect.height, height);
                if (isHeightChanged) { laneRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height); }
                int newestId = -1;
                for (int j = count - 1; j >= 0; j--)
                {
                    if (LaneIndex(messages[j].Minion) == i) { newestId = messages[j].MessageId; break; }
                }
                bool hasNewTarget = _targetId != _lastTargetId && _targetId >= 0;
                if (isHeightChanged || _lastMessage[i] != newestId || hasNewTarget)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(lane.content);
                    lane.verticalNormalizedPosition = 0f;
                    // 응답 대상 카드를 먼저 보여 주고 나머지 메시지는 스크롤로 읽는다.
                    for (int j = 0; j < count; j++)
                    {
                        if (messages[j].MessageId != _targetId || LaneIndex(messages[j].Minion) != i) { continue; }
                        var target = (RectTransform)_cards[j].transform;
                        float scrollRange = lane.content.rect.height - lane.viewport.rect.height;
                        if (scrollRange > 0f)
                        {
                            float top = -target.anchoredPosition.y;
                            lane.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / scrollRange);
                        }
                        break;
                    }
                    _lastMessage[i] = newestId;
                }
            }
            _lastTargetId = _targetId;
            if (_targetId < 0) { return; }
            if (_approve != null && _approve.action.WasPressedThisFrame()) { Respond(_targetId, true); }
            else if (_hold != null && _hold.action.WasPressedThisFrame()) { Respond(_targetId, false); }
        }

        private void RememberAssignments(IReadOnlyList<MinionStatus> minions)
        {
            foreach (MinionStatus status in minions)
            {
                int lane = LaneIndex(status.Minion);
                if (lane < SharedLane && status.AssignedZone.HasValue)
                {
                    _assignedZones[lane] = status.AssignedZone;
                }
            }
        }

        private EZoneId? ReportedZone(MessageEntry message, int lane)
        {
            if (lane == SharedLane) { return null; }
            if (!_reportedZones.TryGetValue(message.MessageId, out EZoneId? zone))
            {
                // 복귀 통보가 도착하면 세션에서는 이미 배정이 해제되어 있다.
                zone = _assignedZones[lane];
                _reportedZones.Add(message.MessageId, zone);
            }
            return zone;
        }

        private void Respond(int messageId, bool approve)
        {
            if (_session == null || _isTalking || _session.MatchState != EMatchState.Running) { return; }
            IReadOnlyList<MinionStatus> minions = _session.Minions;
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].Activity == EMinionActivity.Talking) { return; }
            }
            IReadOnlyList<MessageEntry> messages = _session.ActiveMessages;
            for (int i = 0; i < messages.Count; i++)
            {
                MessageEntry message = messages[i];
                if (message.MessageId != messageId || !message.IsInputTarget || message.Kind != EMessageKind.Proposal || message.RemainingTime <= 0f) { continue; }
                bool accepted = approve ? _session.ApproveOldestProposal() : _session.HoldOldestProposal();
                if (accepted)
                {
                    GameAudioPresenter.MarkHandled(messageId);
                    GameSfxPlayer.Play(approve ? ESfxCue.Confirm : ESfxCue.Cancel);
                }
                return;
            }
        }
    }
}
