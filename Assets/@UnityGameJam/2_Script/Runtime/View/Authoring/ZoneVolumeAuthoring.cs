using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>판정 구역 사각형 하나. 한 구역에 여러 개를 둘 수 있다(로비처럼 모양이 복잡한 경우).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ZoneVolumeAuthoring : MonoBehaviour
    {
        [SerializeField] private EZoneId _zone;

        private BoxCollider _box;

        public EZoneId Zone => _zone;

        public Bounds GetWorldBounds()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            return AuthoringBounds.FromBox(_box);
        }

        private void OnDrawGizmos()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            Color color = Color.HSVToRGB((int)_zone / 9f, 0.7f, 1f);
            AuthoringBounds.DrawBox(_box, color);
        }
    }
}
