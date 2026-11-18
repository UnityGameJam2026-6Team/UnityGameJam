using UnityEngine;
using UnityEngine.EventSystems;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UiSfxFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private UnityEngine.UI.Button _button;
        private static GameObject _lastTarget;

        private void Awake()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        public void OnPointerEnter(PointerEventData data) { Navigate(); }
        public void OnSelect(BaseEventData data) { Navigate(); }

        private void Navigate()
        {
            if (!_button.IsInteractable() || _lastTarget == gameObject) { return; }
            _lastTarget = gameObject;
            GameSfxPlayer.Play(ESfxCue.Navigate);
        }

    }
}
