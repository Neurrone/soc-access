using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SongsOfConquestAccess.Input
{
    internal sealed class KeyboardDisplayNameBinding : InputBinding
    {
        public KeyboardDisplayNameBinding(string displayName, bool ctrl = false, bool shift = false, bool alt = false)
        {
            DisplayName = displayName ?? string.Empty;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public string DisplayName { get; private set; }

        public bool Ctrl { get; private set; }

        public bool Shift { get; private set; }

        public bool Alt { get; private set; }

        public override string Id
        {
            get
            {
                return "keyboard-display:"
                    + DisplayName
                    + ":ctrl="
                    + Ctrl
                    + ":shift="
                    + Shift
                    + ":alt="
                    + Alt;
            }
        }

        public override bool IsModified
        {
            get { return Ctrl || Shift || Alt; }
        }

        public override bool MatchesKeyDown(
            KeyControl keyControl,
            AccessibilityInputRouter.KeyboardStateSnapshot state,
            out Key pressedKey)
        {
            pressedKey = keyControl != null ? keyControl.keyCode : Key.None;
            return keyControl != null
                && keyControl.displayName == DisplayName
                && state != null
                && state.Ctrl == Ctrl
                && state.Shift == Shift
                && state.Alt == Alt;
        }
    }
}
