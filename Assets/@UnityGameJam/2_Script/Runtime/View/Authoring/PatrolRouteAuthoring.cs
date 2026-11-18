using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 졸개가 방에서 한 바퀴 도는 경로(GDD §5.1-2). 점은 이 오브젝트 기준 로컬 좌표 목록이고, 목록 순서가 순찰 순서다.
    /// 자식 오브젝트로 두지 않는 이유: 인스턴스에 자식을 추가하는 것은 프리팹 규칙상 금지된 오버라이드다.
    /// </summary>
    public sealed class PatrolRouteAuthoring : MonoBehaviour
    {
        [SerializeField] private EZoneId _zone;
        [SerializeField] private List<Vector3> _localPoints = new List<Vector3>();

        public EZoneId Zone => _zone;

        public List<Vector3> GetPoints()
        {
            var points = new List<Vector3>(_localPoints.Count);
            foreach (Vector3 local in _localPoints) { points.Add(transform.TransformPoint(local)); }
            return points;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.HSVToRGB((int)_zone / 9f, 0.5f, 1f);
            List<Vector3> points = GetPoints();
            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.DrawWireSphere(points[i] + Vector3.up * 0.1f, 0.25f);
                Gizmos.DrawLine(points[i] + Vector3.up * 0.1f, points[(i + 1) % points.Count] + Vector3.up * 0.1f);
            }
        }
    }
}
