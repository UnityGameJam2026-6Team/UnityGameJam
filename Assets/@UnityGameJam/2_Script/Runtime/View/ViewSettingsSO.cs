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
        [Header("완료 연출")]
        [Tooltip("전체 청소 완료 후 남은 카운트다운을 넘기는 실제 시간(초)")]
        [SerializeField, Min(0.1f)] private float _completionCountdownSeconds = 2f;
        public float CompletionCountdownSeconds => Mathf.Max(0.1f, _completionCountdownSeconds);

        [Header("디버그 판")]
        [Tooltip("GameRule을 사용하지 않는 판에서 대상을 고르는 반경(m)")]
        [SerializeField, Min(0.01f)] private float _interactRadius = 1.5f;

        [Header("플레이어 조작")]
        [Tooltip("이동 또는 대화 방향으로 회전하는 속도(도/초)")]
        [SerializeField, Min(1f)] private float _playerTurnSpeed = 720f;

        [Serializable]
        internal struct OpeningCameraShot
        {
            public Vector3 StartOffset;
            public Vector3 EndOffset;
            public Vector3 FocusOffset;
        }

        [Header("시작 연출")]
        [SerializeField, Min(0.1f)] private float _openingLineDistance = 3f;
        [SerializeField, Min(0.1f)] private float _openingMinionSpacing = 2f;
        [SerializeField] private OpeningCameraShot _openingWide = new OpeningCameraShot
        {
            StartOffset = new Vector3(-7f, 9f, -8f),
            EndOffset = new Vector3(-5f, 8f, -9f),
            FocusOffset = new Vector3(0f, 0.9f, 0f)
        };
        [SerializeField] private OpeningCameraShot _openingShoulder = new OpeningCameraShot
        {
            StartOffset = new Vector3(-1.5f, 4f, 9f),
            EndOffset = new Vector3(-0.6f, 3.8f, 8f),
            FocusOffset = new Vector3(0f, 0.85f, -1.1f)
        };
        [SerializeField] private OpeningCameraShot _openingMinions = new OpeningCameraShot
        {
            StartOffset = new Vector3(-2.8f, 2.5f, 7f),
            EndOffset = new Vector3(2.8f, 2.5f, 7f),
            FocusOffset = new Vector3(0f, 0.8f, -1.2f)
        };
        [SerializeField] private OpeningCameraShot _openingSide = new OpeningCameraShot
        {
            StartOffset = new Vector3(-8f, 4f, -4f),
            EndOffset = new Vector3(-7.4f, 4.5f, -5.4f),
            FocusOffset = new Vector3(0f, 0.9f, 0f)
        };

        internal float OpeningLineDistance => _openingLineDistance;
        internal float OpeningMinionSpacing => _openingMinionSpacing;

        internal OpeningCameraShot GetOpeningShot(int number) => number switch
        {
            5 => _openingWide,
            4 => _openingShoulder,
            3 => _openingMinions,
            _ => _openingSide
        };

        [Serializable]
        internal struct EndingShot
        {
            public Vector3 StartPosition;
            public Vector3 EndPosition;
            public Vector3 StartFocus;
            public Vector3 EndFocus;

            public EndingShot(Vector3 startPosition, Vector3 endPosition, Vector3 startFocus, Vector3 endFocus)
            {
                StartPosition = startPosition;
                EndPosition = endPosition;
                StartFocus = startFocus;
                EndFocus = endFocus;
            }
        }

        [Serializable]
        internal sealed class EndingSettings
        {
            [Min(0.01f)] public float DoorSeconds = 0.65f;
            [Min(0.01f)] public float WalkSeconds = 2f;
            [Min(0.01f)] public float FaceoffSeconds = 0.6f;
            [Min(0.01f)] public float SuccessSeconds = 1.65f;
            [Min(0.01f)] public float DogRunSeconds = 0.42f;
            [Min(0f)] public float DogRunAt = 0.62f;
            public float DoorAngle = 100f;
            public Vector3 KingStart = new Vector3(0f, 0f, -0.75f);
            public Vector3 KingStop = new Vector3(0f, 0f, 2.05f);
            public Vector3 PlayerStart = new Vector3(1.4f, 0f, 5.1f);
            public Vector3 FailurePlayerStart = new Vector3(0f, 0f, 10.35f);
            public Vector3[] FailureMinionOffsets =
            {
                new Vector3(-2f, 0f, -2.15f),
                new Vector3(2.1f, 0f, -2.1f),
                new Vector3(-3.1f, 0f, -0.6f),
                new Vector3(2.8f, 0f, -0.38f)
            };
            public Vector3 DogStart = new Vector3(-3.45f, 0f, 1.6f);
            public Vector3 DogStop = new Vector3(-2.05f, 0f, 2.9f);
            [Min(0f)] public float SuccessStep = 0.55f;
            [Min(0.01f)] public float SuccessStepSeconds = 0.45f;
            public AnimationClip DogGreeting;
            public GameObject ThrowTrash;
            [Min(0.01f)] public float ThrowLookSeconds = 1.2f;
            [Range(0f, 90f)] public float ThrowLookAngle = 48f;
            [Min(0f)] public float ThrowGripAt = 1f;
            [Min(0.01f)] public float ThrowReleaseAt = 2.48f;
            [Min(0.01f)] public float ThrowFlightSeconds = 0.82f;
            [Min(0.01f)] public float ImpactHoldSeconds = 0.11f;
            [Min(0.01f)] public float ImpactRecoverySeconds = 0.62f;
            [Min(0.01f)] public float ThrowTargetHeight = 1.17f;
            [Min(0f)] public float ThrowArcHeight = 1.5f;
            [Min(0f)] public float ImpactRecoil = 0.2f;
            public EndingShot Door = new EndingShot(new Vector3(1.15f, 0.42f, 3.55f), new Vector3(1.15f, 0.42f, 3.55f), new Vector3(0f, 0.24f, -0.8f), new Vector3(0f, 0.24f, -0.8f));
            public EndingShot Entrance = new EndingShot(new Vector3(1.15f, 0.42f, 3.55f), new Vector3(3f, 2.9f, 9.45f), new Vector3(0f, 0.24f, -0.8f), new Vector3(-0.55f, 1.16f, 2.5f));
            public EndingShot Faceoff = new EndingShot(new Vector3(8.1f, 3.35f, 4.7f), new Vector3(8.1f, 3.35f, 4.7f), new Vector3(0.6f, 1.02f, 4f), new Vector3(0.6f, 1.02f, 4f));
            public EndingShot Success = new EndingShot(new Vector3(8.1f, 3.35f, 4.7f), new Vector3(9.2f, 4f, 5.1f), new Vector3(0.6f, 1.02f, 4f), new Vector3(0.5f, 1.05f, 4f));
            public EndingShot Throw = new EndingShot(new Vector3(12.6f, 6.1f, 7.7f), new Vector3(12.95f, 6.25f, 7.7f), new Vector3(0.1f, 1.2f, 6.5f), new Vector3(0.1f, 1.2f, 6.5f));
            public EndingShot Impact = new EndingShot(new Vector3(12.95f, 6.25f, 7.7f), new Vector3(13.2f, 6.4f, 7.7f), new Vector3(0.1f, 1.2f, 6.5f), new Vector3(0.1f, 1.2f, 6.5f));
        }

        [Header("마왕 도착 연출")]
        [SerializeField] private EndingSettings _ending = new EndingSettings();
        internal EndingSettings Ending => _ending;

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
