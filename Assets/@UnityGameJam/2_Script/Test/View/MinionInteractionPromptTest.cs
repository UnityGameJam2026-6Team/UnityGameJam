using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.UI;

namespace UnityGameJam.Test.View
{
    public sealed class MinionInteractionPromptTest
    {
        [Test]
        public void 말풍선은_paper_스프라이트로_연결되고_기존_바닥_표시는_제거된다()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab");
            MinionInteractionPrompt prompt = hud.GetComponentInChildren<MinionInteractionPrompt>(true);
            Assert.That(prompt, Is.Not.Null);
            var image = new SerializedObject(prompt.GetComponent("Image"));
            Assert.That(AssetDatabase.GetAssetPath(image.FindProperty("m_Sprite").objectReferenceValue),
                Is.EqualTo("Assets/@UnityGameJam/4_Prefab/UI/DemonKingSpeechBubble/Art/paper.png"));
            CanvasGroup group = prompt.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);

            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@UnityGameJam/4_Prefab/Character/CH_Player.prefab");
            Assert.That(player.transform.Find("TargetMarker"), Is.Null);
        }
    }
}
