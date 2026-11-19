using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    [DisallowMultipleComponent]
    public sealed class AssignmentPanelChoices : MonoBehaviour
    {
        [SerializeField] private GameObject _roomChoices;
        [SerializeField] private GameObject _hellhoundChoices;
        [SerializeField] private Button _dispatch;
        [SerializeField] private Button _cancelDispatch;

        internal RectTransform VisibleChoices { get; private set; }

        internal void BindHellhoundCommands(UnityAction dispatch, UnityAction cancel)
        {
            _dispatch.onClick.AddListener(dispatch);
            _cancelDispatch.onClick.AddListener(cancel);
        }

        internal void ShowFor(EWorkerType worker)
        {
            bool isHellhound = worker == EWorkerType.Hellhound;
            _roomChoices.SetActive(!isHellhound);
            _hellhoundChoices.SetActive(isHellhound);
            VisibleChoices = (RectTransform)(isHellhound ? _hellhoundChoices : _roomChoices).transform;
            if (isHellhound) { _dispatch.Select(); }
        }
    }
}
