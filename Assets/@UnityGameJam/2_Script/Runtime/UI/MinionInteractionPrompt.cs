using System;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    [DefaultExecutionOrder(50)]
    public sealed class MinionInteractionPrompt : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _headGap = 0.12f;

        private MatchBootstrap _bootstrap;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _rect;
        private CanvasGroup _visibility;
        private MinionAgent _target;
        private Renderer[] _renderers = Array.Empty<Renderer>();

        private void Awake()
        {
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _canvasRect = (RectTransform)_canvas.transform;
            _rect = (RectTransform)transform;
            _visibility = GetComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            MinionAgent target = FindTarget();
            if (target != _target)
            {
                _target = target;
                GameObject model = target != null ? target.GetComponentInChildren<ArtSlot>()?.Model : null;
                _renderers = model != null ? model.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
            }

            bool isVisible = target != null && TryPositionAbove(target);
            _visibility.alpha = isVisible ? 1f : 0f;
        }

        private MinionAgent FindTarget()
        {
            if (_bootstrap == null || _bootstrap.Session?.MatchState != EMatchState.Running ||
                _bootstrap.PlayerInteractor?.AvailableConversationTarget is not EWorkerType worker)
            {
                return null;
            }
            foreach (MinionAgent minion in _bootstrap.Minions)
            {
                if (minion != null && minion.Worker == worker && _bootstrap.CanSeeMinion(minion)) { return minion; }
            }
            return null;
        }

        private bool TryPositionAbove(MinionAgent target)
        {
            Camera camera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
            if (camera == null) { return false; }
            Vector3 point = target.Position;
            bool hasVisibleModel = false;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) { continue; }
                point.y = Mathf.Max(point.y, renderer.bounds.max.y);
                hasVisibleModel = true;
            }
            if (!hasVisibleModel) { return false; }

            point.y += _headGap;
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0f || !camera.pixelRect.Contains(screen)) { return false; }
            Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, canvasCamera, out Vector2 local))
            {
                return false;
            }
            _rect.position = _canvasRect.TransformPoint(local);
            return true;
        }
    }
}
