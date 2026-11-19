using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.UI;

namespace UnityGameJam.Test
{
    public sealed class AssignmentPanelTest
    {
        [Test]
        public void 시설_HUD가_배정_패널의_제목과_현재_담당_방_텍스트를_참조한다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab");
            var presenter = new SerializedObject(hud.GetComponent<FacilityHud>());
            var panel = (GameObject)presenter.FindProperty("_choices").objectReferenceValue;

            AssertTextReference(presenter, "_assignmentTitle", panel.transform, "Choice/Title");
            AssertTextReference(presenter, "_assignmentRoom", panel.transform, "Choice/CurrentRoom");
        }

        [Test]
        public void 대화는_상단_이름표와_하단_명령을_표시하고_방향키_안내는_제거한다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab");
            var presenter = new SerializedObject(hud.GetComponent<FacilityHud>());
            var panel = (GameObject)presenter.FindProperty("_choices").objectReferenceValue;
            var rect = (RectTransform)panel.transform;
            Assert.That(rect.anchorMin.y, Is.Zero);
            Assert.That(rect.anchorMax.y, Is.EqualTo(1f));
            Assert.That(((RectTransform)rect.Find("Dialogue")).anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(((RectTransform)rect.Find("Choice")).anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(((RectTransform)rect.Find("HellhoundChoices")).anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(rect.Find("Choice/KeyboardHints"), Is.Null);
        }

        [Test]
        public void 헬하운드_대화의_배출과_취소는_방_목록과_분리되고_서로_이동할_수_있다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab");
            var presenter = new SerializedObject(hud.GetComponent<FacilityHud>());
            var panel = (GameObject)presenter.FindProperty("_choices").objectReferenceValue;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_AssignmentPanel.prefab");
            var controls = panel.GetComponent<AssignmentPanelChoices>();
            Assert.That(controls, Is.Not.Null);
            Assert.AreEqual(source.GetComponent<AssignmentPanelChoices>(), PrefabUtility.GetCorrespondingObjectFromSource(controls));
            var bindings = new SerializedObject(controls);
            var choices = (GameObject)bindings.FindProperty("_hellhoundChoices").objectReferenceValue;
            var rooms = (GameObject)bindings.FindProperty("_roomChoices").objectReferenceValue;
            var dispatch = (Component)bindings.FindProperty("_dispatch").objectReferenceValue;
            var cancel = (Component)bindings.FindProperty("_cancelDispatch").objectReferenceValue;

            Assert.IsFalse(choices.activeSelf);
            Assert.IsFalse(choices.transform.IsChildOf(rooms.transform));
            Assert.IsTrue(dispatch.transform.IsChildOf(choices.transform));
            Assert.IsTrue(cancel.transform.IsChildOf(choices.transform));
            Assert.AreEqual(cancel, new SerializedObject(dispatch).FindProperty("m_Navigation.m_SelectOnRight").objectReferenceValue);
            Assert.AreEqual(dispatch, new SerializedObject(cancel).FindProperty("m_Navigation.m_SelectOnLeft").objectReferenceValue);
        }

        private static void AssertTextReference(SerializedObject presenter, string propertyName, Transform panel, string childName)
        {
            Transform label = panel.Find(childName);
            Assert.That(label, Is.Not.Null, childName);
            Component text = label.GetComponent("TextMeshProUGUI");
            Assert.That(text, Is.Not.Null, childName);
            SerializedProperty property = presenter.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.EqualTo(text), propertyName);
        }
    }
}
