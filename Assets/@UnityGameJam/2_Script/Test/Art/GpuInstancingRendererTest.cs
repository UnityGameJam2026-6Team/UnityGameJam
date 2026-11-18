using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.View;

namespace UnityGameJam.Test.Art
{
    public class GpuInstancingRendererTest
    {
        [Test]
        public void 직선_벽은_공유_머티리얼로_인스턴싱하고_다른_연출의_속성을_보존한다()
        {
            const string path = "Assets/@UnityGameJam/4_Prefab/Environment/ENV_Wall_Straight.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject wall = Object.Instantiate(prefab);
            try
            {
                GpuInstancingRenderer instancing = wall.GetComponentInChildren<GpuInstancingRenderer>();
                Assert.IsNotNull(instancing);
                MeshRenderer meshRenderer = instancing.GetComponent<MeshRenderer>();
                Assert.IsTrue(meshRenderer.sharedMaterial.enableInstancing);
                Assert.AreSame(prefab.GetComponentInChildren<GpuInstancingRenderer>()
                    .GetComponent<MeshRenderer>().sharedMaterial, meshRenderer.sharedMaterial);
                Assert.AreEqual(SystemInfo.supportsInstancing, meshRenderer.HasPropertyBlock());

                instancing.enabled = false;
                Assert.IsFalse(meshRenderer.HasPropertyBlock());
                instancing.enabled = true;
                var properties = new MaterialPropertyBlock();
                int colorId = Shader.PropertyToID("_BaseColor");
                properties.SetColor(colorId, Color.red);
                meshRenderer.SetPropertyBlock(properties);
                instancing.enabled = false;
                meshRenderer.GetPropertyBlock(properties);
                Assert.AreEqual(Color.red, properties.GetColor(colorId));
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }
    }
}
