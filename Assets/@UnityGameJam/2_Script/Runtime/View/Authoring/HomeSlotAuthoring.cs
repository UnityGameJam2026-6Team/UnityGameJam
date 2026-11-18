using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>
    /// 어질러진 작은 물건의 제자리(GDD §3.1, U3). 물건 종류(Book 등)는 표시용이라 B 레인만 쓴다.
    /// 구역은 위치로 판정한다(FacilityLayoutBuilder).
    /// </summary>
    public sealed class HomeSlotAuthoring : MonoBehaviour
    {
        [SerializeField] private int _slotId;
        [Tooltip("U202: Book, Cup, Document, Chair")]
        [SerializeField] private string _itemKind = "Book";

        public int SlotId => _slotId;
        public string ItemKind => _itemKind;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }
}
