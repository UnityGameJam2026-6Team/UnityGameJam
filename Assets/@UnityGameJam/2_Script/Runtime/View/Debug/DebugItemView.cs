using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// B4(ContaminationView) 전까지 쓰는 개체 디버그 표시. 유형별 색 도형을 만들고,
    /// 플레이어가 들고 있는 물건과 걸레는 머리 위에 띄운다. B4에서 실제 표시로 바꾸면 지운다.
    /// </summary>
    public sealed class DebugItemView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MatchBootstrap _bootstrap;
        [Tooltip("URP Lit 머티리얼. 색은 MaterialPropertyBlock으로 바꾼다")]
        [SerializeField] private Material _baseMaterial;
        [SerializeField] private float _carryHeight = 2.1f;

        private readonly Dictionary<int, Transform> _views = new Dictionary<int, Transform>();
        private Transform _mop;
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            if (_bootstrap != null) { _bootstrap.MatchStarted += HandleMatchStarted; }
        }

        private void OnDisable()
        {
            if (_bootstrap != null) { _bootstrap.MatchStarted -= HandleMatchStarted; }
        }

        private void HandleMatchStarted(MatchBootstrap bootstrap)
        {
            _block = new MaterialPropertyBlock();
            foreach (ItemSnapshot item in bootstrap.Session.Items) { _views[item.ItemId] = CreateView(item.Type); }
            _mop = CreatePrimitive(PrimitiveType.Cylinder, new Vector3(0.1f, 0.7f, 0.1f), new Color(0.25f, 0.45f, 0.9f)).transform;
        }

        private void LateUpdate()
        {
            IMatchSession session = _bootstrap != null ? _bootstrap.Session : null;
            if (session == null || _bootstrap.Player == null) { return; }

            Vector3 head = _bootstrap.Player.transform.position + Vector3.up * _carryHeight;
            foreach (ItemSnapshot item in session.Items)
            {
                if (!_views.TryGetValue(item.ItemId, out Transform view)) { continue; }
                bool isVisible = item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea || item.State == EItemState.Carried;
                view.gameObject.SetActive(isVisible);
                if (!isVisible) { continue; }
                view.position = item.State == EItemState.Carried && item.Carrier == EWorkerType.Player ? head : item.Position + Vector3.up * view.localScale.y * 0.5f;
            }

            _mop.gameObject.SetActive(session.IsPlayerHoldingMop);
            _mop.position = _bootstrap.Player.transform.position + _bootstrap.Player.transform.right * 0.45f + Vector3.up * 0.9f;
        }

        private Transform CreateView(EContaminationType type)
        {
            switch (type)
            {
                case EContaminationType.HeavyDebris: return CreatePrimitive(PrimitiveType.Cube, new Vector3(0.9f, 0.6f, 0.9f), new Color(0.45f, 0.3f, 0.15f)).transform;
                case EContaminationType.Trash: return CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.45f, 0.45f, 0.45f), new Color(0.9f, 0.85f, 0.2f)).transform;
                case EContaminationType.ClutterItem: return CreatePrimitive(PrimitiveType.Cube, new Vector3(0.35f, 0.35f, 0.35f), new Color(0.2f, 0.8f, 0.9f)).transform;
                default: return CreatePrimitive(PrimitiveType.Cylinder, new Vector3(1.2f, 0.01f, 1.2f), new Color(0.35f, 0.15f, 0.45f)).transform;
            }
        }

        private GameObject CreatePrimitive(PrimitiveType shape, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (_baseMaterial != null) { renderer.sharedMaterial = _baseMaterial; }
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
            return go;
        }
    }
}
