using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityGameJam.Editor.Art;
using UnityGameJam.View;

namespace UnityGameJam.Test.Art
{
    public sealed class CharacterAnimationSetTest
    {
        [TestCase("CH_Player", "Idle_12", "Walking", "Female_Bend_Over_Pick_Up_Inspect")]
        [TestCase("CH_Goblin", "Idle_11", "Walking", "Female_Crouch_Pick_Up_Place_Side")]
        [TestCase("CH_Orc", "Idle_3", "Walking", "Collect_Object")]
        [TestCase("CH_Skeleton", "Idle_6", "Walking", "Idle_6")]
        [TestCase("CH_Slime", "Idle", "Idle", null)]
        [TestCase("CH_Hellhound", "dog_idle", "dog_run", null)]
        [TestCase("CH_DemonKing", "Idle", "Walking", null)]
        public void 유닛_프리팹에_자기_FBX의_행동_클립이_연결된다(string role, string idle, string walk, string pickup)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBinder.DefaultPrefabRoot + "/Character/" + role + ".prefab");
            ArtSlot slot = prefab.GetComponentInChildren<ArtSlot>(true);
            CharacterAnimationSet set = slot.AnimationSet;
            Assert.NotNull(set);
            Assert.NotNull(prefab.GetComponent<CharacterAnimatorDriver>());
            Assert.NotNull(slot.Animator);
            var controller = slot.Animator.runtimeAnimatorController as AnimatorController;
            Assert.NotNull(controller);
            Assert.AreEqual($"AC_{role}", controller.name);
            string source = ArtBinder.GetAppliedSourcePath(slot);
            Assert.AreEqual(".fbx", Path.GetExtension(source));
            Assert.AreEqual(idle, EmbeddedCharacterAnimationBinder.GetMotion(controller, ECharacterAnimation.Idle)?.name);
            Assert.AreEqual(walk, EmbeddedCharacterAnimationBinder.GetMotion(controller, ECharacterAnimation.Walk)?.name);
            Assert.AreEqual(pickup, EmbeddedCharacterAnimationBinder.GetMotion(controller, ECharacterAnimation.PickUp)?.name);
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null) { Assert.AreEqual(source, AssetDatabase.GetAssetPath(clip), clip.name); }
            }
        }

        [TestCase("CH_Player", "Female_Crouch_Pick_Throw_Forward", true)]
        [TestCase("CH_Goblin", null, false)]
        [TestCase("CH_Orc", "Collect_Object", true)]
        [TestCase("CH_Skeleton", null, false)]
        public void 청소_유닛은_지정된_걸레와_운반_클립_및_집기_구간을_쓴다(string role, string heavyPickUp, bool leftHand)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBinder.DefaultPrefabRoot + "/Character/" + role + ".prefab");
            ArtSlot slot = prefab.GetComponentInChildren<ArtSlot>();
            CharacterAnimationSet set = slot.AnimationSet;
            AnimationClip GetMotion(ECharacterAnimation state) =>
                EmbeddedCharacterAnimationBinder.GetMotion(slot.Animator.runtimeAnimatorController, state);
            Assert.AreEqual("Running", GetMotion(ECharacterAnimation.Run)?.name);
            Assert.AreEqual(role == "CH_Skeleton" ? "_01a1ed96_6620_7713_9bb7_f6a44c873f65" : "_01a1ed95_d307_7701_a839_04051b7ec198", GetMotion(ECharacterAnimation.MopWalk)?.name);
            Assert.AreEqual("_01a1ed94_4b92_72e8_bf78_42f661410eaf", GetMotion(ECharacterAnimation.Mop)?.name);
            Assert.AreEqual(heavyPickUp, GetMotion(ECharacterAnimation.PickUpHeavy)?.name);
            Assert.AreEqual(leftHand, set.LeftHandCarry);
            if (role != "CH_Skeleton") { Assert.AreEqual("Spear_Walk", GetMotion(ECharacterAnimation.Carry)?.name); }
            if (heavyPickUp != null)
            {
                Assert.AreEqual("Carry_Heavy_Object_Walk_inplace", GetMotion(ECharacterAnimation.CarryHeavy)?.name);
                Assert.Greater(set.PickUpHeavyEnd, set.PickUpHeavyContact);
            }
            Assert.Greater(set.PickUpEnd, set.PickUpContact);
            Assert.LessOrEqual(set.PickUpEnd, GetMotion(ECharacterAnimation.PickUp).length);
            Assert.Greater(set.WalkSpeed, 0f);
            Assert.Greater(set.RunSpeed, 0f);
        }

        [TestCase("CH_Slime", "EAT")]
        [TestCase("CH_Hellhound", "dog_run")]
        public void 특수_졸개의_먹기와_배출에_전용_클립이_연결된다(string role, string clipName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBinder.DefaultPrefabRoot + "/Character/" + role + ".prefab");
            var controller = prefab.GetComponentInChildren<ArtSlot>().Animator.runtimeAnimatorController;
            ECharacterAnimation state = role == "CH_Slime" ? ECharacterAnimation.Eat : ECharacterAnimation.Dispatch;
            Assert.AreEqual(clipName, EmbeddedCharacterAnimationBinder.GetMotion(controller, state)?.name);
        }
    }
}
