using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>오염 생성 지점. 구역은 위치로 판정한다(FacilityLayoutBuilder).</summary>
    public sealed class SpawnPointAuthoring : MonoBehaviour
    {
        [SerializeField] private EContaminationType _type;

        public EContaminationType Type => _type;

        private void OnDrawGizmos()
        {
            Gizmos.color = _type switch
            {
                EContaminationType.HeavyDebris => new Color(0.5f, 0.3f, 0.1f),
                EContaminationType.Trash => Color.yellow,
                EContaminationType.ClutterItem => Color.cyan,
                _ => new Color(0.4f, 0.2f, 0.6f),
            };
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.15f, 0.15f);
        }
    }
}
