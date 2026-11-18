using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>FL_Floor 프리팹 루트. 층 번호와 층 범위(로컬 XZ, 중심 기준)를 들고 있다.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorAuthoring : MonoBehaviour
    {
        [SerializeField] private EFloor _floor;
        [Tooltip("층 바깥 범위(m). X, Z 순서. 중심은 이 오브젝트 위치")]
        [SerializeField] private Vector2 _size = new Vector2(36f, 28f);

        public EFloor Floor => _floor;

        public Bounds GetWorldBounds()
        {
            var bounds = new Bounds(transform.position + Vector3.up * 1.5f, new Vector3(_size.x, 4f, _size.y));
            return bounds;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Bounds bounds = GetWorldBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
