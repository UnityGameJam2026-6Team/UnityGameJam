using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>
    /// B 레인 연출 값(규칙에 영향을 주지 않는 값). 에셋은 3_Data/ScriptableObject/View/에 둔다.
    /// 값은 GDD에 없어 임시값이다(U207). 플레이테스트에서 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityGameJam/View Settings", fileName = "ViewSettings")]
    public sealed class ViewSettingsSO : ScriptableObject
    {
        [Header("상호작용")]
        [Tooltip("E키 대상 고르기 반경(m)")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Header("카메라")]
        [Tooltip("플레이어 위 높이(m)")]
        [SerializeField] private float _cameraHeight = 18f;
        [Tooltip("플레이어 뒤쪽(-Z) 거리(m)")]
        [SerializeField] private float _cameraBackDistance = 12f;
        [Tooltip("내려다보는 각도(도)")]
        [SerializeField] private float _cameraPitch = 56f;
        [Tooltip("추적 부드러움(초). 작을수록 빠르게 따라간다")]
        [SerializeField] private float _cameraSmoothTime = 0.15f;

        [Header("계단 (B3)")]
        [Tooltip("계단으로 층을 옮길 때 걸리는 시간(초). 임시값")]
        [SerializeField] private float _stairTransitSeconds = 2f;

        public float InteractRadius => _interactRadius;
        public float CameraHeight => _cameraHeight;
        public float CameraBackDistance => _cameraBackDistance;
        public float CameraPitch => _cameraPitch;
        public float CameraSmoothTime => _cameraSmoothTime;
        public float StairTransitSeconds => _stairTransitSeconds;
    }
}
