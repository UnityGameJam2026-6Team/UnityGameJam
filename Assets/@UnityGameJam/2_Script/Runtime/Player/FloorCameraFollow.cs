using System.Collections.Generic;
using UnityEngine;
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
        private Transform _target;
        private Transform _conversationPartner;
        private Vector3 _velocity;
        private Quaternion _conversationRotation;
        private Vector3 _conversationFocusOffset;
        private float _conversationDistance;
        private float _conversationScreenCenter;
        private float _conversationScreenHeight;
        private Vector3 _blendFromFocus;
        private Vector3 _blendFromOffset;
        private Quaternion _blendFromRotation;
        private float _blendElapsed;
        private bool _isBlending;
        private readonly List<Vector3> _framingPoints = new();
        private readonly RaycastHit[] _obstructions = new RaycastHit[12];

        private void Awake() => _camera = GetComponent<Camera>();

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target == null || _settings == null) { return; }
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            _velocity = Vector3.zero;
            _isBlending = false;
        }

        public void SetConversationPartner(Transform partner, Vector2 viewportY = default)
        {
            if (_conversationPartner == partner) { return; }
            if (_target != null && _settings != null) { StartBlend(); }
            _conversationPartner = partner;
            if (_target == null || partner == null || _settings == null) { return; }
            _framingPoints.Clear();
            AddFramingPoints(_target);
            AddFramingPoints(partner);
            var bounds = new Bounds(_framingPoints[0], Vector3.zero);
            foreach (Vector3 point in _framingPoints) { bounds.Encapsulate(point); }
            Vector3 focus = ConversationMidpoint();
            focus.y = bounds.center.y;
            _conversationFocusOffset = focus - ConversationMidpoint();
            float availableHeight = viewportY.y - viewportY.x;
            _conversationScreenCenter = availableHeight > 0f ? viewportY.x + viewportY.y - 1f : 0f;
            _conversationScreenHeight = availableHeight > 0f
                ? Mathf.Min(availableHeight * 0.92f, _settings.ConversationFrameHeight) : _settings.ConversationFrameHeight;
            SetConversationFraming(focus);
        }

        public void SnapToTarget() => SetTarget(_target);

        private void LateUpdate()
        {
            if (_target == null || _settings == null) { return; }
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            if (_isBlending)
            {
                BlendPose(position, rotation);
                return;
            }
            float smoothTime = Mathf.Max(0.01f, _settings.CameraSmoothTime);
            transform.position = Vector3.SmoothDamp(transform.position, position, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-2f * Time.deltaTime / smoothTime));
        }

        private void StartBlend()
        {
            _blendFromFocus = ViewFocus;
            _blendFromRotation = transform.rotation;
            _blendFromOffset = Quaternion.Inverse(_blendFromRotation) * (transform.position - _blendFromFocus);
            _blendElapsed = 0f;
            _velocity = Vector3.zero;
            _isBlending = true;
        }

        private void BlendPose(Vector3 position, Quaternion rotation)
        {
            _blendElapsed += Time.deltaTime;
            float fraction = Mathf.Clamp01(_blendElapsed / _settings.ConversationCameraBlendSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, fraction);

            // 초점과 카메라의 상대 위치를 함께 보간해 줌 중에도 대화 상대가 화면 안에 머문다.
            Vector3 focus = ViewFocus;
            Vector3 blendedFocus = Vector3.Lerp(_blendFromFocus, focus, eased);
            Vector3 endOffset = Quaternion.Inverse(rotation) * (position - focus);
            Vector3 offset = Vector3.Lerp(_blendFromOffset, endOffset, eased);
            Quaternion blendedRotation = Quaternion.Slerp(_blendFromRotation, rotation, eased);
            transform.SetPositionAndRotation(blendedFocus + blendedRotation * offset, blendedRotation);
            _isBlending = fraction < 1f;
        }

        private void GetDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 followOffset = new Vector3(0f, _settings.CameraHeight, -_settings.CameraBackDistance);
            if (_conversationPartner == null)
            {
                position = _target.position + followOffset;
                rotation = Quaternion.Euler(_settings.CameraPitch, 0f, 0f);
                return;
            }

            rotation = _conversationRotation;
            position = ConversationPosition(ViewFocus, rotation, _conversationDistance);
        }

        private Vector3 ViewFocus => _conversationPartner == null ? _target.position : ConversationMidpoint() + _conversationFocusOffset;

        private Vector3 ConversationMidpoint() => (_target.position + _conversationPartner.position) * 0.5f;

        private float VerticalSlope => Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        private Vector3 ConversationPosition(Vector3 focus, Quaternion rotation, float distance) =>
            focus + rotation * new Vector3(0f, -distance * VerticalSlope * _conversationScreenCenter, -distance);

        private void SetConversationFraming(Vector3 focus)
        {
            float pitch = _settings.ConversationCameraPitch;
            Frame(focus, pitch);
            if (HasClearView(focus)) { return; }
            // 벽 근처에서도 좌우 방향을 유지하고 더 위에서 대화 장면을 잡는다.
            Frame(focus, pitch + 14f);
            if (HasClearView(focus)) { return; }
            Frame(focus, 80f);
        }

        private void Frame(Vector3 focus, float pitch)
        {
            _conversationRotation = Quaternion.Euler(pitch, 0f, 0f);
            Quaternion inverseRotation = Quaternion.Inverse(_conversationRotation);
            float slope = VerticalSlope;
            float vertical = slope * _conversationScreenHeight;
            float horizontal = slope * _camera.aspect * 0.85f;
            float distance = 2.3f;
            foreach (Vector3 point in _framingPoints)
            {
                Vector3 local = inverseRotation * (point - focus);
                // 상하 패널 사이의 중심이 화면 중앙과 다를 때도 모델 전체를 그 안에 맞춘다.
                float verticalOffset = local.y - local.z * slope * _conversationScreenCenter;
                distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(local.x) / horizontal, Mathf.Abs(verticalOffset) / vertical) - local.z);
            }
            _conversationDistance = distance;
        }

        private bool HasClearView(Vector3 focus)
        {
            Vector3 offset = ConversationPosition(focus, _conversationRotation, _conversationDistance) - focus;
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
                        // 대화는 제자리에서 진행되므로 이동 클립 전체의 컬링 범위 대신 기본 모델에 여유를 둔다.
                        Bounds modelBounds = skin.sharedMesh.bounds;
                        modelBounds.Expand(modelBounds.size * 0.15f);
                        AddBoundsCorners(modelBounds, skin.localToWorldMatrix);
                    }
                    else { AddBoundsCorners(renderer.bounds, Matrix4x4.identity); }
                }
            }
            if (_framingPoints.Count == initialCount)
            {
                AddBoundsCorners(new Bounds(actor.position + Vector3.up * 0.65f, new Vector3(0.65f, 1.3f, 0.65f)), Matrix4x4.identity);
            }
        }

        private void AddBoundsCorners(Bounds bounds, Matrix4x4 localToWorld)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                _framingPoints.Add(localToWorld.MultiplyPoint3x4(point));
            }
        }
    }
}
