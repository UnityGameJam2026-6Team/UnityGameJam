using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.UI;

namespace UnityGameJam.Test.View
{
    public sealed class FacilityProgressHudTest
    {
        [Test]
        public void 시설_진척도는_타이머_아래의_읽기_전용_슬라이더에_표시된다()
        {
            const string path = "Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab";
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var presenter = new SerializedObject(hud.GetComponent<FacilityHud>());
            var label = (Component)presenter.FindProperty("_facilityProgress").objectReferenceValue;
            Assert.That(label, Is.Not.Null);
            var panel = (RectTransform)label.transform.parent;
            Component slider = panel.GetComponent("Slider");
            Assert.That(slider, Is.Not.Null);
            var sliderProperties = new SerializedObject(slider);
            Assert.That(sliderProperties.FindProperty("m_Interactable").boolValue, Is.False);
            Assert.That(sliderProperties.FindProperty("m_MinValue").floatValue, Is.Zero);
            Assert.That(sliderProperties.FindProperty("m_MaxValue").floatValue, Is.EqualTo(100f));
            Assert.That(sliderProperties.FindProperty("m_FillRect").objectReferenceValue, Is.Not.Null);

            var timer = (RectTransform)hud.transform.Find("DemonKingCountdown/HUD");
            Assert.That(panel.anchorMin, Is.EqualTo(timer.anchorMin));
            Assert.That(panel.anchorMax, Is.EqualTo(timer.anchorMax));
            float timerBottom = timer.anchoredPosition.y - timer.sizeDelta.y * timer.pivot.y * timer.localScale.y;
            float panelTop = panel.anchoredPosition.y + panel.sizeDelta.y * (1f - panel.pivot.y) * panel.localScale.y;
            Assert.That(panelTop, Is.LessThan(timerBottom));
        }
    }
}
