using System;
using UnityEngine;
using UnityGameJam.Contract;

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
        [Tooltip("상호작용 대상을 고르는 반경(m)")]
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
        public float CameraHeight => _cameraHeight;
        public float CameraBackDistance => _cameraBackDistance;
        public float CameraPitch => _cameraPitch;
        public float CameraSmoothTime => _cameraSmoothTime;
        public float StairTransitSeconds => _stairTransitSeconds;
    }
}
