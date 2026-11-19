using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 카메라와 플레이어 사이를 가리는 벽을 반투명하게 만든다.
    /// 가리는 렌더러만 잠깐 Wall Fade 머티리얼 복사본으로 바꾸고, 더 이상 가리지 않으면 원래 머티리얼로 되돌린다.
    /// 카메라(FloorCameraFollow)와 같은 오브젝트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(FloorCameraFollow))]
    public sealed class WallOcclusionFader : MonoBehaviour
    {
        private static readonly int FadeAmountId = Shader.PropertyToID("_WallFadeAmount");
        private static readonly int FadeTargetId = Shader.PropertyToID("_WallFadeTarget");
        private static readonly int FadeParamsId = Shader.PropertyToID("_WallFadeParams");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("UnityGameJam/Wall Fade 셰이더 머티리얼(M_FX_WallFade)")]
        [SerializeField] private Material _fadeMaterial;

        [Header("가림 검사")]
        [Tooltip("카메라에서 플레이어로 쏘는 구의 반지름(m)")]
        [SerializeField, Min(0.05f)] private float _probeRadius = 0.45f;
        [Tooltip("플레이어 발에서 검사할 높이들(m). 발과 머리 쪽을 함께 본다")]
        [SerializeField] private float[] _probeHeights = { 0.6f, 1.4f };

        [Header("보이는 모습")]
        [Tooltip("구멍 중심 높이(플레이어 발 기준, m)")]
        [SerializeField] private float _holeCenterHeight = 0.9f;
        [Tooltip("플레이어 위치에서 잰 구멍 반경(m)")]
        [SerializeField, Min(0.1f)] private float _holeRadius = 1.5f;
        [Tooltip("구멍 테두리 부드러움")]
        [SerializeField, Range(0.01f, 0.95f)] private float _holeSoftness = 0.35f;
        [Tooltip("가장 투명할 때의 불투명도(0 = 완전히 투명)")]
        [SerializeField, Range(0f, 1f)] private float _minOpacity = 0.2f;
        [Tooltip("투명해지고 돌아오는 속도(1/초)")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 5f;

        private sealed class Occluder
        {
            public Renderer[] Renderers;
            public Material[][] Originals;
            public MaterialPropertyBlock[] OriginalBlocks;
            public float Amount;
            public bool IsSwapped;
            public bool IsTracked;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Dictionary<Collider, Occluder> _occluderByCollider = new();
        private readonly Dictionary<Material, Material> _fadeByOriginal = new();
        private readonly List<Occluder> _tracked = new();
        private readonly HashSet<Occluder> _occludingNow = new();
        private readonly List<Renderer> _rendererBuffer = new();
        private FloorCameraFollow _follow;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _follow = GetComponent<FloorCameraFollow>();
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            foreach (Occluder occluder in _tracked)
            {
                Restore(occluder);
                occluder.Amount = 0f;
                occluder.IsTracked = false;
            }
            _tracked.Clear();
        }

        private void OnDestroy()
        {
            foreach (Material fade in _fadeByOriginal.Values)
            {
                if (fade != null) { Destroy(fade); }
            }
            _fadeByOriginal.Clear();
        }

        private void LateUpdate()
        {
            _occludingNow.Clear();
            Transform target = _follow.Target;
            if (target != null && _fadeMaterial != null)
            {
                Vector3 feet = target.position;
                Shader.SetGlobalVector(FadeTargetId, feet + Vector3.up * _holeCenterHeight);
                Shader.SetGlobalVector(FadeParamsId, new Vector4(_holeRadius, _holeSoftness, _minOpacity, 0f));
                foreach (float height in _probeHeights)
                {
                    Probe(transform.position, feet + Vector3.up * height, feet.y, target);
                }
            }

            // 결과 화면 등에서 시간이 멈춰도 벽이 돌아오도록 실제 시간으로 움직인다.
            float step = Time.unscaledDeltaTime * _fadeSpeed;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                Occluder occluder = _tracked[i];
                bool isOccluding = _occludingNow.Contains(occluder);
                occluder.Amount = Mathf.MoveTowards(occluder.Amount, isOccluding ? 1f : 0f, step);
                if (!isOccluding && occluder.Amount <= 0f)
                {
                    Restore(occluder);
                    occluder.IsTracked = false;
                    _tracked.RemoveAt(i);
                    continue;
                }
                if (!occluder.IsSwapped) { Swap(occluder); }
                ApplyAmount(occluder);
            }
        }

        private void Probe(Vector3 origin, Vector3 point, float feetY, Transform target)
        {
            Vector3 toPoint = point - origin;
            float length = toPoint.magnitude;
            float distance = length - _probeRadius;
            if (distance <= 0.01f) { return; }

            int count = Physics.SphereCastNonAlloc(origin, _probeRadius, toPoint / length, _hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i].collider;
                if (hit == null) { continue; }
                // 발밑보다 낮은 바닥·계단은 가리는 물체가 아니다.
                if (hit.bounds.max.y < feetY + 0.3f) { continue; }
                // 플레이어 자신, 들고 있는 물건, 졸개는 비우지 않는다.
                if (hit.transform.IsChildOf(target)) { continue; }
                if (hit.GetComponentInParent<PlayerBody>() != null || hit.GetComponentInParent<MinionAgent>() != null) { continue; }

                Occluder occluder = GetOccluder(hit);
                if (occluder == null || !_occludingNow.Add(occluder) || occluder.IsTracked) { continue; }
                occluder.IsTracked = true;
                _tracked.Add(occluder);
            }
        }

        private Occluder GetOccluder(Collider collider)
        {
            if (_occluderByCollider.TryGetValue(collider, out Occluder cached)) { return cached; }

            CollectOpaqueRenderers(collider.transform);
            // 콜라이더와 모델이 형제 오브젝트로 나뉜 소품은 부모에서 찾는다. 층 전체를 잡지 않도록 작은 묶음만 허용한다.
            if (_rendererBuffer.Count == 0 && collider.transform.parent != null)
            {
                CollectOpaqueRenderers(collider.transform.parent);
                if (_rendererBuffer.Count > 12) { _rendererBuffer.Clear(); }
            }

            Occluder occluder = null;
            if (_rendererBuffer.Count > 0)
            {
                occluder = new Occluder
                {
                    Renderers = _rendererBuffer.ToArray(),
                    Originals = new Material[_rendererBuffer.Count][],
                    OriginalBlocks = new MaterialPropertyBlock[_rendererBuffer.Count]
                };
            }
            _occluderByCollider[collider] = occluder;
            return occluder;
        }

        private void CollectOpaqueRenderers(Transform root)
        {
            _rendererBuffer.Clear();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) { continue; }
                if (IsOpaque(renderer.sharedMaterials)) { _rendererBuffer.Add(renderer); }
            }
        }

        // 방 가림막처럼 이미 반투명한 물체는 건드리지 않는다.
        private static bool IsOpaque(Material[] materials)
        {
            bool hasMaterial = false;
            foreach (Material material in materials)
            {
                if (material == null) { continue; }
                if (material.renderQueue > 2500) { return false; }
                hasMaterial = true;
            }
            return hasMaterial;
        }

        private void Swap(Occluder occluder)
        {
            for (int i = 0; i < occluder.Renderers.Length; i++)
            {
                Renderer renderer = occluder.Renderers[i];
                if (renderer == null) { continue; }

                Material[] originals = renderer.sharedMaterials;
                occluder.Originals[i] = originals;
                if (renderer.HasPropertyBlock())
                {
                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original);
                    occluder.OriginalBlocks[i] = original;
                }
                else
                {
                    occluder.OriginalBlocks[i] = null;
                }

                var fades = new Material[originals.Length];
                for (int m = 0; m < originals.Length; m++) { fades[m] = GetFadeMaterial(originals[m]); }
                renderer.sharedMaterials = fades;
            }
            occluder.IsSwapped = true;
        }

        private void ApplyAmount(Occluder occluder)
        {
            float amount = Mathf.SmoothStep(0f, 1f, occluder.Amount);
            foreach (Renderer renderer in occluder.Renderers)
            {
                if (renderer == null) { continue; }
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeAmountId, amount);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void Restore(Occluder occluder)
        {
            if (!occluder.IsSwapped) { return; }
            for (int i = 0; i < occluder.Renderers.Length; i++)
            {
                Renderer renderer = occluder.Renderers[i];
                if (renderer == null || occluder.Originals[i] == null) { continue; }
                renderer.sharedMaterials = occluder.Originals[i];
                renderer.SetPropertyBlock(occluder.OriginalBlocks[i]);
                occluder.Originals[i] = null;
                occluder.OriginalBlocks[i] = null;
            }
            occluder.IsSwapped = false;
        }

        private Material GetFadeMaterial(Material original)
        {
            if (original == null) { return _fadeMaterial; }
            if (_fadeByOriginal.TryGetValue(original, out Material fade) && fade != null) { return fade; }

            fade = new Material(_fadeMaterial) { name = $"{original.name} (Wall Fade)" };
            if (original.HasProperty(BaseMapId)) { CopyTexture(original, BaseMapId, fade); }
            else if (original.HasProperty(MainTexId)) { CopyTexture(original, MainTexId, fade); }
            if (original.HasProperty(BaseColorId)) { fade.SetColor(BaseColorId, original.GetColor(BaseColorId)); }
            else if (original.HasProperty(ColorId)) { fade.SetColor(BaseColorId, original.GetColor(ColorId)); }
            _fadeByOriginal[original] = fade;
            return fade;
        }

        private static void CopyTexture(Material source, int textureId, Material destination)
        {
            destination.SetTexture(BaseMapId, source.GetTexture(textureId));
            destination.SetTextureScale(BaseMapId, source.GetTextureScale(textureId));
            destination.SetTextureOffset(BaseMapId, source.GetTextureOffset(textureId));
        }
    }
}
