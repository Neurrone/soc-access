using SongsOfConquest.Client.InputManagement;
using SongsOfConquestAccess.Screens;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SongsOfConquestAccess.Input
{
    internal sealed class AccessibilityInputRouter
    {
        private readonly ScreenManager _screenManager;

        public AccessibilityInputRouter(ScreenManager screenManager)
        {
            _screenManager = screenManager;
        }

        /// <summary>
        /// Entry point for keyboard callbacks that arrive through the game's central
        /// UnityInputManager hook.
        ///
        /// Most keyboard actions we care about use this path, but some UI navigation
        /// keys such as arrow keys do not surface there and must be
        /// intercepted lower down in the Unity UI input module.
        /// </summary>
        public bool TryHandleKeyboardInput(InputSource source, InputPhase phase, InputControl control)
        {
            KeyControl keyControl = null;
            if (source == InputSource.Keyboard && phase == InputPhase.Down)
            {
                keyControl = control as KeyControl;
            }

            Key? key = keyControl != null ? (Key?)keyControl.keyCode : null;
            return TryHandleKeyboardKey(key, "AccessibilityInputRouter detected");
        }

        /// <summary>
        /// Entry point for raw keyboard interception below the game's normal action
        /// callback layer.
        ///
        /// This exists because keys like arrows are processed by the
        /// Unity UI input module and do not arrive through TryHandleKeyboardInput.
        /// The mapping and dispatch logic is shared so both interception layers stay
        /// behaviorally identical.
        /// </summary>
        public bool TryHandleRawKeyboardKey(Key key)
        {
            return TryHandleKeyboardKey(key, "AccessibilityInputRouter intercepted raw key");
        }

        private bool TryHandleKeyboardKey(Key? key, string logPrefix)
        {
            if (!key.HasValue)
            {
                return false;
            }

            InputAction action = TryGetAccessibilityActionForKey(key.Value);
            if (action == null)
            {
                return false;
            }

            SoqAccessPlugin.Instance?.LogInfo(logPrefix + " key " + key.Value + " as action " + action.Key);
            return _screenManager != null && _screenManager.DispatchAction(action);
        }

        private static InputAction TryGetAccessibilityActionForKey(Key keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            switch (keyCode)
            {
                case Key.Tab:
                    bool reverse = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                    return reverse ? AccessibilityActions.PreviousWidget : AccessibilityActions.NextWidget;
                case Key.UpArrow:
                    return AccessibilityActions.PreviousMenuItem;
                case Key.DownArrow:
                    return AccessibilityActions.NextMenuItem;
                case Key.Home:
                    return AccessibilityActions.FirstMenuItem;
                case Key.End:
                    return AccessibilityActions.LastMenuItem;
                case Key.Enter:
                case Key.NumpadEnter:
                    return AccessibilityActions.Activate;
                case Key.Escape:
                    return AccessibilityActions.Cancel;
                default:
                    return null;
            }
        }
    }
}
