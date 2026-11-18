using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>
    /// 계단 출입 지점. 층은 부모 FloorAuthoring에서 정한다.
    /// 층간 이동(대기 후 다른 층 계단으로 이동)은 B3의 StairPortal이 이 지점을 써서 처리한다.
    /// </summary>
    public sealed class StairAuthoring : MonoBehaviour
    {
        public Vector3 EntryPosition => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }
    }
}
