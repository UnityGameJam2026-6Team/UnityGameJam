using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.UI;

namespace UnityGameJam.Test.View
{
    public sealed class MinionBottomHudTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string HudPath = "Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab";

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
        public void 복귀_통보는_재배정되어도_메시지_도착_당시_담당_방을_표시한다()
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
                var notice = new MessageEntry(11, EMessageKind.Notice, EWorkerType.Goblin, "로비로 돌아갈게.", null, 0f, 4f, false);
                var zone = (EZoneId?)report.Invoke(stack, new object[] { notice, 0 });
                remember.Invoke(stack, new object[] { new[]
                {
                    new MinionStatus(EWorkerType.Goblin, EMinionActivity.MovingToRoom, EZoneId.Floor2Lobby, null, false)
                } });

                Assert.That(zone, Is.EqualTo(EZoneId.Floor1Lobby));
                Assert.That(report.Invoke(stack, new object[] { notice, 0 }), Is.EqualTo(zone));
                string dialogue = (string)format.Invoke(null, new object[] { notice.Text, zone });
                Assert.That(dialogue, Does.Contain(ZoneDisplayName.GetFullName(EZoneId.Floor1Lobby)));
                Assert.That(dialogue, Does.EndWith(notice.Text));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
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

    }
}
