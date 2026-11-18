using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityGameJam.UI
{
    // TMP 입력 필드의 IME 요청을 새 Input System의 키보드에 전달한다.
    public sealed class NicknameImeInput : BaseInput
    {
        private Keyboard _keyboard;
        private string _composition = string.Empty;
        private IMECompositionMode _compositionMode;
        private Vector2 _cursorPosition;

        public override string compositionString => _composition;

        public override IMECompositionMode imeCompositionMode
        {
            get => _compositionMode;
            set
            {
                _compositionMode = value;
                StopComposition();
                if (value != IMECompositionMode.On || Keyboard.current == null) { return; }

                _keyboard = Keyboard.current;
                _keyboard.onIMECompositionChange += UpdateComposition;
                _keyboard.SetIMECursorPosition(_cursorPosition);
                _keyboard.SetIMEEnabled(true);
            }
        }

        public override Vector2 compositionCursorPos
        {
            get => _cursorPosition;
            set
            {
                _cursorPosition = value;
                _keyboard?.SetIMECursorPosition(value);
            }
        }

        protected override void OnDisable()
        {
            _compositionMode = IMECompositionMode.Auto;
            StopComposition();
            base.OnDisable();
        }

        private void UpdateComposition(IMECompositionString composition)
        {
            _composition = composition.ToString();
        }

        private void StopComposition()
        {
            if (_keyboard != null)
            {
                _keyboard.onIMECompositionChange -= UpdateComposition;
                _keyboard.SetIMEEnabled(false);
                _keyboard = null;
            }

            _composition = string.Empty;
        }
    }
}
