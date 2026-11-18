using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Editor.Art;
using UnityGameJam.View;

namespace UnityGameJam.Test.Art
{
    /// <summary>
    /// Spec ArtPipeline 규칙 12~14. 임시 폴더에 대상 프리팹과 모델용 프리팹을 만들어 실제로 적용해 본다.
    /// 끝나면 임시 폴더를 지운다.
    /// </summary>
    public class ArtBinderTest
    {
        private const string TempRoot = "Assets/__ArtBinderTestTemp";
        private const string PrefabRoot = TempRoot + "/Prefab";
        private const string ModelRoot = TempRoot + "/Model";
        private const string ArtKey = "CH_TestDummy";
        private const string PrefabPath = PrefabRoot + "/" + ArtKey + ".prefab";
        private const string ModelPath = ModelRoot + "/" + ArtKey + "/" + ArtKey + ".prefab";

        [SetUp]
        public void SetUp()
        {
            DeleteTemp();
            AssetDatabase.CreateFolder("Assets", "__ArtBinderTestTemp");
            AssetDatabase.CreateFolder(TempRoot, "Prefab");
            AssetDatabase.CreateFolder(TempRoot, "Model");
            AssetDatabase.CreateFolder(ModelRoot, ArtKey);

            // 대상 프리팹: 루트에 콜라이더, Visual/ArtSlot, Placeholder
            GameObject target = ArtBinder.CreateStandardStructure(ArtKey, PrimitiveType.Capsule, new Vector3(1f, 2f, 1f));
            target.AddComponent<CapsuleCollider>();
            PrefabUtility.SaveAsPrefabAsset(target, PrefabPath);
            Object.DestroyImmediate(target);

            // 모델 대신 쓸 프리팹 (에셋 스토어 모델과 같은 경우)
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            PrefabUtility.SaveAsPrefabAsset(model, ModelPath);
            Object.DestroyImmediate(model);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTemp();
        }

        [Test]
        public void 적용하면_Model이_생기고_Placeholder가_꺼진다()
        {
            ArtBindingItem item = InspectSingle();
            Assert.AreEqual(EArtBindingStatus.Ready, item.Status);

            Assert.IsTrue(ArtBinder.Apply(item));

            ArtSlot slot = LoadSlot();
            Assert.IsNotNull(slot.Model);
            Assert.AreEqual(ArtSlot.ModelObjectName, slot.Model.name);
            Assert.IsFalse(slot.Placeholder.activeSelf);
            Assert.AreEqual(EArtBindingStatus.Applied, InspectSingle().Status);
        }

        [Test]
        public void 루트의_Collider는_적용_후에도_유지된다()
        {
            ArtBinder.Apply(InspectSingle());

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab.GetComponent<CapsuleCollider>());
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void 되돌리면_Model이_사라지고_Placeholder가_켜진다()
        {
            ArtBinder.Apply(InspectSingle());

            Assert.IsTrue(ArtBinder.Revert(PrefabPath));

            ArtSlot slot = LoadSlot();
            Assert.IsNull(slot.Model);
            Assert.IsTrue(slot.Placeholder.activeSelf);
            Assert.IsFalse(slot.GetComponentsInChildren<Transform>(true).Any(t => t.name == ArtSlot.ModelObjectName));
            Assert.AreEqual(EArtBindingStatus.Ready, InspectSingle().Status);
        }

        [Test]
        public void 같은_모델을_두_번_적용해도_프리팹이_바뀌지_않는다()
        {
            ArtBinder.ApplyAll(PrefabRoot, ModelRoot);
            string before = File.ReadAllText(PrefabPath);

            int changed = ArtBinder.ApplyAll(PrefabRoot, ModelRoot);

            Assert.AreEqual(0, changed);
            Assert.AreEqual(before, File.ReadAllText(PrefabPath));
        }

        [Test]
        public void MatchPlaceholderHeight이면_모델_높이를_Placeholder에_맞춘다()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var serialized = new SerializedObject(root.GetComponentInChildren<ArtSlot>(true));
            serialized.FindProperty("_fitMode").enumValueIndex = (int)EArtFitMode.MatchPlaceholderHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            ArtBinder.Apply(InspectSingle());

            // Placeholder 높이 2m, 큐브 높이 1m → 배율 2
            Assert.AreEqual(2f, LoadSlot().Model.transform.localScale.y, 0.01f);
        }

        [Test]
        public void 높이를_맞출_때_원본_비율과_회전을_보존하고_슬롯_보정을_적용한다()
        {
            var sourceScale = new Vector3(1f, 3f, 4f);
            Quaternion sourceRotation = Quaternion.Euler(90f, 0f, 0f);
            using (var source = new PrefabUtility.EditPrefabContentsScope(ModelPath))
            {
                source.prefabContentsRoot.transform.localScale = sourceScale;
                source.prefabContentsRoot.transform.localRotation = sourceRotation;
            }

            var slotEuler = new Vector3(0f, 90f, 0f);
            var slotOffset = new Vector3(0.25f, 0.5f, 0.75f);
            using (var target = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var slot = new SerializedObject(target.prefabContentsRoot.GetComponentInChildren<ArtSlot>());
                slot.FindProperty("_fitMode").enumValueIndex = (int)EArtFitMode.MatchPlaceholderHeight;
                slot.FindProperty("_modelEuler").vector3Value = slotEuler;
                slot.FindProperty("_modelOffset").vector3Value = slotOffset;
                slot.FindProperty("_modelScale").floatValue = 1.5f;
                slot.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.IsTrue(ArtBinder.Apply(InspectSingle()));

            Transform applied = LoadSlot().Model.transform;
            // 원본 큐브의 Z축이 위를 향해 높이가 4m다. 2m에 맞춘 뒤 슬롯 배율 1.5를 곱한다.
            Assert.That(Vector3.Distance(sourceScale * 0.75f, applied.localScale), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(slotEuler) * sourceRotation, applied.localRotation), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(slotOffset, applied.localPosition), Is.LessThan(0.001f));
        }

        [Test]
        public void 다른_프리팹을_중첩한_조립_프리팹은_아트_대상이_아니다()
        {
            var assembly = new GameObject("FL_TestAssembly");
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), assembly.transform);
            PrefabUtility.SaveAsPrefabAsset(assembly, PrefabRoot + "/FL_TestAssembly.prefab");
            Object.DestroyImmediate(assembly);

            Assert.IsFalse(ArtBinder.Inspect(PrefabRoot, ModelRoot).Any(i => i.ArtKey == "FL_TestAssembly"));
        }

        private static ArtBindingItem InspectSingle()
        {
            return ArtBinder.Inspect(PrefabRoot, ModelRoot).Single(i => i.ArtKey == ArtKey);
        }

        private static ArtSlot LoadSlot()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<ArtSlot>(true);
        }

        private static void DeleteTemp()
        {
            if (AssetDatabase.IsValidFolder(TempRoot)) { AssetDatabase.DeleteAsset(TempRoot); }
        }
    }
}
