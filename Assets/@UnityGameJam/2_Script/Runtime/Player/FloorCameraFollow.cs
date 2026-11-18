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
        private Mesh _framingMesh;
        private readonly List<Vector3> _modelVertices = new();
        private readonly List<Vector3> _framingPoints = new();
        private readonly RaycastHit[] _obstructions = new RaycastHit[12];

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnDestroy()
        {
            if (_framingMesh != null) { Destroy(_framingMesh); }
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target == null || _settings == null) { return; }
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            _velocity = Vector3.zero;
        }

        public void SetConversationPartner(Transform partner)
        {
            _conversationPartner = partner;
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
            SetConversationFraming(focus, yaw);
        }

        public void SnapToTarget() => SetTarget(_target);

        private void LateUpdate()
        {
            if (_target == null || _settings == null) { return; }
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            float smoothTime = Mathf.Max(0.01f, _settings.CameraSmoothTime);
            transform.position = Vector3.SmoothDamp(transform.position, position, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-2f * Time.deltaTime / smoothTime));
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
            position = ConversationPosition(ConversationMidpoint() + _conversationFocusOffset, rotation, _conversationDistance);
        }

        private Vector3 ConversationMidpoint() => (_target.position + _conversationPartner.position) * 0.5f;

        private float VerticalSlope => Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        private Vector3 ConversationPosition(Vector3 focus, Quaternion rotation, float distance) =>
            focus + rotation * new Vector3(0f, distance * VerticalSlope * 0.12f, -distance);

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
            float vertical = VerticalSlope * _settings.ConversationFrameHeight;
            // 양옆 대사와 지시 패널 사이의 중앙 영역에 두 유닛을 담는다.
            float horizontal = VerticalSlope * _camera.aspect * 0.44f;
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
                        // 애니메이션 전체의 컬링 상자 대신 화면에 보이는 자세를 기준으로 잰다.
                        if (_framingMesh == null) { _framingMesh = new Mesh { name = "ConversationFraming" }; }
                        skin.BakeMesh(_framingMesh);
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
