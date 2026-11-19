using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>
    /// 플레이어를 따라가며 내려다보는 카메라(GDD §9, Spec Player 규칙 7).
    /// 층은 X축으로 100m 떨어져 있어서 다른 층은 화면에 들어오지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FloorCameraFollow : MonoBehaviour
    {
        [SerializeField] private ViewSettingsSO _settings;

        private Camera _camera;
        private Canvas _screenFadeCanvas;
        private CanvasRenderer _screenFadeRenderer;
        private Transform _target;
        private Transform _conversationPartner;
        private Vector3 _focus;
        private Vector3 _focusVelocity;
        private Vector3 _cameraOffset;
        private Quaternion _viewRotation;
        private Vector3 _transitionFocus;
        private Vector3 _transitionOffset;
        private Quaternion _transitionRotation;
        private float _transitionElapsed;
        private bool _isOpening;
        private bool _isEnding;

        private float TransitionDuration => Mathf.Max(0.25f, _settings.CameraSmoothTime * 3f);
        private Quaternion _conversationRotation;
        private Vector3 _conversationFocusOffset;
        private float _conversationDistance;
        private float _conversationScreenCenter;
        private float _conversationScreenHeight;
        private bool _usesConversationViewport;
        private Mesh _framingMesh;
        private readonly List<Vector3> _modelVertices = new();
        private readonly List<Vector3> _framingPoints = new();
        private readonly RaycastHit[] _obstructions = new RaycastHit[12];

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            var overlay = new GameObject("Screen Fade", typeof(RectTransform), typeof(Canvas));
            if (overlay.scene != gameObject.scene) { SceneManager.MoveGameObjectToScene(overlay, gameObject.scene); }
            _screenFadeCanvas = overlay.GetComponent<Canvas>();
            _screenFadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _screenFadeCanvas.targetDisplay = _camera.targetDisplay;
            _screenFadeCanvas.sortingOrder = short.MaxValue;
            _screenFadeCanvas.enabled = false;

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.SetParent(overlay.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.color = Color.black;
            image.raycastTarget = false;
            _screenFadeRenderer = image.canvasRenderer;
        }

        internal void SetScreenFade(float opacity)
        {
            if (_screenFadeCanvas == null) { return; }
            _screenFadeRenderer.SetAlpha(opacity);
            bool visible = opacity > 0f && isActiveAndEnabled;
            if (_screenFadeCanvas.enabled != visible) { _screenFadeCanvas.enabled = visible; }
        }

        private void OnDisable() => SetScreenFade(0f);

        private void OnDestroy()
        {
            if (_screenFadeCanvas != null) { Destroy(_screenFadeCanvas.gameObject); }
            if (_framingMesh != null) { Destroy(_framingMesh); }
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target == null || _settings == null) { return; }
            GetDesiredView(out _focus, out _viewRotation, out _cameraOffset);
            transform.SetPositionAndRotation(_focus + _viewRotation * _cameraOffset, _viewRotation);
            _focusVelocity = Vector3.zero;
            _transitionElapsed = TransitionDuration;
        }

        public void SetConversationPartner(Transform partner, Vector2 viewportY = default)
        {
            if (_conversationPartner == partner) { return; }
            _conversationPartner = partner;
            _transitionFocus = _focus;
            _transitionRotation = _viewRotation;
            _transitionOffset = _cameraOffset;
            _transitionElapsed = 0f;
            _focusVelocity = Vector3.zero;
            if (_target == null || partner == null || _settings == null) { return; }
            Vector3 line = Vector3.ProjectOnPlane(partner.position - _target.position, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, line);
            if (Vector3.Dot(side, Vector3.forward) < 0f) { side = -side; }
            float yaw = side.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(Vector3.forward, side, Vector3.up) : 0f;
            _framingPoints.Clear();
            AddFramingPoints(_target);
            AddFramingPoints(partner);
            var bounds = new Bounds(_framingPoints[0], Vector3.zero);
            foreach (Vector3 point in _framingPoints) { bounds.Encapsulate(point); }
            Vector3 focus = ConversationMidpoint();
            focus.y = bounds.center.y;
            _conversationFocusOffset = focus - ConversationMidpoint();
            float availableHeight = viewportY.y - viewportY.x;
            _usesConversationViewport = availableHeight > 0f;
            _conversationScreenCenter = _usesConversationViewport ? viewportY.x + viewportY.y - 1f : -0.12f;
            _conversationScreenHeight = _usesConversationViewport ? Mathf.Min(availableHeight * 0.92f, _settings.ConversationFrameHeight) : _settings.ConversationFrameHeight;
            SetConversationFraming(focus, yaw);
        }

        public void SnapToTarget() => SetTarget(_target);

        internal void ShowOpeningShot(Vector3 center, int number, float progress)
        {
            if (_target == null || _settings == null) { return; }
            _isOpening = true;
            float amount = Mathf.SmoothStep(0f, 1f, progress);
            ViewSettingsSO.OpeningCameraShot shot = _settings.GetOpeningShot(number);
            Vector3 lookAt = center + shot.FocusOffset;
            Vector3 position = center + Vector3.Lerp(shot.StartOffset, shot.EndOffset, amount);
            if (number == 1)
            {
                GetDesiredView(out Vector3 focus, out Quaternion followRotation, out Vector3 offset);
                Vector3 followPosition = focus + followRotation * offset;
                Vector3 followLookAt = followPosition + followRotation * Vector3.forward * offset.magnitude;
                position = Vector3.Lerp(center + shot.EndOffset, followPosition, amount);
                lookAt = Vector3.Lerp(lookAt, followLookAt, amount);
            }
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        internal void EndOpening()
        {
            _isOpening = false;
            SnapToTarget();
        }

        internal void ShowEndingShot(Vector3 position, Vector3 focus)
        {
            _isEnding = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
        }

        internal void ClearEndingShot()
        {
            _isEnding = false;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_isOpening || _isEnding || _target == null || _settings == null) { return; }
            GetDesiredView(out Vector3 focus, out Quaternion rotation, out Vector3 cameraOffset);
            if (_transitionElapsed < TransitionDuration)
            {
                _transitionElapsed += Time.deltaTime;
                float amount = Mathf.SmoothStep(0f, 1f, _transitionElapsed / TransitionDuration);
                _focus = Vector3.Lerp(_transitionFocus, focus, amount);
                _viewRotation = Quaternion.Slerp(_transitionRotation, rotation, amount);
                _cameraOffset = Vector3.Lerp(_transitionOffset, cameraOffset, amount);
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, _settings.CameraSmoothTime);
                _focus = Vector3.SmoothDamp(_focus, focus, ref _focusVelocity, smoothTime);
                float amount = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
                _viewRotation = Quaternion.Slerp(_viewRotation, rotation, amount);
                _cameraOffset = Vector3.Lerp(_cameraOffset, cameraOffset, amount);
            }
            // 카메라가 두 위치 사이의 직선을 가로지르지 않고 초점 주변을 돌아간다.
            transform.SetPositionAndRotation(_focus + _viewRotation * _cameraOffset, _viewRotation);
        }

        private void GetDesiredView(out Vector3 focus, out Quaternion rotation, out Vector3 offset)
        {
            if (_conversationPartner == null)
            {
                focus = _target.position;
                rotation = Quaternion.Euler(_settings.CameraPitch, 0f, 0f);
                Vector3 followOffset = new Vector3(0f, _settings.CameraHeight, -_settings.CameraBackDistance);
                offset = Quaternion.Inverse(rotation) * followOffset;
                return;
            }

            focus = ConversationMidpoint() + _conversationFocusOffset;
            rotation = _conversationRotation;
            offset = ConversationOffset(_conversationDistance);
        }

        private Vector3 ConversationMidpoint() => (_target.position + _conversationPartner.position) * 0.5f;

        private float VerticalSlope => Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        private Vector3 ConversationOffset(float distance) =>
            new Vector3(0f, -distance * VerticalSlope * _conversationScreenCenter, -distance);

        private void SetConversationFraming(Vector3 focus, float yaw)
        {
            float pitch = _settings.ConversationCameraPitch;
            Frame(focus, pitch, yaw);
            if (HasClearView(focus)) { return; }
            Frame(focus, pitch, yaw + 180f);
            if (HasClearView(focus)) { return; }
            // 벽이 양쪽에 가까우면 조금 더 위에서 장면을 잡는다.
            Frame(focus, pitch + 14f, yaw);
            if (!HasClearView(focus)) { Frame(focus, pitch + 14f, yaw + 180f); }
        }

        private void Frame(Vector3 focus, float pitch, float yaw)
        {
            _conversationRotation = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion inverseRotation = Quaternion.Inverse(_conversationRotation);
            float vertical = VerticalSlope * _conversationScreenHeight;
            // 오른쪽 대화 패널을 피하도록 화면 중앙에 두 유닛을 담는다.
            float horizontal = VerticalSlope * _camera.aspect * (_usesConversationViewport ? 0.85f : 0.44f);
            float distance = 2.3f;
            foreach (Vector3 point in _framingPoints)
            {
                Vector3 local = inverseRotation * (point - focus);
                distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(local.x) / horizontal, Mathf.Abs(local.y) / vertical) - local.z);
            }
            _conversationDistance = distance;
        }

        private bool HasClearView(Vector3 focus)
        {
            Vector3 offset = _conversationRotation * ConversationOffset(_conversationDistance);
            int count = Physics.RaycastNonAlloc(focus, offset.normalized, _obstructions, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _obstructions[i].collider;
                if (hit.GetComponentInParent<PlayerBody>() != null || hit.GetComponentInParent<MinionAgent>() != null) { continue; }
                return false;
            }
            return true;
        }

        private void AddFramingPoints(Transform actor)
        {
            int initialCount = _framingPoints.Count;
            ArtSlot slot = actor.GetComponentInChildren<ArtSlot>();
            if (slot != null && slot.Model != null)
            {
                foreach (Renderer renderer in slot.Model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) { continue; }
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        // 애니메이션 전체의 컬링 상자 대신 화면에 보이는 자세를 기준으로 잰다.
                        if (_framingMesh == null) { _framingMesh = new Mesh { name = "ConversationFraming" }; }
                        skin.BakeMesh(_framingMesh, true);
                        _framingMesh.GetVertices(_modelVertices);
                        Matrix4x4 localToWorld = skin.localToWorldMatrix;
                        int step = Mathf.Max(1, _modelVertices.Count / 2048);
                        for (int i = 0; i < _modelVertices.Count; i += step)
                        {
                            _framingPoints.Add(localToWorld.MultiplyPoint3x4(_modelVertices[i]));
                        }
                    }
                    else { AddBoundsCorners(renderer.bounds); }
                }
            }
            if (_framingPoints.Count == initialCount)
            {
                AddBoundsCorners(new Bounds(actor.position + Vector3.up * 0.65f, new Vector3(0.65f, 1.3f, 0.65f)));
            }
        }

        private void AddBoundsCorners(Bounds bounds)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                _framingPoints.Add(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
            }
        }
    }
}
