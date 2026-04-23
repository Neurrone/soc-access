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
        /// Convert raw keyboard input from the game's central input hook into one
        /// semantic accessibility action, then dispatch it through the screen stack.
        /// </summary>
        public bool TryHandleKeyboardInput(InputSource source, InputPhase phase, InputControl control)
        {
            InputAction action = TryGetKeyboardAccessibilityAction(source, phase, control);
            if (action == null)
            {
                return false;
            }

            SoqAccessPlugin.Instance?.LogInfo("AccessibilityInputRouter detected action " + action.Key);
            return _screenManager != null && _screenManager.DispatchAction(action);
        }

        private static InputAction TryGetKeyboardAccessibilityAction(InputSource source, InputPhase phase, InputControl control)
        {
            if (source != InputSource.Keyboard || phase != InputPhase.Down)
            {
                return null;
            }

            KeyControl keyControl = control as KeyControl;
            if (keyControl == null)
            {
                return null;
            }

            Keyboard keyboard = Keyboard.current;
            switch (keyControl.keyCode)
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
