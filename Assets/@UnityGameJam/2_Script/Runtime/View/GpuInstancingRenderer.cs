using UnityEngine;

namespace UnityGameJam.View
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GpuInstancingRenderer : MonoBehaviour
    {
        private static MaterialPropertyBlock _scratch;
        private MeshRenderer _configuredRenderer;

        private void OnEnable()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            Material material = meshRenderer.sharedMaterial;
            if (!SystemInfo.supportsInstancing || material == null || !material.enableInstancing ||
                meshRenderer.isPartOfStaticBatch || meshRenderer.HasPropertyBlock())
            {
                return;
            }

            // 빈 블록으로 이 렌더러만 SRP Batcher에서 제외해 같은 메시끼리 GPU 인스턴싱한다.
            // SetPropertyBlock은 값을 복사하므로 모든 벽이 임시 블록 하나를 재사용할 수 있다.
            _scratch ??= new MaterialPropertyBlock();
            _scratch.Clear();
            meshRenderer.SetPropertyBlock(_scratch);
            _configuredRenderer = meshRenderer;
        }

        private void OnDisable()
        {
            if (_configuredRenderer == null)
            {
                return;
            }

            // 다른 연출이 나중에 설정한 속성은 보존한다.
            _configuredRenderer.GetPropertyBlock(_scratch);
            if (_scratch.isEmpty)
            {
                _configuredRenderer.SetPropertyBlock(null);
            }
            _configuredRenderer = null;
        }
    }
}
