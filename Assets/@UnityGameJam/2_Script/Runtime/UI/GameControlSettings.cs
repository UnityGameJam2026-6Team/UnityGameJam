using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityGameJam.UI
{
    public static class GameControlSettings
    {
        private const string PreferenceKey = "UnityGameJam.SpaceAndMouseControls";
        private static bool _isLoaded;
        private static bool _useSpaceAndMouse;

        public static bool UseSpaceAndMouse
        {
            get
            {
                if (!_isLoaded)
                {
                    _useSpaceAndMouse = PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
                    _isLoaded = true;
                }
                return _useSpaceAndMouse;
            }
        }

        public static void ApplySaved() => ApplyBindings();

        public static void Toggle()
        {
            _useSpaceAndMouse = !UseSpaceAndMouse;
            _isLoaded = true;
            PlayerPrefs.SetInt(PreferenceKey, _useSpaceAndMouse ? 1 : 0);
            PlayerPrefs.Save();
            ApplyBindings();
        }

        private static void ApplyBindings()
        {
            InputActionAsset actions = InputSystem.actions;
            if (actions == null) { return; }
            OverrideKeyboard(actions.FindAction("Player/Interact"), "<Keyboard>/space");
            OverrideKeyboard(actions.FindAction("UI/Submit"), "<Keyboard>/space");
            OverrideKeyboard(actions.FindAction("Player/Approve"), "");
            OverrideKeyboard(actions.FindAction("Player/Hold"), "");
        }

        private static void OverrideKeyboard(InputAction action, string spaceModePath)
        {
            if (action == null) { return; }
            bool hasMapped = false;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite || !binding.path.StartsWith("<Keyboard>/")) { continue; }
                if (UseSpaceAndMouse)
                {
                    action.ApplyBindingOverride(i, hasMapped ? "" : spaceModePath);
                    hasMapped = true;
                }
                else { action.RemoveBindingOverride(i); }
            }
        }
    }
}
