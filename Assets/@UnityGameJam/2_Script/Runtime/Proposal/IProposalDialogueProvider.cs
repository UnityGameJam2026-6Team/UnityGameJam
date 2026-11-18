using System;
using System.Collections.Generic;
using System.Threading;
using UnityGameJam.Contract;

namespace UnityGameJam.Proposal
{
    internal enum EProposalReason { KnownWork, Explore, OtherWorker }

    internal readonly struct ProposalRequest
    {
        public int Id { get; }
        public EWorkerType Worker { get; }
        public int AssignmentId { get; }
        public EZoneId Zone { get; }
        public EProposalReason Reason { get; }
        public bool AreMopsUnavailable { get; }
        public IReadOnlyList<EZoneId> Candidates { get; }
        public bool IsOrcHelp { get; }
        public CancellationToken Cancellation { get; }

        public ProposalRequest(int id, EWorkerType worker, int assignmentId, EZoneId zone,
            EProposalReason reason, bool areMopsUnavailable, IReadOnlyList<EZoneId> candidates = null,
            bool isOrcHelp = false, CancellationToken cancellation = default)
        {
            Id = id;
            Worker = worker;
            AssignmentId = assignmentId;
            Zone = zone;
            Reason = reason;
            AreMopsUnavailable = areMopsUnavailable;
            Candidates = candidates ?? new[] { zone };
            IsOrcHelp = isOrcHelp;
            Cancellation = cancellation;
        }
    }

    internal readonly struct ProposalReply
    {
        public EZoneId Zone { get; }
        public string Text { get; }

        public ProposalReply(EZoneId zone, string text)
        {
            Zone = zone;
            Text = text;
        }
    }

    internal interface IProposalDialogueProvider
    {
        void Request(ProposalRequest request, Action<ProposalReply> reply);
    }

    internal sealed class DefaultProposalDialogueProvider : IProposalDialogueProvider
    {
        public void Request(ProposalRequest request, Action<ProposalReply> reply)
        {
            reply(new ProposalReply(request.Zone, Format(request)));
        }

        internal static string Format(ProposalRequest request) => Format(request, request.Zone, false);

        internal static string Format(ProposalRequest request, EZoneId zone, bool useAlternate)
        {
            string room = ZoneDisplayName.GetFullName(zone);
            if (request.IsOrcHelp)
            {
                return useAlternate
                    ? $"대장, 오크가 {room} 정리를 도와 달랬어. 다녀와도 돼?"
                    : $"오크가 {room}에서 정리할 걸 봤대. 나 거기 가서 도와줘도 돼?";
            }
            if (request.AreMopsUnavailable) { return $"지금 빌릴 걸레가 없어. {room}부터 살펴볼까?"; }

            if (useAlternate)
            {
                return request.Worker switch
                {
                    EWorkerType.Goblin => $"대장! 이번엔 {room}에 가 봐도 돼?",
                    EWorkerType.Orc => $"다음은 {room}이다. 가도 되나?",
                    EWorkerType.Skeleton => $"다음은 {room}으로 이동해도 괜찮겠습니까?",
                    EWorkerType.Slime => $"꿀렁, 이번엔 {room}!",
                    _ => $"{room}으로 가도 될까요?"
                };
            }

            return request.Worker switch
            {
                EWorkerType.Goblin => $"대장, {room}부터 가 볼까?",
                EWorkerType.Orc => $"{room}, 보내 줘. 직접 확인할게.",
                EWorkerType.Skeleton => $"다음 목표는 {room}입니다. 확인해 볼까요?",
                EWorkerType.Slime => $"꿀렁! {room}, 가 볼까?",
                _ => $"{room}, 살펴볼까요?"
            };
        }
    }
}
