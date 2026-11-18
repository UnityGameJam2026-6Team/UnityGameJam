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

            AssertTextReference(presenter, "_assignmentTitle", panel.transform, "Title");
            AssertTextReference(presenter, "_assignmentRoom", panel.transform, "CurrentRoom");
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
