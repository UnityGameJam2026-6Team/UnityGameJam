using System;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// B 레인 월드·표현 및 이동 조정 값. 에셋은 3_Data/ScriptableObject/View/에 둔다.
    /// 값은 GDD에 없어 임시값이다(U207). 플레이테스트에서 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityGameJam/View Settings", fileName = "ViewSettings")]
    public sealed class ViewSettingsSO : ScriptableObject
    {
        [Header("디버그 판")]
        [Tooltip("GameRule을 사용하지 않는 판에서 대상을 고르는 반경(m)")]
        [SerializeField, Min(0.01f)] private float _interactRadius = 1.5f;

        [Header("플레이어 조작")]
        [Tooltip("이동 또는 대화 방향으로 회전하는 속도(도/초)")]
        [SerializeField, Min(1f)] private float _playerTurnSpeed = 720f;

        [Header("카메라")]
        [Tooltip("플레이어 위 높이(m)")]
        [SerializeField] private float _cameraHeight = 18f;
        [Tooltip("플레이어 뒤쪽(-Z) 거리(m)")]
        [SerializeField] private float _cameraBackDistance = 12f;
        [Tooltip("내려다보는 각도(도)")]
        [SerializeField] private float _cameraPitch = 56f;
        [Tooltip("추적 부드러움(초). 작을수록 빠르게 따라간다")]
        [SerializeField] private float _cameraSmoothTime = 0.15f;
        [SerializeField, Range(10f, 55f)] private float _conversationCameraPitch = 24f;
        [SerializeField, Range(0.35f, 0.75f)] private float _conversationFrameHeight = 0.67f;

        [Header("계단")]
        [Tooltip("플레이어와 졸개가 계단으로 층을 옮기는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _stairTransitSeconds = 2f;
        [Tooltip("플레이어가 계단을 자동으로 타기 시작하는 거리(m)")]
        [SerializeField, Min(0.1f)] private float _stairApproachRadius = 0.85f;

        [Serializable]
        private struct ClutterEntry
        {
            public string Kind;
            public GameObject Prefab;
        }

        [Header("오염 표시")]
        [SerializeField] private GameObject[] _heavyDebrisPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _trashPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _stainPrefabs = Array.Empty<GameObject>();
        [SerializeField] private ClutterEntry[] _clutterPrefabs = Array.Empty<ClutterEntry>();
        [SerializeField] private GameObject _mopPrefab;
        [SerializeField] private float _carryOffset = 0.2f;
        [SerializeField] private Vector3 _mopOffset = new Vector3(0.45f, 0f, 0f);

        [Header("이동·애니메이션 조정")]
        [Tooltip("플레이어와 모든 졸개의 이동 속도 배율에 곱합니다. 1은 기본 배율입니다.")]
        [SerializeField] private float _globalMoveRate = 1f;
        [Tooltip("플레이어와 모든 졸개의 애니메이션 재생 배율에 곱합니다. 1은 기본 배율입니다.")]
        [SerializeField] private float _globalAnimationRate = 1f;
        [SerializeField] private UnitMotionTuning _playerMotion;
        [SerializeField] private UnitMotionTuning _goblinMotion;
        [SerializeField] private UnitMotionTuning _orcMotion;
        [SerializeField] private UnitMotionTuning _skeletonMotion;
        [SerializeField] private UnitMotionTuning _slimeMotion;
        [SerializeField] private UnitMotionTuning _hellhoundMotion;

        public UnitMotionTuning GetMotionTuning(EWorkerType worker) => (worker switch
        {
            EWorkerType.Player => _playerMotion,
            EWorkerType.Goblin => _goblinMotion,
            EWorkerType.Orc => _orcMotion,
            EWorkerType.Skeleton => _skeletonMotion,
            EWorkerType.Slime => _slimeMotion,
            EWorkerType.Hellhound => _hellhoundMotion,
            _ => default
        }).WithGlobalRates(_globalMoveRate, _globalAnimationRate);

        public GameObject MopPrefab => _mopPrefab;
        public float CarryOffset => _carryOffset;
        public Vector3 MopOffset => _mopOffset;

        public GameObject ResolvePrefab(ItemSnapshot item, string kind)
        {
            if (item.Type == EContaminationType.ClutterItem)
            {
                foreach (ClutterEntry entry in _clutterPrefabs)
                {
                    if (entry.Kind == kind && kind != null) { return entry.Prefab; }
                }
                return _clutterPrefabs.Length > 0 ? _clutterPrefabs[0].Prefab : null;
            }
            GameObject[] prefabs = item.Type switch
            {
                EContaminationType.HeavyDebris => _heavyDebrisPrefabs,
                EContaminationType.Trash => _trashPrefabs,
                EContaminationType.FloorStain => _stainPrefabs,
                _ => Array.Empty<GameObject>()
            };
            int index = ContaminationViewRules.ChoosePrefabIndex(item.ItemId, prefabs.Length);
            return index < 0 ? null : prefabs[index];
        }

        public float InteractRadius => _interactRadius;
        public float PlayerTurnSpeed => _playerTurnSpeed;
        public float CameraHeight => _cameraHeight;
        public float CameraBackDistance => _cameraBackDistance;
        public float CameraPitch => _cameraPitch;
        public float CameraSmoothTime => _cameraSmoothTime;
        public float ConversationCameraPitch => _conversationCameraPitch;
        public float ConversationFrameHeight => _conversationFrameHeight;
        public float StairTransitSeconds => _stairTransitSeconds;
        public float StairApproachRadius => _stairApproachRadius;
    }
}
