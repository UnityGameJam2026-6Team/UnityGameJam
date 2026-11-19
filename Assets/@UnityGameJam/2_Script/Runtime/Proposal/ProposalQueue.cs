using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityGameJam.Contract;

namespace UnityGameJam.Proposal
{
    internal sealed class ProposalQueue
    {
        private readonly float _proposalSeconds;
        private readonly float _noticeSeconds;
        private readonly float _replySeconds;
        private readonly IProposalDialogueProvider _provider;
        private readonly Dictionary<EWorkerType, (ProposalRequest Request, float Deadline, CancellationTokenSource Cancellation)> _pending =
            new Dictionary<EWorkerType, (ProposalRequest, float, CancellationTokenSource)>();
        private readonly ConcurrentQueue<(ProposalRequest Request, ProposalReply Reply)> _replies =
            new ConcurrentQueue<(ProposalRequest, ProposalReply)>();
        private readonly List<(MessageEntry Message, ProposalRequest? Request, float Deadline)> _active =
            new List<(MessageEntry, ProposalRequest?, float)>();
        private readonly Dictionary<(EWorkerType, EWorkerType), float> _roomReportUntil = new();
        private float _time;
        private int _nextId;
        private bool _isClosed;

        public event Action Changed;
        public event Action<ProposalRequest, bool> Answered;

        public ProposalQueue(float proposalSeconds, float noticeSeconds, float replySeconds, IProposalDialogueProvider provider)
        {
            _proposalSeconds = proposalSeconds;
            _noticeSeconds = noticeSeconds;
            _replySeconds = replySeconds;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyList<MessageEntry> Messages
        {
            get
            {
                var result = new MessageEntry[_active.Count];
                bool hasInputTarget = false;
                for (int index = 0; index < _active.Count; index++)
                {
                    var (message, request, deadline) = _active[index];
                    bool isInputTarget = request.HasValue && !hasInputTarget;
                    hasInputTarget |= isInputTarget;
                    result[index] = new MessageEntry(message.MessageId, message.Kind, message.Minion, message.Text,
                        message.ProposedZone, message.ShownAt, Math.Max(0f, deadline - _time), isInputTarget, message.ProposalKind);
                }
                return result;
            }
        }

        public bool Request(EWorkerType worker, int assignmentId, EZoneId zone, EProposalReason reason,
            bool areMopsUnavailable, float? requestedAt = null, IReadOnlyList<EZoneId> candidates = null,
            bool isOrcHelp = false, IReadOnlyDictionary<EZoneId, EWorkerType> coworkers = null,
            IReadOnlyList<CollaborationPlan> plans = null, float? remainingSeconds = null, EContaminationType? preferredTask = null)
        {
            if (_isClosed || _pending.ContainsKey(worker)
                || _active.Exists(entry => entry.Request?.Worker == worker)) { return false; }
            var cancellation = new CancellationTokenSource();
            var request = new ProposalRequest(++_nextId, worker, assignmentId, zone, reason, areMopsUnavailable,
                candidates, isOrcHelp, cancellation.Token, coworkers, plans, remainingSeconds: remainingSeconds,
                preferredTask: preferredTask);
            _pending.Add(worker, (request, (requestedAt ?? _time) + _replySeconds, cancellation));
            try
            {
                _provider.Request(request, reply => _replies.Enqueue((request, reply)));
            }
            catch (Exception)
            {
                _replies.Enqueue((request, default));
            }
            return true;
        }

        public void AddNotice(EWorkerType worker, string text, float? shownAt = null, EProposalKind proposalKind = EProposalKind.Move)
        {
            if (_isClosed) { return; }
            float start = shownAt ?? _time;
            var message = new MessageEntry(++_nextId, EMessageKind.Notice, worker, text, null, start, _noticeSeconds, false, proposalKind);
            _active.Add((message, null, start + _noticeSeconds));
            Changed?.Invoke();
        }

        public void AddRoomReport(EWorkerType speaker, EWorkerType listener, float shownAt)
        {
            if (_isClosed) { return; }
            var pair = (int)speaker < (int)listener ? (speaker, listener) : (listener, speaker);
            if (_roomReportUntil.TryGetValue(pair, out float until) && shownAt < until) { return; }
            _roomReportUntil[pair] = shownAt + _noticeSeconds;
            string name = DefaultProposalDialogueProvider.WorkerName(listener);
            AddNotice(speaker, $"{name}, 내가 둘러본 방 상황을 알려줄게.", shownAt);
        }

        public void Tick(float time)
        {
            if (_isClosed) { return; }
            _time = time;
            bool hasChanged = _active.Count > 0;
            var expired = new List<ProposalRequest>();
            for (int index = 0; index < _active.Count;)
            {
                var entry = _active[index];
                if (entry.Deadline > time) { index++; continue; }
                _active.RemoveAt(index);
                if (entry.Request.HasValue) { expired.Add(entry.Request.Value); }
            }

            while (_replies.TryDequeue(out var reply))
            {
                if (!IsCurrent(reply.Request, out float deadline) || deadline <= time) { continue; }
                Show(reply.Request, reply.Reply);
                hasChanged = true;
            }

            if (_pending.Count > 0)
            {
                var timedOut = new List<ProposalRequest>();
                foreach (var pending in _pending.Values)
                {
                    if (pending.Deadline <= time) { timedOut.Add(pending.Request); }
                }
                foreach (ProposalRequest request in timedOut)
                {
                    Show(request, default);
                    hasChanged = true;
                }
            }

            foreach (ProposalRequest request in expired) { Answered?.Invoke(request, false); }
            if (hasChanged) { Changed?.Invoke(); }
        }

        public bool RespondOldest(bool isApproved, Predicate<ProposalRequest> tryApprove = null)
        {
            int index = _active.FindIndex(entry => entry.Request.HasValue);
            if (index < 0 || _isClosed) { return false; }
            ProposalRequest request = _active[index].Request.Value;
            _active.RemoveAt(index);
            bool accepted = !isApproved || tryApprove == null || tryApprove(request);
            if (accepted) { Answered?.Invoke(request, isApproved); }
            Changed?.Invoke();
            return accepted;
        }

        public void Cancel(EWorkerType worker)
        {
            CancelPending(worker);
            if (_active.RemoveAll(entry => entry.Request?.Worker == worker) > 0) { Changed?.Invoke(); }
        }

        public void Close()
        {
            _isClosed = true;
            foreach (EWorkerType worker in _pending.Keys.ToArray()) { CancelPending(worker); }
            if (_active.Count > 0)
            {
                _active.Clear();
                Changed?.Invoke();
            }
            _replies.Clear();
        }

        private bool IsCurrent(ProposalRequest request, out float deadline)
        {
            if (_pending.TryGetValue(request.Worker, out var pending) && pending.Request.Id == request.Id)
            {
                deadline = pending.Deadline;
                return true;
            }

            deadline = default;
            return false;
        }

        private void Show(ProposalRequest request, ProposalReply reply)
        {
            CancelPending(request.Worker);
            bool hasValidReply = !string.IsNullOrWhiteSpace(reply.Text) && (request.Plans.Count == 0
                ? !reply.Plan.HasValue && request.Candidates.Contains(reply.Zone)
                : reply.Plan is CollaborationPlan plan && reply.Zone == plan.OwnTask.Zone && request.Plans.Contains(plan));
            if (!hasValidReply) { reply = DefaultProposalDialogueProvider.Fallback(request); }
            var shown = new ProposalRequest(request.Id, request.Worker, request.AssignmentId, reply.Zone,
                request.Reason, request.AreMopsUnavailable, request.Candidates, request.IsOrcHelp,
                coworkers: request.Coworkers, selectedPlan: reply.Plan);
            var message = new MessageEntry(request.Id, EMessageKind.Proposal, request.Worker, reply.Text,
                reply.Zone, _time, _proposalSeconds, false, reply.Plan?.Kind ?? EProposalKind.Move);
            _active.Add((message, shown, _time + _proposalSeconds));
        }

        private void CancelPending(EWorkerType worker)
        {
            if (!_pending.Remove(worker, out var pending)) { return; }
            pending.Cancellation.Cancel();
            pending.Cancellation.Dispose();
        }
    }
}
