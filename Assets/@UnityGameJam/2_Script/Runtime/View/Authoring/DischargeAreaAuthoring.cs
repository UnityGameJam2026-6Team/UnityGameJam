using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>1층 현관 배출 구역(GDD §7.2). 이 범위 안의 폐기물만 헬하운드가 가져간다.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DischargeAreaAuthoring : MonoBehaviour
    {
        private BoxCollider _box;

        public Bounds GetWorldBounds()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            return AuthoringBounds.FromBox(_box);
        }

        private void OnDrawGizmos()
        {
            if (_box == null) { _box = GetComponent<BoxCollider>(); }
            AuthoringBounds.DrawBox(_box, new Color(1f, 0.5f, 0f));
        }
    }
}
