using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.UI;

namespace UnityGameJam.Test
{
    public sealed class ResultPanelTest
    {
        private const string Prefabs = "Assets/@UnityGameJam/4_Prefab/";
        private const string PanelPath = Prefabs + "UI/UI_ResultPanel.prefab";

        [Test]
        public void 시설_HUD의_마감_연출이_마왕과_숨겨진_결과_패널을_참조한다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "UI/UI_HudRoot.prefab");
            var demonKing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Character/CH_DemonKing.prefab");
            Assert.That(demonKing, Is.Not.Null);
            var arrival = hud.GetComponent<DemonKingArrivalView>();
            Assert.That(arrival, Is.Not.Null);
            var serialized = new SerializedObject(arrival);
            Assert.That(serialized.FindProperty("_demonKingPrefab").objectReferenceValue, Is.EqualTo(demonKing));
            var panel = (Component)serialized.FindProperty("_resultPanel").objectReferenceValue;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.transform.IsChildOf(hud.transform), Is.True);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject), Is.EqualTo(PanelPath));
            Assert.That(panel.gameObject.activeSelf, Is.False);
            var resultPanel = new SerializedObject(panel);
            Assert.That(resultPanel.FindProperty("_restartButton").objectReferenceValue, Is.Not.Null);
            Assert.That(resultPanel.FindProperty("_restartAction").objectReferenceValue.name, Is.EqualTo("UI/Restart"));
        }

        [TestCase(85f, 60f, 85f, 60f, true)]
        [TestCase(90f, 59.99f, 85f, 60f, false)]
        [TestCase(84.99f, 60f, 85f, 60f, false)]
        [TestCase(88f, 64f, 88f, 64f, true)]
        public void 결과가_시설과_아홉_구역의_최종_점수와_판정을_층_순으로_표시한다(
            float facility, float lobby, float facilityThreshold, float zoneThreshold, bool isSuccess)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
                var scores = new Dictionary<EZoneId, float>();
                for (int i = zones.Length - 1; i >= 0; i--)
                {
                    scores.Add(zones[i], zones[i] == EZoneId.Floor3Lobby ? lobby : zoneThreshold);
                }
                var result = new MatchResult(isSuccess, facility, scores);
                var panel = instance.GetComponent<ResultPanel>();
                Assert.That(panel, Is.Not.Null);
                panel.Show(result, facilityThreshold, zoneThreshold);
                Assert.That(instance.activeSelf, Is.True);
                var serialized = new SerializedObject(panel);
                Assert.That(ReadText(serialized.FindProperty("_verdict")), Is.EqualTo(isSuccess ? "성공" : "실패"));
                AssertRow(serialized.FindProperty("_facility"), "시설 전체", facility, facilityThreshold);

                SerializedProperty rows = serialized.FindProperty("_zones");
                Assert.That(rows.arraySize, Is.EqualTo(9));
                for (int i = 0; i < zones.Length; i++)
                {
                    EZoneId zone = zones[i];
                    AssertRow(rows.GetArrayElementAtIndex(i), ZoneDisplayName.GetFullName(zone), scores[zone], zoneThreshold);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static string ReadText(SerializedProperty property)
        {
            UnityEngine.Object label = property.objectReferenceValue;
            Assert.That(label, Is.Not.Null, property.propertyPath);
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }

        private static void AssertRow(SerializedProperty row, string name, float score, float threshold)
        {
            string text = ReadText(row);
            Assert.That(text, Does.StartWith(name));
            Assert.That(text, Does.Contain($"{Mathf.FloorToInt(score)}% / {threshold}%"));
            Assert.That(text, Does.Contain(score >= threshold ? "통과" : "미달"));
        }
    }
}
