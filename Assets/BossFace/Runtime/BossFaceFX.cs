using System.Linq;
using UnityEngine;

namespace DemonKing.BossFace
{
    /// <summary>
    /// Brings the boss's identity back onto the plain FBX (Boss_DemonKing.fbx) in Unity:
    /// black mist filling the hood, glowing blinking eyes, everything kept inside the hood opening.
    ///
    /// Use: drop the FBX into the scene (or into your own prefab) and add this component to its root.
    /// It finds the nodes the FBX already has (ShadowHead, HoodInterior, Eye_L, Eye_R) and puts the
    /// effect materials and the mist on them. Works in Edit mode (preview) and in Play mode.
    /// Also works on the Boss_DemonKing prefab from the package (then there is nothing to add).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BossFaceFX : MonoBehaviour
    {
        [Tooltip("Mist particles (BossFace_Mist.prefab)")]
        public GameObject mistPrefab;
        [Tooltip("Hood interior mask material for the FBX mesh (body UV in UV0): HoodInteriorMask_FBX.mat")]
        public Material hoodInteriorMaterial;
        [Tooltip("Eye materials: BossEye_R.mat / BossEye_L.mat")]
        public Material eyeMaterialR, eyeMaterialL;

        const string MistName = "BossFace_Mist";
        GameObject previewMist;   // edit-mode only, never saved

        void OnEnable() { Apply(); }

        void OnDisable()
        {
            if (previewMist != null) DestroyImmediate(previewMist);
            previewMist = null;
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            var all = GetComponentsInChildren<Transform>(true);
            var head = all.FirstOrDefault(t => t.name == "ShadowHead");
            if (head == null) { Debug.LogWarning("[BossFaceFX] no 'ShadowHead' node under " + name + " (use Boss_DemonKing.fbx)", this); return; }

            // hood interior: invisible stencil mask (+ near-black backdrop) on the inner surfaces
            var interior = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "HoodInterior" || r.name == "HoodInteriorMask");
            if (interior != null && hoodInteriorMaterial != null && interior.name == "HoodInterior")
                interior.sharedMaterial = hoodInteriorMaterial;
            if (interior is SkinnedMeshRenderer smr) { smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false; }

            // eyes: the blinking glow shader
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Eye_R" && eyeMaterialR != null) r.sharedMaterial = eyeMaterialR;
                if (r.name == "Eye_L" && eyeMaterialL != null) r.sharedMaterial = eyeMaterialL;
                if (r.name == "Eye_R" || r.name == "Eye_L") { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            }

            // mist: unless the head already has it (the package prefab does)
            bool hasMist = head.GetComponentsInChildren<ParticleSystem>(true).Any(p => previewMist == null || !p.transform.IsChildOf(previewMist.transform));
            if (hasMist || mistPrefab == null) return;
            if (Application.isPlaying)
            {
                var m = Instantiate(mistPrefab, head, false);
                m.name = MistName;
                Place(m.transform);
            }
            else if (previewMist == null)
            {
                previewMist = Instantiate(mistPrefab, head, false);
                previewMist.name = MistName + " (preview)";
                previewMist.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                foreach (var t in previewMist.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = previewMist.hideFlags;
                Place(previewMist.transform);
            }
        }

        static void Place(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            var p = t.parent.lossyScale;
            t.localScale = new Vector3(1f / p.x, 1f / p.y, 1f / p.z);   // the mist is authored in meters
        }

#if UNITY_EDITOR
        // fill the references automatically when the component is added in the editor
        void Reset() { AutoAssign(); }

        public void AutoAssign()
        {
            // (match by file name AND shader: an imported FBX can bring its own plain materials with similar names)
            T Find<T>(string n, string shader = null) where T : Object
            {
                foreach (var g in UnityEditor.AssetDatabase.FindAssets(n))
                {
                    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileNameWithoutExtension(p) != n) continue;
                    var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(p);
                    if (a == null) continue;
                    if (shader != null && !(a is Material m && m.shader != null && m.shader.name == shader)) continue;
                    return a;
                }
                return null;
            }
            mistPrefab = Find<GameObject>("BossFace_Mist");
            hoodInteriorMaterial = Find<Material>("HoodInteriorMask_FBX", "DemonKing/HoodInteriorMask");
            eyeMaterialR = Find<Material>("BossEye_R", "DemonKing/GlowEyes");
            eyeMaterialL = Find<Material>("BossEye_L", "DemonKing/GlowEyes");
            Apply();
        }
#endif
    }
}
