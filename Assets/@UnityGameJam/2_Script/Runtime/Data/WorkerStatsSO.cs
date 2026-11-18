using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Worker Stats", fileName = "WorkerStats")]
    public sealed class WorkerStatsSO : ScriptableObject
    {
        [SerializeField] private EWorkerType _worker;
        [SerializeField, Min(0f)] private float _moveSpeed;
        [Tooltip("무거운 잔해를 들고 이동할 때 적용하는 속도 배율")]
        [SerializeField, Min(0f)] private float _heavyCarryMultiplier;
        [Tooltip("졸개가 작은 물건을 들고 이동할 때 적용하는 속도 배율")]
        [SerializeField, Min(0f)] private float _clutterCarryMultiplier;
        [Tooltip("졸개가 바닥 얼룩 하나를 닦는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _mopSeconds;
        [Tooltip("슬라임이 폐기물 하나를 소화하는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _digestSeconds;
        [Tooltip("헬하운드가 배출하러 다녀오는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _dispatchSeconds;
        [SerializeField] private bool _canCarryClutter;
        [SerializeField] private bool _canCarryTrash;
        [SerializeField] private bool _canCarryHeavyDebris;
        [SerializeField] private bool _canMop;
        [SerializeField] private bool _canDigestWaste;

        public EWorkerType Worker => _worker;
        public float MoveSpeed => _moveSpeed;
        public float HeavyCarryMultiplier => _heavyCarryMultiplier;
        public float ClutterCarryMultiplier => _clutterCarryMultiplier;
        public float MopSeconds => _mopSeconds;
        public float DigestSeconds => _digestSeconds;
        public float DispatchSeconds => _dispatchSeconds;
        public bool CanCarryClutter => _canCarryClutter;
        public bool CanCarryTrash => _canCarryTrash;
        public bool CanCarryHeavyDebris => _canCarryHeavyDebris;
        public bool CanMop => _canMop;
        public bool CanDigestWaste => _canDigestWaste;
    }
}
