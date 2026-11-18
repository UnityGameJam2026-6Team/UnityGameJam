using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityGameJam.Contract;

namespace UnityGameJam.Proposal
{
    internal sealed class ProposalQueue
    {
        private readonly float _proposalSeconds;
        private readonly float _noticeSeconds;
        private readonly float _replySeconds;
        private readonly IProposalDialogueProvider _provider;
        private readonly Dictionary<EWorkerType, (ProposalRequest Request, float Deadline)> _pending =
            new Dictionary<EWorkerType, (ProposalRequest, float)>();
        private readonly ConcurrentQueue<(ProposalRequest Request, ProposalReply Reply)> _replies =
            new ConcurrentQueue<(ProposalRequest, ProposalReply)>();
        private readonly List<(MessageEntry Message, ProposalRequest? Request, float Deadline)> _active =
            new List<(MessageEntry, ProposalRequest?, float)>();
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
                        message.ProposedZone, message.ShownAt, Math.Max(0f, deadline - _time), isInputTarget);
                }
                return result;
            }
        }

        public void Request(EWorkerType worker, int assignmentId, EZoneId zone, EProposalReason reason,
            bool areMopsUnavailable, float? requestedAt = null)
        {
            if (_isClosed || _pending.ContainsKey(worker)
                || _active.Exists(entry => entry.Request?.Worker == worker)) { return; }
            var request = new ProposalRequest(++_nextId, worker, assignmentId, zone, reason, areMopsUnavailable);
            _pending.Add(worker, (request, (requestedAt ?? _time) + _replySeconds));
            try
            {
                _provider.Request(request, reply => _replies.Enqueue((request, reply)));
            }
            catch (Exception)
            {
                _replies.Enqueue((request, default));
            }
        }

        public void AddNotice(EWorkerType worker, string text, float? shownAt = null)
        {
            if (_isClosed) { return; }
            float start = shownAt ?? _time;
            var message = new MessageEntry(++_nextId, EMessageKind.Notice, worker, text, null, start, _noticeSeconds, false);
            _active.Add((message, null, start + _noticeSeconds));
            Changed?.Invoke();
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

        public bool RespondOldest(bool isApproved)
        {
            int index = _active.FindIndex(entry => entry.Request.HasValue);
            if (index < 0 || _isClosed) { return false; }
            ProposalRequest request = _active[index].Request.Value;
            _active.RemoveAt(index);
            Answered?.Invoke(request, isApproved);
            Changed?.Invoke();
            return true;
        }

        public void Cancel(EWorkerType worker)
        {
            _pending.Remove(worker);
            if (_active.RemoveAll(entry => entry.Request?.Worker == worker) > 0) { Changed?.Invoke(); }
        }

        public void Close()
        {
            _isClosed = true;
            _pending.Clear();
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
            _pending.Remove(request.Worker);
            string text = reply.Zone == request.Zone && !string.IsNullOrWhiteSpace(reply.Text)
                ? reply.Text : DefaultProposalDialogueProvider.Format(request);
            var message = new MessageEntry(request.Id, EMessageKind.Proposal, request.Worker, text,
                request.Zone, _time, _proposalSeconds, false);
            _active.Add((message, request, _time + _proposalSeconds));
        }
    }
}
