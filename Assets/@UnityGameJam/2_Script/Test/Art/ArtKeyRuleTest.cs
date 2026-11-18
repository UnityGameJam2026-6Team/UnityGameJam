using NUnit.Framework;
using UnityGameJam.Editor.Art;

namespace UnityGameJam.Test.Art
{
    /// <summary>Spec ArtPipeline 규칙 1~5: 아트 파일 이름 규칙.</summary>
    public class ArtKeyRuleTest
    {
        [TestCase("CH_Goblin")]
        [TestCase("IT_HeavyDebris_01")]
        [TestCase("ENV_Wall_Curve")]
        [TestCase("PR_HomeSlot_Book")]
        public void 규칙에_맞는_이름만_ArtKey로_인정한다(string key)
        {
            Assert.IsTrue(ArtKeyRule.IsValidArtKey(key));
        }

        [TestCase("goblin_final")]
        [TestCase("CH-Goblin")]
        [TestCase("Ch_Goblin")]
        [TestCase("CH_goblin")]
        [TestCase("FL_Floor1")]
        [TestCase("")]
        [TestCase(null)]
        public void 규칙을_어긴_이름은_ArtKey로_인정하지_않는다(string key)
        {
            Assert.IsFalse(ArtKeyRule.IsValidArtKey(key));
        }

        [TestCase("CH_Goblin", EArtCategory.Character)]
        [TestCase("IT_Trash_01", EArtCategory.Item)]
        [TestCase("PR_Desk", EArtCategory.Prop)]
        [TestCase("ENV_Wall_Curve", EArtCategory.Environment)]
        public void 접두사로_분류를_정한다(string key, EArtCategory expected)
        {
            Assert.IsTrue(ArtKeyRule.TryGetCategory(key, out EArtCategory category));
            Assert.AreEqual(expected, category);
        }

        [Test]
        public void 접두사가_ENV이면_Environment로_분류한다()
        {
            ArtKeyRule.TryGetCategory("ENV_Stairs", out EArtCategory category);
            Assert.AreEqual(EArtCategory.Environment, category);
        }

        [Test]
        public void 골뱅이_뒤의_이름을_클립으로_분리한다()
        {
            Assert.IsTrue(ArtKeyRule.TrySplitAnimation("CH_Goblin@Idle", out string artKey, out string clipName));
            Assert.AreEqual("CH_Goblin", artKey);
            Assert.AreEqual("Idle", clipName);
            Assert.IsTrue(ArtKeyRule.IsAllowedClipName(clipName));
        }

        [Test]
        public void 목록에_없는_클립_이름은_허용하지_않는다()
        {
            ArtKeyRule.TrySplitAnimation("CH_Goblin@Dance", out _, out string clipName);
            Assert.IsFalse(ArtKeyRule.IsAllowedClipName(clipName));
        }

        [Test]
        public void 골뱅이가_없으면_애니메이션_파일이_아니다()
        {
            Assert.IsFalse(ArtKeyRule.TrySplitAnimation("CH_Goblin", out _, out _));
        }

        [Test]
        public void 텍스처_이름을_기준_이름과_맵으로_나눈다()
        {
            Assert.IsTrue(ArtKeyRule.TrySplitTexture("PR_Desk_Top_Normal", out string baseName, out ETextureMap map));
            Assert.AreEqual("PR_Desk_Top", baseName);
            Assert.AreEqual(ETextureMap.Normal, map);
        }

        [Test]
        public void 맵_접미사가_없는_텍스처는_인식하지_않는다()
        {
            Assert.IsFalse(ArtKeyRule.TrySplitTexture("CH_Goblin_Color", out _, out _));
        }

        [Test]
        public void 오버라이드_키가_있으면_프리팹_이름보다_우선한다()
        {
            Assert.AreEqual("CH_Orc", ArtKeyRule.ResolvePrefabKey("CH_Orc", "Assets/4_Prefab/CH_Goblin.prefab"));
            Assert.AreEqual("CH_Goblin", ArtKeyRule.ResolvePrefabKey("", "Assets/4_Prefab/CH_Goblin.prefab"));
        }

        [TestCase("CH_MinionBase", true)]
        [TestCase("CH_Goblin", false)]
        public void Base로_끝나는_키는_베이스다(string key, bool expected)
        {
            Assert.AreEqual(expected, ArtKeyRule.IsBaseKey(key));
        }
    }
}
