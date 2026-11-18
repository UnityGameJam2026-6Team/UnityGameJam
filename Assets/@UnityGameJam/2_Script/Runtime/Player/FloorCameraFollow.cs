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

        private Transform _target;
        private Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target == null || _settings == null) { return; }
            transform.SetPositionAndRotation(GetDesiredPosition(), Quaternion.Euler(_settings.CameraPitch, 0f, 0f));
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_target == null || _settings == null) { return; }
            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, _settings.CameraSmoothTime);
            transform.rotation = Quaternion.Euler(_settings.CameraPitch, 0f, 0f);
        }

        private Vector3 GetDesiredPosition()
        {
            return _target.position + new Vector3(0f, _settings.CameraHeight, -_settings.CameraBackDistance);
        }
    }
}
