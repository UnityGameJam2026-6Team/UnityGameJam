using System;
using NUnit.Framework;
using UnityGameJam.Contract;
using UnityGameJam.Proposal;

namespace UnityGameJam.Test
{
    public class ProposalQueueTest
    {
        [Test]
        public void 방_정보는_발화자의_통보로_나오고_제안만_승인할_수_있다()
        {
            var queue = new ProposalQueue(7f, 3f, 2f, new DefaultProposalDialogueProvider());
            queue.AddRoomReport(EWorkerType.Orc, EWorkerType.Goblin, 0f);
            queue.AddRoomReport(EWorkerType.Goblin, EWorkerType.Orc, .1f);
            queue.Request(EWorkerType.Skeleton, 1, EZoneId.Workshop, EProposalReason.OtherWorker, false,
                coworkers: new System.Collections.Generic.Dictionary<EZoneId, EWorkerType> { [EZoneId.Workshop] = EWorkerType.Orc });
            queue.Tick(.2f);

            Assert.That(queue.Messages.Count, Is.EqualTo(2));
            MessageEntry notice = queue.Messages[0];
            Assert.That(notice.Minion, Is.EqualTo(EWorkerType.Orc));
            Assert.That(notice.Kind, Is.EqualTo(EMessageKind.Notice));
            Assert.That(notice.Text, Does.StartWith("고블린,"));
            Assert.That(notice.IsInputTarget, Is.False);
            Assert.That(queue.Messages[1].Text, Does.Contain("오크"));
            Assert.That(queue.Messages[1].IsInputTarget, Is.True);
            Assert.That(queue.RespondOldest(true), Is.True);
            Assert.That(queue.Messages.Count, Is.EqualTo(1));
            queue.Tick(3.1f);
            Assert.That(queue.Messages, Is.Empty);
        }

        [Test]
        public void Decisions_선택값이_준비된_대사를_찾고_범위를_벗어나면_거부한다()
        {
            var choices = new[] { new ProposalReply(EZoneId.Storage, "대장, 창고로 갈까?") };
            const string valid = "{\"answers\":[{\"type\":\"choice\",\"name\":\"proposal\",\"choice\":\"0\"}]}";
            Assert.That(OpenAiProposalDialogueProvider.ParseReply(valid, choices).Text, Is.EqualTo(choices[0].Text));
            Assert.That(OpenAiProposalDialogueProvider.ParseReply(valid.Replace("\"0\"", "\"7\""), choices).Text, Is.Null);
        }

        [Test]
        public void 제안은_개별_만료되고_통보가_입력_순서에_끼지_않는다()
        {
            var queue = new ProposalQueue(7f, 20f, 2f, new DefaultProposalDialogueProvider());
            queue.AddNotice(EWorkerType.Hellhound, "배출할 폐기물이 없어.");
            queue.Request(EWorkerType.Goblin, 1, EZoneId.Lounge, EProposalReason.Explore, false);
            queue.Tick(0f);
            queue.Tick(3f);
            queue.Request(EWorkerType.Orc, 1, EZoneId.Workshop, EProposalReason.Explore, false);
            queue.Tick(3f);

            Assert.That(queue.Messages[1].IsInputTarget, Is.True);
            Assert.That(queue.Messages[2].RemainingTime, Is.EqualTo(7f));
            queue.Tick(7f);
            Assert.That(queue.Messages[0].Kind, Is.EqualTo(EMessageKind.Notice));
            Assert.That(queue.Messages[1].Minion, Is.EqualTo(EWorkerType.Orc));
            Assert.That(queue.Messages[1].RemainingTime, Is.EqualTo(3f));
        }

        [Test]
        public void 잘못된_목적지와_늦은_중복_응답은_기본_제안을_중복_표시하지_않는다()
        {
            var provider = new DelayedProvider();
            var queue = new ProposalQueue(7f, 4f, 2f, provider);
            queue.Request(EWorkerType.Goblin, 1, EZoneId.Lounge, EProposalReason.Explore, false);
            provider.Reply(new ProposalReply(EZoneId.Storage, "다른 방"));
            queue.Tick(1f);
            Assert.That(queue.Messages[0].ProposedZone, Is.EqualTo(EZoneId.Lounge));

            provider.Reply(new ProposalReply(EZoneId.Lounge, "늦은 응답"));
            queue.Tick(2f);
            Assert.That(queue.Messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void 재배정으로_취소된_응답은_표시되지_않는다()
        {
            var provider = new DelayedProvider();
            var queue = new ProposalQueue(7f, 4f, 2f, provider);
            queue.Request(EWorkerType.Goblin, 1, EZoneId.Lounge, EProposalReason.Explore, false);
            queue.Cancel(EWorkerType.Goblin);
            provider.Reply(new ProposalReply(EZoneId.Lounge, "만료된 배정"));
            queue.Tick(1f);
            Assert.That(queue.Messages, Is.Empty);
        }

        private sealed class DelayedProvider : IProposalDialogueProvider
        {
            public Action<ProposalReply> Reply { get; private set; }

            public void Request(ProposalRequest request, Action<ProposalReply> reply)
            {
                Reply = reply;
            }
        }
    }
}
