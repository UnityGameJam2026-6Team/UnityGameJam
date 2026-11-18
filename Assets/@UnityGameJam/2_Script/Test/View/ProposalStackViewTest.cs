using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityGameJam.Test
{
    public sealed class ProposalStackViewTest
    {
        [Test]
        public void 시설_HUD에_제안_스택과_응답_입력이_연결되어_있다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab");
            Transform stack = hud.transform.Find("UI_ProposalStack");
            Assert.That(stack, Is.Not.Null);
            var component = stack.GetComponent("ProposalStackView");
            Assert.That(component, Is.Not.Null);
            var serialized = new SerializedObject(component);
            Assert.That(serialized.FindProperty("_template").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_approve").objectReferenceValue.name, Is.EqualTo("Player/Approve"));
            Assert.That(serialized.FindProperty("_hold").objectReferenceValue.name, Is.EqualTo("Player/Hold"));
            var presenter = new SerializedObject(hud.GetComponent("FacilityHud"));
            Assert.That(presenter.FindProperty("_proposalStack").objectReferenceValue, Is.EqualTo(component));
        }
    }
}
