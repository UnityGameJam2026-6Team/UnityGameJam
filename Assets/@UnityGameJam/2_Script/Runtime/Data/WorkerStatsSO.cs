using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Worker Stats", fileName = "WorkerStats")]
    public sealed class WorkerStatsSO : ScriptableObject
    {
        [SerializeField] private EWorkerType _worker;
        [SerializeField, Min(0f)] private float _moveSpeed;
        [SerializeField, Min(0f)] private float _heavyCarryMultiplier;
        [SerializeField, Min(0f)] private float _clutterCarryMultiplier;
        [SerializeField, Min(0f)] private float _mopSeconds;
        [SerializeField, Min(0f)] private float _digestSeconds;
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
