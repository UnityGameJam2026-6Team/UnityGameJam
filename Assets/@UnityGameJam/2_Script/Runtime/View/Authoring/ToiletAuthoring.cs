using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>층마다 1개인 화장실(GDD §2, §7.1). 범위는 BoxCollider, 층은 부모 FloorAuthoring에서 정한다.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ToiletAuthoring : MonoBehaviour
    {
        [Tooltip("물걸레를 빌리고 반납하는 위치. 비우면 화장실 범위의 바닥 중심")]
        [SerializeField] private Transform _mopPickup;

        private BoxCollider _box;

        public Vector3 MopPickupPosition
        {
            get
            {
                if (_mopPickup != null) { return _mopPickup.position; }
                Bounds bounds = GetWorldBounds();
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
        }

        public Bounds GetWorldBounds()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            return AuthoringBounds.FromBox(_box);
        }

        private void OnDrawGizmos()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            AuthoringBounds.DrawBox(_box, Color.white);
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(MopPickupPosition + Vector3.up * 0.2f, 0.2f);
        }
    }
}
