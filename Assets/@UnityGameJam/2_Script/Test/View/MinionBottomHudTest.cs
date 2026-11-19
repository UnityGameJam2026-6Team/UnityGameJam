using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Proposal;
using UnityGameJam.UI;

namespace UnityGameJam.Test.View
{
    public sealed class MinionBottomHudTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string HudPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab";

        [Test]
        public void 캔버스_그룹이_없는_카드도_통보와_제안을_다시_표시한다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var stack = hud.GetComponentInChildren<ProposalStackView>(true);
            var template = (ProposalCard)new SerializedObject(stack).FindProperty("_template").objectReferenceValue;
            var card = UnityEngine.Object.Instantiate(template);
            try
            {
                if (card.TryGetComponent(out CanvasGroup existing)) { UnityEngine.Object.DestroyImmediate(existing); }
                card.gameObject.SetActive(true);
                var notice = new MessageEntry(1, EMessageKind.Notice, EWorkerType.Goblin, "오크, 방 상황을 알려줄게.", null, 0f, 4f, false);
                card.Show(notice, null, 0f, false, 0f);
                Assert.That(card.TryGetComponent(out CanvasGroup opacity), Is.True);
                Assert.That(opacity.alpha, Is.Zero);

                var proposal = new MessageEntry(2, EMessageKind.Proposal, EWorkerType.Orc, "작업실에 가도 될까?",
                    EZoneId.Workshop, 1f, 7f, true);
                card.Show(proposal, null, 1f, true, 0f);
                Assert.That(opacity.alpha, Is.EqualTo(1f));
                Assert.That(card.GetComponents<CanvasGroup>(), Has.Length.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void 하단_현황은_헬하운드를_제외한_4명을_타입으로_연결한다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var panel = hud.GetComponentInChildren<MinionStatusPanel>(true);
            var slots = new SerializedObject(panel).FindProperty("_slots");
            Assert.That(slots.arraySize, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                Assert.That(slot.FindPropertyRelative("_worker").intValue, Is.EqualTo(i + 1));
                foreach (string field in new[] { "_name", "_room", "_activity" })
                {
                    Assert.That(slot.FindPropertyRelative(field).objectReferenceValue, Is.Not.Null, field);
                }
            }
        }

        [Test]
        public void 방_정보가_발화자_칸에서_제안_뒤에_사분의_일만_보인다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_ProposalStack.prefab");
            var root = new GameObject("Hud", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(1920f, 1080f);
                var stack = UnityEngine.Object.Instantiate(prefab, root.transform).GetComponent<ProposalStackView>();
                typeof(ProposalStackView).GetMethod("Awake", Private).Invoke(stack, null);
                var session = new StubSession();
                var queue = new ProposalQueue(7f, 4f, 2f, new DefaultProposalDialogueProvider());
                queue.Request(EWorkerType.Orc, 1, EZoneId.Workshop, EProposalReason.OtherWorker, false);
                queue.Tick(0f);
                queue.AddRoomReport(EWorkerType.Orc, EWorkerType.Goblin, 1f);
                queue.Tick(1.5f);
                session.Messages.AddRange(queue.Messages);
                session.Statuses.Add(new MinionStatus(EWorkerType.Orc, EMinionActivity.Working, EZoneId.Storage, null, false));
                stack.Show(session, session.Minions, 1.5f, null, false);

                Assert.That(stack.transform.Find("SharedNotices").gameObject.activeSelf, Is.False);
                Assert.That(stack.transform.Find("GoblinMessages").gameObject.activeSelf, Is.False);
                var lane = stack.transform.Find("OrcMessages").GetComponent<UnityEngine.UI.ScrollRect>();
                var front = (RectTransform)lane.content.GetChild(1);
                var back = (RectTransform)lane.content.GetChild(0);
                Assert.That(front.GetComponent<ProposalCard>().MessageId, Is.EqualTo(session.Messages[0].MessageId));
                Assert.That(back.GetComponent<ProposalCard>().MessageId, Is.EqualTo(session.Messages[1].MessageId));
                Assert.That(CardText(back.GetComponent<ProposalCard>(), "_title"), Is.EqualTo("오크"));
                Assert.That(CardText(back.GetComponent<ProposalCard>(), "_dialogue"), Is.EqualTo(session.Messages[1].Text));
                Assert.That(back.Find("ApproveButton").gameObject.activeSelf, Is.False);
                Assert.That(front.Find("ApproveButton").gameObject.activeSelf, Is.True);
                float frontTop = front.anchoredPosition.y + front.rect.yMax;
                float backTop = back.anchoredPosition.y + back.rect.yMax;
                Assert.That(backTop - frontTop, Is.EqualTo(back.rect.height * .25f).Within(.01f));
                Assert.That(((RectTransform)lane.transform).rect.height,
                    Is.EqualTo(front.rect.height + back.rect.height * .25f).Within(.01f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 상태_리스트_순서가_바뀌어도_각_졸개의_행동이_맞고_빈_정보는_초기화된다()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_MinionStatusPanel.prefab");
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                var panel = root.GetComponent<MinionStatusPanel>();
                typeof(MinionStatusPanel).GetMethod("OnEnable", Private).Invoke(panel, null);
                panel.Show(new[] {
                    new MinionStatus(EWorkerType.Hellhound, EMinionActivity.Dispatching, null, null, false),
                    new MinionStatus(EWorkerType.Slime, EMinionActivity.Working, null, null, false),
                    new MinionStatus(EWorkerType.Goblin, EMinionActivity.AwaitingResponse, null, null, false)
                });
                Assert.That(ReadActivity(panel, 0), Is.EqualTo("응답 대기"));
                Assert.That(ReadActivity(panel, 3), Is.EqualTo("작업 중"));
                Assert.That(ReadActivity(panel, 1), Is.EqualTo("정보 없음"));
                panel.Show(Array.Empty<MinionStatus>());
                Assert.That(ReadActivity(panel, 0), Is.EqualTo("정보 없음"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 탭으로_상세만_접고_제목과_메시지_스택을_유지한다()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                var panel = root.GetComponentInChildren<MinionStatusPanel>();
                var stack = root.GetComponentInChildren<ProposalStackView>();
                var serialized = new SerializedObject(panel);
                var body = (RectTransform)serialized.FindProperty("_body").objectReferenceValue;
                var title = (Component)serialized.FindProperty("_title").objectReferenceValue;
                Assert.That(serialized.FindProperty("_toggle").objectReferenceValue.name, Is.EqualTo("Player/ToggleMinionPanel"));
                typeof(MinionStatusPanel).GetMethod("Awake", Private).Invoke(panel, null);
                float expandedHeight = ((RectTransform)panel.transform).rect.height;
                var toggle = typeof(MinionStatusPanel).GetMethod("OnToggle", Private);
                var context = new object[] { null };

                toggle.Invoke(panel, context);
                Assert.That(body.gameObject.activeSelf, Is.False);
                Assert.That(title.gameObject.activeInHierarchy, Is.True);
                Assert.That(stack.gameObject.activeInHierarchy, Is.True);
                Assert.That(((RectTransform)panel.transform).rect.height, Is.LessThan(expandedHeight));

                toggle.Invoke(panel, context);
                Assert.That(body.gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)panel.transform).rect.height, Is.EqualTo(expandedHeight));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 작업_제안은_재배정되어도_메시지_도착_당시_담당_방을_표시한다()
        {
            var obj = new GameObject("Notice", typeof(RectTransform), typeof(ProposalStackView));
            try
            {
                var stack = obj.GetComponent<ProposalStackView>();
                var remember = typeof(ProposalStackView).GetMethod("RememberAssignments", Private);
                var report = typeof(ProposalStackView).GetMethod("ReportedZone", Private);
                var format = typeof(ProposalCard).GetMethod("FormatDialogue", BindingFlags.Static | BindingFlags.NonPublic);
                remember.Invoke(stack, new object[] { new[]
                {
                    new MinionStatus(EWorkerType.Goblin, EMinionActivity.Working, EZoneId.Floor1Lobby, null, false)
                } });
                var proposal = new MessageEntry(11, EMessageKind.Proposal, EWorkerType.Goblin, "작업실에 가도 될까?",
                    EZoneId.Workshop, 0f, 7f, false);
                var zone = (EZoneId?)report.Invoke(stack, new object[] { proposal, 0 });
                remember.Invoke(stack, new object[] { new[]
                {
                    new MinionStatus(EWorkerType.Goblin, EMinionActivity.MovingToRoom, EZoneId.Floor2Lobby, null, false)
                } });

                Assert.That(zone, Is.EqualTo(EZoneId.Floor1Lobby));
                Assert.That(report.Invoke(stack, new object[] { proposal, 0 }), Is.EqualTo(zone));
                string dialogue = (string)format.Invoke(null, new object[] { proposal.Text, zone });
                Assert.That(dialogue, Does.Contain(ZoneDisplayName.GetFullName(EZoneId.Floor1Lobby)));
                Assert.That(dialogue, Does.EndWith(proposal.Text));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        private static string CardText(ProposalCard card, string field)
        {
            var label = new SerializedObject(card).FindProperty(field).objectReferenceValue;
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }

        private static string ReadActivity(MinionStatusPanel panel, int index)
        {
            var property = new SerializedObject(panel).FindProperty("_slots").GetArrayElementAtIndex(index).FindPropertyRelative("_activity");
            var component = property.objectReferenceValue;
            return (string)component.GetType().GetProperty("text").GetValue(component);
        }

        [Test]
        public void 발신_메시지_레인_및_승인_보류_버튼이_프리팹에_연결된다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var stack = hud.GetComponentInChildren<ProposalStackView>(true);
            var so = new SerializedObject(stack);
            var lanes = so.FindProperty("_lanes");
            Assert.That(lanes.arraySize, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                var lane = (Component)lanes.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(lane, Is.Not.Null);
                var laneSo = new SerializedObject(lane);
                Assert.That(laneSo.FindProperty("m_Content").objectReferenceValue, Is.Not.Null);
                Assert.That(laneSo.FindProperty("m_Viewport").objectReferenceValue, Is.Not.Null);
            }
            var template = new SerializedObject(so.FindProperty("_template").objectReferenceValue);
            Assert.That(template.FindProperty("_approveButton").objectReferenceValue, Is.Not.Null);
            Assert.That(template.FindProperty("_holdButton").objectReferenceValue, Is.Not.Null);
            var map = typeof(ProposalStackView).GetMethod("LaneIndex", BindingFlags.Static | BindingFlags.NonPublic);
            for (int i = 1; i <= 5; i++) { Assert.That(map.Invoke(null, new object[] { (EWorkerType)i }), Is.EqualTo(i - 1)); }
        }

        [Test]
        public void 만료_이전_카드나_대화중_응답은_다른_제안을_승인하지_않는다()
        {
            var obj = new GameObject("ResponseGuard", typeof(RectTransform), typeof(ProposalStackView));
            try
            {
                var stack = obj.GetComponent<ProposalStackView>();
                var session = new StubSession();
                typeof(ProposalStackView).GetField("_session", Private).SetValue(stack, session);
                var respond = typeof(ProposalStackView).GetMethod("Respond", Private);
                session.Messages.Add(new MessageEntry(2, EMessageKind.Proposal, EWorkerType.Goblin, "테스트", EZoneId.Floor1Lobby, 0f, 10f, true));
                respond.Invoke(stack, new object[] { 1, true });
                Assert.That(session.Approvals, Is.Zero, "stale MessageId");
                session.Statuses.Add(new MinionStatus(EWorkerType.Orc, EMinionActivity.Talking, null, null, false));
                respond.Invoke(stack, new object[] { 2, true });
                Assert.That(session.Approvals, Is.Zero, "face-to-face conversation blocks response");
                session.Statuses.Clear();
                respond.Invoke(stack, new object[] { 2, true });
                Assert.That(session.Approvals, Is.EqualTo(1));
                respond.Invoke(stack, new object[] { 2, true });
                Assert.That(session.Approvals, Is.EqualTo(1), "double click must not approve next message");
                session.Messages.Add(new MessageEntry(3, EMessageKind.Proposal, EWorkerType.Slime, "테스트", null, 0f, 0f, true));
                respond.Invoke(stack, new object[] { 3, false });
                Assert.That(session.Holds, Is.Zero, "expired proposal");
                session.Messages.Clear();
                session.Messages.Add(new MessageEntry(4, EMessageKind.Proposal, EWorkerType.Slime, "테스트", null, 0f, 10f, true));
                respond.Invoke(stack, new object[] { 4, false });
                Assert.That(session.Holds, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        private sealed class StubSession : IMatchSession
        {
            internal readonly List<MinionStatus> Statuses = new List<MinionStatus>();
            internal readonly List<MessageEntry> Messages = new List<MessageEntry>();
            internal int Approvals;
            internal int Holds;
            public EMatchState MatchState => EMatchState.Running;
            public IReadOnlyList<MinionStatus> Minions => Statuses;
            public IReadOnlyList<MessageEntry> ActiveMessages => Messages;
            public float RemainingTime => 60f;
            public float ZoneThreshold => 60f;
            public float FacilityThreshold => 85f;
            public float FacilityCleanliness => 0f;
            public EZoneId? PlayerZone => null;
            public int? PlayerCarriedItemId => null;
            public bool IsPlayerHoldingMop => false;
            public float PlayerMoveSpeed => 1f;
            public bool IsPlayerWorking => false;
            public float PlayerWorkProgress => 0f;
            public IReadOnlyList<ItemSnapshot> Items => Array.Empty<ItemSnapshot>();
            public MatchResult Result => null;
            public event Action<ItemSnapshot> ItemChanged { add { } remove { } }
            public event Action MessagesChanged { add { } remove { } }
            public event Action<EMatchState> MatchStateChanged { add { } remove { } }
            public float GetZoneCleanliness(EZoneId zone) => 0f;
            public void StartMatch() { }
            public void Tick(float deltaTime) { }
            public bool BeginTalk(EWorkerType minion) => false;
            public void CancelTalk(EWorkerType minion) { }
            public bool AssignRoom(EWorkerType minion, EZoneId zone) => false;
            public bool ApproveOldestProposal() { Approvals++; Messages.RemoveAt(0); return true; }
            public bool HoldOldestProposal() { Holds++; Messages.RemoveAt(0); return true; }
            public bool DispatchHellhound() => false;
            public bool TryPickUp(int itemId) => false;
            public bool TryDrop() => false;
            public bool TryPlaceAtHome() => false;
            public bool TryStartMop(int itemId) => false;
            public void CancelWork() { }
            public bool TryBorrowMop(EFloor floor) => false;
            public bool TryReturnMop() => false;
        }
    }
}
