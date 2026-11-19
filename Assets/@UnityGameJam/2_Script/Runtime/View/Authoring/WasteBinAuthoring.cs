using UnityEngine;

namespace UnityGameJam.View
{
    [DisallowMultipleComponent]
    public sealed class WasteBinAuthoring : MonoBehaviour
    {
        [SerializeField] private Vector3 _useOffset = new Vector3(0f, 0f, 1f);

        public Vector3 UsePosition => transform.TransformPoint(_useOffset);
    }
}
