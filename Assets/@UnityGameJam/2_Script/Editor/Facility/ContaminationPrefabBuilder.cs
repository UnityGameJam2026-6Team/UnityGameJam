using System;
using UnityEditor;
using UnityEngine;
using UnityGameJam.View;
using Object = UnityEngine.Object;

namespace UnityGameJam.Editor
{
    internal static class ContaminationPrefabBuilder
    {
        private const string Root = "Assets/@UnityGameJam/";
        private const string ItemDir = Root + "4_Prefab/Item/";
        private const string BootstrapPath = Root + "4_Prefab/System/SYS_Bootstrap.prefab";
        private static readonly string[] _kinds = { "Book", "Cup", "Document", "Chair" };

        [MenuItem("Tools/UnityGameJam/B4/오염 프리팹 생성 및 연결")]
        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("편집 모드에서 실행하세요."); }
            GameObject debrisBase = Make("IT_DebrisBase", PrimitiveType.Cube, new Vector3(0.9f, 0.6f, 0.8f), "Prop");
            GameObject clutterBase = Make("IT_ClutterBase", PrimitiveType.Cube, Vector3.one, "Slot");
            GameObject debris = Make("IT_HeavyDebris_01", PrimitiveType.Cube, new Vector3(0.9f, 0.6f, 0.8f), "Prop", debrisBase);
            GameObject trash = Make("IT_Trash_01", PrimitiveType.Sphere, Vector3.one * 0.45f, "Discharge");
            GameObject stain = Make("IT_FloorStain_01", PrimitiveType.Quad, new Vector3(1.2f, 0.85f, 1f), "Slime");
            GameObject mop = Make("IT_Mop", PrimitiveType.Cylinder, new Vector3(0.08f, 0.65f, 0.08f), "Mop");
            Vector3[] sizes = { new(0.46f, 0.12f, 0.32f), new(0.22f, 0.13f, 0.22f), new(0.3f, 0.02f, 0.42f), new(0.48f, 0.45f, 0.48f) };
            string[] materials = { "Prop", "Toilet", "Skeleton", "Sofa" };
            var clutter = new GameObject[_kinds.Length];
            for (int i = 0; i < clutter.Length; i++)
            {
                clutter[i] = Make("IT_Clutter_" + _kinds[i], i == 1 ? PrimitiveType.Cylinder : PrimitiveType.Cube, sizes[i], materials[i], clutterBase);
            }
            var settings = AssetDatabase.LoadAssetAtPath<ViewSettingsSO>(Root + "3_Data/ScriptableObject/View/ViewSettings.asset");
            var serialized = new SerializedObject(settings);
            SetFirst(serialized.FindProperty("_heavyDebrisPrefabs"), debris);
            SetFirst(serialized.FindProperty("_trashPrefabs"), trash);
            SetFirst(serialized.FindProperty("_stainPrefabs"), stain);
            SerializedProperty entries = serialized.FindProperty("_clutterPrefabs");
            for (int i = 0; i < clutter.Length; i++)
            {
                bool exists = false;
                for (int j = 0; j < entries.arraySize; j++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(j);
                    if (entry.FindPropertyRelative("Kind").stringValue != _kinds[i]) { continue; }
                    if (entry.FindPropertyRelative("Prefab").objectReferenceValue == null) { entry.FindPropertyRelative("Prefab").objectReferenceValue = clutter[i]; }
                    exists = true;
                    break;
                }
                if (exists) { continue; }
                int index = entries.arraySize;
                entries.arraySize = index + 1;
                SerializedProperty added = entries.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("Kind").stringValue = _kinds[i];
                added.FindPropertyRelative("Prefab").objectReferenceValue = clutter[i];
            }
            SerializedProperty mopRef = serialized.FindProperty("_mopPrefab");
            if (mopRef.objectReferenceValue == null) { mopRef.objectReferenceValue = mop; }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(settings);
            using (var scope = new PrefabUtility.EditPrefabContentsScope(BootstrapPath))
            {
                GameObject root = scope.prefabContentsRoot;
                if (!root.TryGetComponent(out ContaminationView _)) { root.AddComponent<ContaminationView>(); }
            }
        }

        private static void SetFirst(SerializedProperty list, GameObject prefab)
        {
            if (list.arraySize == 0) { list.arraySize = 1; }
            SerializedProperty first = list.GetArrayElementAtIndex(0);
            if (first.objectReferenceValue == null) { first.objectReferenceValue = prefab; }
        }

        private static GameObject Make(string name, PrimitiveType shape, Vector3 size, string material, GameObject basis = null)
        {
            string path = ItemDir + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) { return existing; }
            GameObject root = basis == null ? new GameObject(name) : (GameObject)PrefabUtility.InstantiatePrefab(basis);
            GameObject primitive = GameObject.CreatePrimitive(shape);
            try
            {
                root.name = name;
                var slot = root.GetComponentInChildren<ArtSlot>();
                GameObject placeholder;
                if (slot == null)
                {
                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(root.transform, false);
                    slot = visual.AddComponent<ArtSlot>();
                    placeholder = primitive;
                    placeholder.name = ArtSlot.PlaceholderObjectName;
                    placeholder.transform.SetParent(visual.transform, false);
                    Object.DestroyImmediate(placeholder.GetComponent<Collider>());
                    var serialized = new SerializedObject(slot);
                    serialized.FindProperty("_placeholder").objectReferenceValue = placeholder;
                    serialized.FindProperty("_isTextureOnly").boolValue = shape == PrimitiveType.Quad;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    placeholder = slot.Placeholder;
                    placeholder.GetComponent<MeshFilter>().sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                }
                placeholder.transform.localScale = size;
                float height = shape == PrimitiveType.Cylinder ? size.y : size.y * 0.5f;
                placeholder.transform.localPosition = Vector3.up * (shape == PrimitiveType.Quad ? 0.015f : height);
                if (shape == PrimitiveType.Quad) { placeholder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); }
                placeholder.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    Root + "5_Art/Material/Whitebox/M_WB_" + material + ".mat");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) { throw new InvalidOperationException(path); }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (primitive != null) { Object.DestroyImmediate(primitive); }
            }
        }
    }
}
