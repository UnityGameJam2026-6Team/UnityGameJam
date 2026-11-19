using UnityEngine;
using UnityEngine.EventSystems;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UiSfxFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private UnityEngine.UI.Button _button;
        private static GameObject _lastTarget;

        private void Awake()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        public void OnPointerEnter(PointerEventData data) { Navigate(); }
        public void OnPointerExit(PointerEventData data) { ClearTarget(); }
        public void OnSelect(BaseEventData data) { Navigate(); }
        public void OnDeselect(BaseEventData data) { ClearTarget(); }
        private void OnDisable() { ClearTarget(); }

        private void ClearTarget()
        {
            if (_lastTarget == gameObject) { _lastTarget = null; }
        }

        private void Navigate()
        {
            if (!_button.IsInteractable() || _lastTarget == gameObject) { return; }
            _lastTarget = gameObject;
            GameSfxPlayer.Play(ESfxCue.Navigate);
        }
    }
}
