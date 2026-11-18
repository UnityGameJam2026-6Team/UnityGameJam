using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnityGameJam.UI
{
    public sealed class NameInputPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _checkButton;
        [SerializeField] private NicknameImeInput _imeInput;
        [SerializeField] private InputActionAsset _inputActions;

        private Action _onRegistered;
        private BaseInputModule _inputModule;
        private BaseInput _previousInput;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private bool _cancelRequested;
        private bool _submitRequested;
        private bool _focusRequested;
        private bool _enterDuringComposition;

        public bool IsOpen => _onRegistered != null;

        private void Awake()
        {
            _checkButton.onClick.AddListener(Confirm);
            _input.onSubmit.AddListener(OnInputSubmit);
            _input.onEndEdit.AddListener(OnInputEndEdit);
            _submitAction = _inputActions.FindAction("Nickname/Confirm", true);
            _cancelAction = _inputActions.FindAction("Nickname/Cancel", true);
            _submitAction.performed += OnEnter;
            _cancelAction.performed += OnEscape;
        }

        public void Open(Action onRegistered)
        {
            _onRegistered = onRegistered;
            gameObject.SetActive(true);
            _inputModule = EventSystem.current.currentInputModule;
            _previousInput = _inputModule.inputOverride;
            _inputModule.inputOverride = _imeInput;
            _input.SetTextWithoutNotify(PlayerNickname.Current);
            _submitAction.Enable();
            _cancelAction.Enable();
            Focus();
        }

        private void OnEscape(InputAction.CallbackContext context)
        {
            Cancel();
        }

        private void OnInputEndEdit(string value)
        {
            if (_input.wasCanceled) { _cancelRequested = true; }
        }

        private void OnEnter(InputAction.CallbackContext context)
        {
            _enterDuringComposition = _imeInput.compositionString.Length > 0;
            _submitRequested = !_enterDuringComposition;
        }

        private void OnInputSubmit(string value)
        {
            if (_enterDuringComposition || _imeInput.compositionString.Length > 0)
            {
                // 조합 중인 글자를 확정하고 입력창 포커스를 유지한다.
                EventSystem.current.SetSelectedGameObject(null);
                _focusRequested = true;
                _submitRequested = false;
                return;
            }

            _submitRequested = true;
        }

        private void LateUpdate()
        {
            if (_cancelRequested)
            {
                Cancel();
                return;
            }

            bool submit = _submitRequested;
            _submitRequested = false;
            _enterDuringComposition = false;
            if (submit) { Confirm(); }
            if (_focusRequested)
            {
                _focusRequested = false;
                Focus();
            }
        }

        private void Confirm()
        {
            if (!IsOpen) { return; }

            // 확인 버튼으로 끝낸 경우 마지막 한글 조합도 입력값에 포함한다.
            EventSystem.current.SetSelectedGameObject(null);
            string nickname = _input.text.Trim();
            if (nickname.Length == 0)
            {
                Focus();
                return;
            }

            PlayerNickname.Register(nickname);
            Action onRegistered = _onRegistered;
            gameObject.SetActive(false);
            onRegistered();
        }

        private void Cancel()
        {
            if (!IsOpen) { return; }

            // 포커스를 해제할 때 TMP가 미확정 한글 조합을 입력값에 넣지 않도록 먼저 비운다.
            _imeInput.imeCompositionMode = IMECompositionMode.Auto;
            EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false);
        }

        private void Focus()
        {
            _input.Select();
            _input.ActivateInputField();
        }

        private void OnDisable()
        {
            _submitAction?.Disable();
            _cancelAction?.Disable();
            if (_inputModule != null && _inputModule.inputOverride == _imeInput)
            {
                _inputModule.inputOverride = _previousInput;
            }

            _inputModule = null;
            _previousInput = null;
            _onRegistered = null;
            _cancelRequested = false;
            _submitRequested = false;
            _focusRequested = false;
            _enterDuringComposition = false;
        }

        private void OnDestroy()
        {
            if (_submitAction != null) { _submitAction.performed -= OnEnter; }
            if (_cancelAction != null) { _cancelAction.performed -= OnEscape; }
        }
    }
}
