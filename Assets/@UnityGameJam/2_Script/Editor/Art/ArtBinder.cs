using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGameJam.View;

namespace UnityGameJam.Editor.Art
{
    /// <summary>
    /// 이름 규칙(ArtKey)으로 모델을 찾아 프리팹의 Visual/Model을 교체한다(Spec ArtPipeline 규칙 12~14).
    /// 루트의 로직 컴포넌트는 건드리지 않고, 이미 같은 모델이 들어 있으면 저장하지 않는다.
    /// </summary>
    public static class ArtBinder
    {
        public const string DefaultPrefabRoot = "Assets/@UnityGameJam/4_Prefab";
        public const string DefaultModelRoot = "Assets/@UnityGameJam/5_Art/Model";
        private const string MenuRoot = "Tools/UnityGameJam/Art/";

        private static readonly string[] _modelExtensions = { ".fbx", ".prefab" };
        private static readonly string[] _textureExtensions = { ".png", ".tga", ".jpg", ".jpeg" };

        // ---------- 메뉴 ----------

        [MenuItem(MenuRoot + "검사 (Validate)", priority = 0)]
        private static void ValidateMenu()
        {
            ArtBindingWindow.Open();
        }

        [MenuItem(MenuRoot + "전체 적용 (Apply All)", priority = 1)]
        private static void ApplyAllMenu()
        {
            int count = ApplyAll(DefaultPrefabRoot, DefaultModelRoot);
            Debug.Log($"[Art] 전체 적용 완료: 프리팹 {count}개 변경");
            ArtBindingWindow.RefreshIfOpen();
        }

        [MenuItem(MenuRoot + "선택한 프리팹에 적용 (Apply Selected)", priority = 2)]
        private static void ApplySelectedMenu()
        {
            HashSet<string> selected = GetSelectedPrefabPaths();
            int count = 0;
            foreach (ArtBindingItem item in Inspect(DefaultPrefabRoot, DefaultModelRoot))
            {
                if (item.PrefabPath != null && selected.Contains(item.PrefabPath) && ApplyOrRefresh(item)) { count++; }
            }
            Debug.Log($"[Art] 선택 적용 완료: 프리팹 {count}개 변경");
            ArtBindingWindow.RefreshIfOpen();
        }

        [MenuItem(MenuRoot + "플레이스홀더로 되돌리기 (Revert Selected)", priority = 3)]
        private static void RevertSelectedMenu()
        {
            int count = GetSelectedPrefabPaths().Count(Revert);
            Debug.Log($"[Art] 되돌리기 완료: 프리팹 {count}개 변경");
            ArtBindingWindow.RefreshIfOpen();
        }

        [MenuItem(MenuRoot + "선택한 프리팹에 적용 (Apply Selected)", true)]
        [MenuItem(MenuRoot + "플레이스홀더로 되돌리기 (Revert Selected)", true)]
        private static bool HasSelectedPrefab()
        {
            return GetSelectedPrefabPaths().Count > 0;
        }

        // ---------- 공개 API (테스트와 결과 창에서 사용) ----------

        public static List<ArtBindingItem> Inspect(string prefabRoot, string modelRoot)
        {
            return ArtBindingPlanner.Plan(CollectPrefabs(prefabRoot), CollectAssets(modelRoot, _modelExtensions), CollectAssets(modelRoot, _textureExtensions));
        }

        /// <summary>적용 가능 항목은 적용하고, 적용됨 항목은 머티리얼과 애니메이션만 갱신한다. 저장한 프리팹 수를 돌려준다.</summary>
        public static int ApplyAll(string prefabRoot, string modelRoot)
        {
            int count = 0;
            try
            {
                foreach (ArtBindingItem item in Inspect(prefabRoot, modelRoot))
                {
                    if (ApplyOrRefresh(item)) { count++; }
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
            }
            return count;
        }

        public static bool ApplyOrRefresh(ArtBindingItem item)
        {
            switch (item.Status)
            {
                case EArtBindingStatus.Ready:
                    return Apply(item);
                case EArtBindingStatus.Applied:
                    return Refresh(item);
                default:
                    return false;
            }
        }

        /// <summary>프리팹 하나에 원본(모델 또는 BaseMap)을 넣는다. 저장했으면 true.</summary>
        public static bool Apply(ArtBindingItem item)
        {
            if (item.PrefabPath == null || item.SourcePath == null) { return false; }

            GameObject root = PrefabUtility.LoadPrefabContents(item.PrefabPath);
            try
            {
                ArtSlot slot = root.GetComponentInChildren<ArtSlot>(true);
                if (slot == null)
                {
                    Debug.LogError($"[Art] ArtSlot이 없습니다: {item.PrefabPath}");
                    return false;
                }

                bool isApplied = slot.IsTextureOnly ? ApplyTexture(slot, item) : ApplyModel(slot, item);
                if (!isApplied) { return false; }

                PrefabUtility.SaveAsPrefabAsset(root, item.PrefabPath);
                Debug.Log($"[Art] 적용: {item.ArtKey} ← {item.SourcePath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Model을 지우고 Placeholder를 다시 켠다. 저장했으면 true.</summary>
        public static bool Revert(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ArtSlot slot = root.GetComponentInChildren<ArtSlot>(true);
                if (slot == null) { return false; }

                bool isChanged = RemoveModels(slot);
                var serialized = new SerializedObject(slot);
                if (serialized.FindProperty("_model").objectReferenceValue != null)
                {
                    serialized.FindProperty("_model").objectReferenceValue = null;
                    isChanged = true;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (slot.Placeholder != null && !slot.Placeholder.activeSelf)
                {
                    slot.Placeholder.SetActive(true);
                    isChanged = true;
                }
                if (slot.IsTextureOnly && slot.Placeholder != null)
                {
                    // 텍스처 전용은 머티리얼만 바뀌었으므로 기본 머티리얼로 돌린다.
                    var renderer = slot.Placeholder.GetComponent<Renderer>();
                    Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                    if (renderer != null && renderer.sharedMaterial != defaultMaterial)
                    {
                        renderer.sharedMaterial = defaultMaterial;
                        isChanged = true;
                    }
                }

                if (!isChanged) { return false; }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[Art] 되돌림: {prefabPath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 아트를 받는 프리팹의 표준 구조(루트 / Visual(ArtSlot) / Placeholder)를 만든다. 씬에는 만들지 않으므로
        /// 호출한 쪽에서 PrefabUtility.SaveAsPrefabAsset으로 저장한 뒤 지운다. Placeholder 바닥이 y=0에 오게 놓는다.
        /// </summary>
        public static GameObject CreateStandardStructure(string name, PrimitiveType shape, Vector3 placeholderSize)
        {
            var root = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            ArtSlot slot = visual.AddComponent<ArtSlot>();

            GameObject placeholder = GameObject.CreatePrimitive(shape);
            placeholder.name = ArtSlot.PlaceholderObjectName;
            Object.DestroyImmediate(placeholder.GetComponent<Collider>()); // 콜라이더는 루트에만 둔다
            placeholder.transform.SetParent(visual.transform, false);
            PlacePrimitive(placeholder.transform, shape, placeholderSize);

            var serialized = new SerializedObject(slot);
            serialized.FindProperty("_placeholder").objectReferenceValue = placeholder;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>프리팹에 지금 들어 있는 원본 경로. 텍스처 전용이면 Placeholder 머티리얼의 BaseMap 경로.</summary>
        public static string GetAppliedSourcePath(ArtSlot slot)
        {
            if (slot.IsTextureOnly)
            {
                Renderer renderer = slot.Placeholder != null ? slot.Placeholder.GetComponent<Renderer>() : null;
                return renderer == null ? null : ArtMaterialBuilder.GetBaseMapPath(renderer.sharedMaterial);
            }
            if (slot.Model == null) { return null; }
            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(slot.Model);
            return source == null ? null : AssetDatabase.GetAssetPath(source);
        }

        // ---------- 내부 ----------

        private static bool ApplyModel(ArtSlot slot, ArtBindingItem item)
        {
            ArtMaterialBuilder.BuildForModel(item.SourcePath, item.ArtKey);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(item.SourcePath);
            if (source == null)
            {
                Debug.LogError($"[Art] 모델을 불러올 수 없습니다: {item.SourcePath}");
                return false;
            }

            RemoveModels(slot);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, slot.transform);
            model.name = ArtSlot.ModelObjectName;
            SetLayerRecursively(model, slot.gameObject.layer);
            FitModel(slot, model, source.transform);

            AnimatorOverrideController overrideController = ArtAnimationBinder.BuildOverride(item.ArtKey, Path.GetDirectoryName(item.SourcePath).Replace('\\', '/'));
            if (overrideController != null) { ArtAnimationBinder.AssignToModel(model, overrideController); }

            var serialized = new SerializedObject(slot);
            serialized.FindProperty("_model").objectReferenceValue = model;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (slot.Placeholder != null) { slot.Placeholder.SetActive(false); }
            return true;
        }

        private static bool ApplyTexture(ArtSlot slot, ArtBindingItem item)
        {
            Renderer renderer = slot.Placeholder != null ? slot.Placeholder.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                Debug.LogError($"[Art] 텍스처 전용 프리팹에 Placeholder 렌더러가 없습니다: {item.PrefabPath}");
                return false;
            }
            renderer.sharedMaterial = ArtMaterialBuilder.BuildTextureOnly(item.ArtKey, item.SourcePath);
            slot.Placeholder.SetActive(true);
            return true;
        }

        /// <summary>적용됨 항목: 머티리얼과 애니메이션만 갱신하고, Animator 연결이 바뀔 때만 프리팹을 저장한다.</summary>
        private static bool Refresh(ArtBindingItem item)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.PrefabPath);
            ArtSlot slot = prefab != null ? prefab.GetComponentInChildren<ArtSlot>(true) : null;
            if (slot == null) { return false; }

            if (slot.IsTextureOnly)
            {
                ArtMaterialBuilder.BuildTextureOnly(item.ArtKey, item.SourcePath);
                return false;
            }

            ArtMaterialBuilder.BuildForModel(item.SourcePath, item.ArtKey);
            AnimatorOverrideController overrideController = ArtAnimationBinder.BuildOverride(item.ArtKey, Path.GetDirectoryName(item.SourcePath).Replace('\\', '/'));
            if (overrideController == null || slot.Model == null) { return false; }

            var animator = slot.Model.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == overrideController) { return false; }

            GameObject root = PrefabUtility.LoadPrefabContents(item.PrefabPath);
            try
            {
                ArtSlot editable = root.GetComponentInChildren<ArtSlot>(true);
                if (editable.Model == null || !ArtAnimationBinder.AssignToModel(editable.Model, overrideController)) { return false; }
                PrefabUtility.SaveAsPrefabAsset(root, item.PrefabPath);
                Debug.Log($"[Art] 애니메이션 갱신: {item.ArtKey}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool RemoveModels(ArtSlot slot)
        {
            bool isRemoved = false;
            for (int i = slot.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = slot.transform.GetChild(i).gameObject;
                if (child == slot.Placeholder) { continue; }
                if (child != slot.Model && child.name != ArtSlot.ModelObjectName) { continue; }
                Object.DestroyImmediate(child);
                isRemoved = true;
            }
            return isRemoved;
        }

        private static void FitModel(ArtSlot slot, GameObject model, Transform source)
        {
            Transform modelTransform = model.transform;
            modelTransform.localPosition = slot.ModelOffset;
            modelTransform.localRotation = Quaternion.Euler(slot.ModelEuler) * source.localRotation;
            modelTransform.localScale = source.localScale;

            float factor = 1f;
            if (slot.FitMode != EArtFitMode.None && slot.Placeholder != null)
            {
                // 꺼진 렌더러는 bounds가 0이므로 잠시 켜서 잰다.
                bool wasActive = slot.Placeholder.activeSelf;
                slot.Placeholder.SetActive(true);
                Bounds target = CalculateBounds(slot.Placeholder);
                slot.Placeholder.SetActive(wasActive);
                Bounds current = CalculateBounds(model);

                if (current.size.y > Mathf.Epsilon && slot.FitMode == EArtFitMode.MatchPlaceholderHeight)
                {
                    factor = target.size.y / current.size.y;
                }
                else if (slot.FitMode == EArtFitMode.MatchPlaceholderBounds)
                {
                    factor = MinRatio(target.size, current.size);
                }
            }
            modelTransform.localScale = source.localScale * (factor * slot.ModelScale);
        }

        private static float MinRatio(Vector3 target, Vector3 current)
        {
            float ratio = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                if (current[axis] > Mathf.Epsilon) { ratio = Mathf.Min(ratio, target[axis] / current[axis]); }
            }
            return ratio == float.MaxValue ? 1f : ratio;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { return new Bounds(target.transform.position, Vector3.zero); }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) { bounds.Encapsulate(renderers[i].bounds); }
            return bounds;
        }

        private static void PlacePrimitive(Transform placeholder, PrimitiveType shape, Vector3 size)
        {
            switch (shape)
            {
                case PrimitiveType.Capsule:
                case PrimitiveType.Cylinder:
                    // 기본 높이가 2라서 y 배율은 절반
                    placeholder.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
                    placeholder.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                    break;
                case PrimitiveType.Quad:
                case PrimitiveType.Plane:
                    placeholder.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    placeholder.localScale = shape == PrimitiveType.Plane ? new Vector3(size.x * 0.1f, 1f, size.z * 0.1f) : new Vector3(size.x, size.z, 1f);
                    placeholder.localPosition = new Vector3(0f, 0.01f, 0f); // 바닥과 겹쳐 깜빡이지 않게 살짝 띄운다
                    if (shape == PrimitiveType.Plane) { placeholder.localRotation = Quaternion.identity; }
                    break;
                default:
                    placeholder.localScale = size;
                    placeholder.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                    break;
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform) { SetLayerRecursively(child.gameObject, layer); }
        }

        private static List<ArtPrefabInfo> CollectPrefabs(string prefabRoot)
        {
            var result = new List<ArtPrefabInfo>();
            if (!AssetDatabase.IsValidFolder(prefabRoot)) { return result; }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                ArtSlot slot = prefab != null ? FindOwnSlot(prefab) : null;
                if (slot == null) { continue; }

                string key = ArtKeyRule.ResolvePrefabKey(slot.ArtKeyOverride, path);
                result.Add(new ArtPrefabInfo(path, key, GetAppliedSourcePath(slot), slot.IsTextureOnly));
            }
            return result;
        }

        /// <summary>
        /// 프리팹 자신의 ArtSlot만 찾는다. 표준 구조(docs/rules/prefab.md 3절)에서 ArtSlot은 루트 바로 아래 Visual에 있다.
        /// FL_Floor처럼 다른 프리팹을 중첩한 조립품은 안의 ArtSlot이 더 깊이 있으므로 제외된다. Variant도 구조가 같아 그대로 잡힌다.
        /// </summary>
        private static ArtSlot FindOwnSlot(GameObject prefab)
        {
            foreach (Transform child in prefab.transform)
            {
                var slot = child.GetComponent<ArtSlot>();
                if (slot != null) { return slot; }
            }
            return null;
        }

        private static List<string> CollectAssets(string root, string[] extensions)
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(root)) { return result; }

            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (extensions.Contains(Path.GetExtension(path).ToLowerInvariant()) && !result.Contains(path)) { result.Add(path); }
            }
            return result;
        }

        private static HashSet<string> GetSelectedPrefabPaths()
        {
            var paths = new HashSet<string>();
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (path.EndsWith(".prefab")) { paths.Add(path); }
            }
            return paths;
        }
    }
}
