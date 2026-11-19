using System;
using System.Collections.Generic;
using System.Threading;
using UnityGameJam.Contract;

namespace UnityGameJam.Proposal
{
    internal enum EProposalReason { KnownWork, Explore, OtherWorker }

    internal readonly struct CollaborationPlan
    {
        public readonly (EZoneId Zone, int ItemId, EContaminationType Type) OwnTask;
        public readonly (EZoneId Zone, int ItemId, EContaminationType Type) PartnerTask;
        public readonly EWorkerType Partner;
        public readonly int PartnerAssignmentId;
        public readonly int FloorChanges;
        public readonly float EstimatedSeconds;

        public EProposalKind Kind => Partner == EWorkerType.Player ? EProposalKind.PlayerAssistance : EProposalKind.MinionCollaboration;

        public CollaborationPlan((EZoneId Zone, int ItemId, EContaminationType Type) ownTask,
            (EZoneId Zone, int ItemId, EContaminationType Type) partnerTask, EWorkerType partner, int partnerAssignmentId,
            int floorChanges, float estimatedSeconds)
        {
            OwnTask = ownTask;
            PartnerTask = partnerTask;
            Partner = partner;
            PartnerAssignmentId = partnerAssignmentId;
            FloorChanges = floorChanges;
            EstimatedSeconds = estimatedSeconds;
        }
    }

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
        public IReadOnlyDictionary<EZoneId, EWorkerType> Coworkers { get; }
        public IReadOnlyList<CollaborationPlan> Plans { get; }
        public CollaborationPlan? SelectedPlan { get; }
        public float? RemainingSeconds { get; }
        public EContaminationType? PreferredTask { get; }

        public ProposalRequest(int id, EWorkerType worker, int assignmentId, EZoneId zone,
            EProposalReason reason, bool areMopsUnavailable, IReadOnlyList<EZoneId> candidates = null,
            bool isOrcHelp = false, CancellationToken cancellation = default,
            IReadOnlyDictionary<EZoneId, EWorkerType> coworkers = null, IReadOnlyList<CollaborationPlan> plans = null,
            CollaborationPlan? selectedPlan = null, float? remainingSeconds = null, EContaminationType? preferredTask = null)
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
            Coworkers = coworkers;
            Plans = plans ?? Array.Empty<CollaborationPlan>();
            SelectedPlan = selectedPlan;
            RemainingSeconds = remainingSeconds;
            PreferredTask = preferredTask;
        }
    }

    internal readonly struct ProposalReply
    {
        public EZoneId Zone { get; }
        public string Text { get; }

        public CollaborationPlan? Plan { get; }

        public ProposalReply(EZoneId zone, string text, CollaborationPlan? plan = null)
        {
            Plan = plan;
            Zone = zone;
            Text = text;
        }
    }

    internal interface IProposalDialogueProvider
    {
        void Request(ProposalRequest request, Action<ProposalReply> reply);
    }

    internal interface ITaskPreferenceProvider
    {
        void RequestTaskPreferences(IReadOnlyDictionary<EWorkerType, EContaminationType[]> allowed, CancellationToken cancellation,
            Action<IReadOnlyDictionary<EWorkerType, EContaminationType>> reply);
    }

    internal sealed class DefaultProposalDialogueProvider : IProposalDialogueProvider
    {
        internal static string SkillReaction(EWorkerType worker, ECommandSkill skill) => (worker, skill) switch
        {
            (EWorkerType.Goblin, ECommandSkill.Shout) => "응, 대장! 지금 갈게!",
            (EWorkerType.Orc, ECommandSkill.Shout) => "알았다. 간다.",
            (EWorkerType.Skeleton, ECommandSkill.Shout) => "부르셨습니까? 곧 가겠습니다.",
            (EWorkerType.Slime, ECommandSkill.Shout) => "꿀렁! 나도 갈게, 대장.",
            (EWorkerType.Goblin, _) => "우와, 대장! 발이 저절로 움직여!",
            (EWorkerType.Orc, _) => "좋다. 더 빨리 끝내지.",
            (EWorkerType.Skeleton, _) => "몸이 가벼워졌습니다. 꼼꼼히 마무리하겠습니다.",
            (EWorkerType.Slime, _) => "꿀렁! 오늘따라 몸이 가볍네.",
            _ => "멍!",
        };

        public void Request(ProposalRequest request, Action<ProposalReply> reply)
        {
            reply(Fallback(request));
        }

        internal static ProposalReply Fallback(ProposalRequest request) => request.Plans.Count > 0
            ? CollaborationReply(request.Worker, request.Plans[0], false)
            : new ProposalReply(request.Zone, Format(request, request.Zone, false));

        internal static ProposalReply CollaborationReply(EWorkerType worker, CollaborationPlan plan, bool alternate)
        {
            string own = WorkDescription(plan.OwnTask);
            string other = WorkDescription(plan.PartnerTask);
            string name = WorkerName(plan.Partner);
            string division = plan.Partner == EWorkerType.Player ? $"대장, {other} 부탁해." : $"{name}에게 {other} 부탁할게.";
            bool formal = worker == EWorkerType.Skeleton;
            if (formal)
            {
                division = plan.Partner == EWorkerType.Player
                    ? $"대장님, {other} 부탁드려도 되겠습니까?"
                    : $"{name}에게 {other} 부탁하겠습니다.";
            }
            string ownLine = worker switch
            {
                EWorkerType.Orc => $"{own}, 내가 맡겠다.",
                EWorkerType.Skeleton => $"{own}은 제가 맡겠습니다.",
                EWorkerType.Slime => $"꿀렁! {own}은 내가 할게.",
                _ => $"{own}은 내가 할게."
            };
            string text = alternate ? $"{division}\n{ownLine}" : $"{ownLine}\n{division}";
            return new ProposalReply(plan.OwnTask.Zone, text, plan);
        }

        internal static string WorkDescription((EZoneId Zone, int ItemId, EContaminationType Type) task)
        {
            string work = task.Type switch
            {
                EContaminationType.HeavyDebris => "무거운 잔해 정리",
                EContaminationType.Trash => "쓰레기 정리",
                EContaminationType.ClutterItem => "작은 물건 제자리 정리",
                EContaminationType.FloorStain => "바닥 얼룩 닦기",
                _ => "청소"
            };
            return $"{ZoneDisplayName.GetFullName(task.Zone)} {work}";
        }

        internal static string Format(ProposalRequest request, EZoneId zone, bool useAlternate)
        {
            string room = ZoneDisplayName.GetFullName(zone);
            if (request.IsOrcHelp)
            {
                return useAlternate
                    ? $"대장, 오크가 {room} 정리를 도와 달랬어. 다녀와도 돼?"
                    : $"오크가 {room}에서 정리할 걸 봤대. 나 거기 가서 도와줘도 돼?";
            }
            if (request.Coworkers != null && request.Coworkers.TryGetValue(zone, out EWorkerType coworker))
            {
                string name = WorkerName(coworker);
                return request.Worker switch
                {
                    EWorkerType.Goblin => useAlternate ? $"대장, {room}의 {name}한테 가 봐도 돼?" : $"{room}에 가서 {name} 도울까?",
                    EWorkerType.Orc => useAlternate ? $"{room}의 {name}, 내가 돕고 와도 되나?" : $"{room}, {name}은 내가 도울까?",
                    EWorkerType.Skeleton => useAlternate ? $"{room}의 {name}에게 합류해도 되겠습니까?" : $"{room}에서 {name}의 일을 도울까요?",
                    _ => useAlternate ? $"꿀렁! {room}에서 {name} 도울까?" : $"{room}의 {name} 옆으로 갈까?"
                };
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

        internal static string WorkerName(EWorkerType worker) => worker switch
        {
            EWorkerType.Goblin => "고블린",
            EWorkerType.Orc => "오크",
            EWorkerType.Skeleton => "스켈레톤",
            EWorkerType.Slime => "슬라임",
            _ => worker.ToString()
        };
    }
}
