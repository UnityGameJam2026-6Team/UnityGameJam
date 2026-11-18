using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityGameJam.Editor.Art;

namespace UnityGameJam.Test.Art
{
    /// <summary>Spec ArtPipeline 규칙 6~11: 프리팹과 모델의 짝짓기와 상태 분류.</summary>
    public class ArtBindingPlannerTest
    {
        private const string PrefabDir = "Assets/@UnityGameJam/4_Prefab/Character/";
        private const string ModelDir = "Assets/@UnityGameJam/5_Art/Model/Character/";

        private static readonly List<string> _noTextures = new List<string>();

        [Test]
        public void 프리팹과_모델의_키가_같으면_적용_가능이다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Goblin") };
            var models = new List<string> { ModelDir + "CH_Goblin/CH_Goblin.fbx" };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, models, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.Ready, item.Status);
            Assert.AreEqual(models[0], item.SourcePath);
        }

        [Test]
        public void 이미_같은_모델이_있으면_적용됨이다()
        {
            string model = ModelDir + "CH_Goblin/CH_Goblin.fbx";
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Goblin", appliedSourcePath: model) };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string> { model }, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.Applied, item.Status);
        }

        [Test]
        public void 다른_모델이_들어_있으면_적용_가능이다()
        {
            string model = ModelDir + "CH_Goblin/CH_Goblin.fbx";
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Goblin", appliedSourcePath: ModelDir + "Old/CH_Goblin.prefab") };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string> { model }, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.Ready, item.Status);
        }

        [Test]
        public void Base로_끝나는_프리팹은_적용하지_않는다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_MinionBase") };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string>(), _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.Base, item.Status);
        }

        [Test]
        public void 같은_키의_모델이_둘이면_중복으로_적용하지_않는다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Goblin") };
            var models = new List<string> { ModelDir + "CH_Goblin/CH_Goblin.fbx", ModelDir + "Other/CH_Goblin.prefab" };

            List<ArtBindingItem> items = ArtBindingPlanner.Plan(prefabs, models, _noTextures);

            Assert.AreEqual(EArtBindingStatus.Duplicate, items.Single(i => i.PrefabPath != null).Status);
            Assert.IsFalse(items.Any(i => i.Status == EArtBindingStatus.Ready));
        }

        [Test]
        public void 모델이_없으면_모델_없음이다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Slime") };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string>(), _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.MissingModel, item.Status);
        }

        [Test]
        public void 텍스처_전용_프리팹은_BaseMap을_원본으로_쓴다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("IT_FloorStain_01", isTextureOnly: true) };
            var textures = new List<string>
            {
                "Assets/@UnityGameJam/5_Art/Model/Item/IT_FloorStain_01/IT_FloorStain_01_Normal.png",
                "Assets/@UnityGameJam/5_Art/Model/Item/IT_FloorStain_01/IT_FloorStain_01_BaseMap.png",
            };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string>(), textures).Single();

            Assert.AreEqual(EArtBindingStatus.Ready, item.Status);
            Assert.AreEqual(textures[1], item.SourcePath);
        }

        [Test]
        public void 프리팹이_없는_모델은_주인_없음이다()
        {
            var models = new List<string> { "Assets/@UnityGameJam/5_Art/Model/Item/IT_Trash_02/IT_Trash_02.fbx" };

            ArtBindingItem item = ArtBindingPlanner.Plan(new List<ArtPrefabInfo>(), models, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.Orphan, item.Status);
            Assert.AreEqual("IT_Trash_02", item.ArtKey);
        }

        [Test]
        public void 규칙을_어긴_모델_이름은_이름_오류다()
        {
            var models = new List<string> { ModelDir + "goblin_final.fbx" };

            ArtBindingItem item = ArtBindingPlanner.Plan(new List<ArtPrefabInfo>(), models, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.InvalidName, item.Status);
        }

        [Test]
        public void 규칙을_어긴_프리팹_키는_이름_오류다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("Goblin") };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, new List<string>(), _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.InvalidName, item.Status);
        }

        [Test]
        public void 애니메이션_파일은_모델로_보지_않는다()
        {
            var prefabs = new List<ArtPrefabInfo> { Prefab("CH_Goblin") };
            var models = new List<string> { ModelDir + "CH_Goblin/CH_Goblin@Idle.fbx" };

            ArtBindingItem item = ArtBindingPlanner.Plan(prefabs, models, _noTextures).Single();

            Assert.AreEqual(EArtBindingStatus.MissingModel, item.Status);
        }

        private static ArtPrefabInfo Prefab(string key, string appliedSourcePath = null, bool isTextureOnly = false)
        {
            return new ArtPrefabInfo(PrefabDir + key + ".prefab", key, appliedSourcePath, isTextureOnly);
        }
    }
}
