using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>플레이어와 졸개의 시작 위치(GDD §2: 1층 로비, 헬하운드는 현관).</summary>
    public sealed class StartPointAuthoring : MonoBehaviour
    {
        [SerializeField] private EWorkerType _worker;

        public EWorkerType Worker => _worker;

        private void OnDrawGizmos()
        {
            Gizmos.color = _worker == EWorkerType.Player ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
