using System;
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

        public ProposalRequest(int id, EWorkerType worker, int assignmentId, EZoneId zone,
            EProposalReason reason, bool areMopsUnavailable)
        {
            Id = id;
            Worker = worker;
            AssignmentId = assignmentId;
            Zone = zone;
            Reason = reason;
            AreMopsUnavailable = areMopsUnavailable;
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

        internal static string Format(ProposalRequest request)
        {
            string room = ZoneDisplayName.GetFullName(request.Zone);
            if (request.AreMopsUnavailable) { return $"지금 빌릴 걸레가 없어. {room}부터 살펴볼까?"; }

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
