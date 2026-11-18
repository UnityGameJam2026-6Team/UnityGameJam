using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemonKing.UI
{
    /// <summary>
    /// One loose sheet of the clipboard list. Holds references and spring state only;
    /// all motion is computed by <see cref="ClipboardListUI"/>.
    /// Only the front sheet (TodoSheet) has a TextSlot.
    /// Clicks on a sheet stop here, so clicking the open to-do list does not close the clipboard
    /// (while closed, a click on the sheet opens it like the board does).
    /// </summary>
    [DisallowMultipleComponent]
    public class ClipboardSheet : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Where the programmer places the to-do text (TextMeshPro etc.). Empty on the decorative back sheets.")]
        public RectTransform textSlot;
        public Image paper;
        public Image shadow;

        [System.NonSerialized] public ClipboardListUI owner;

        // spring state (written by ClipboardListUI)
        [System.NonSerialized] public float open, openVel, target;

        public RectTransform Rect { get { return (RectTransform)transform; } }

        public void OnPointerClick(PointerEventData e) { if (owner != null) owner.NotifySheetClicked(this); }
    }
}
