using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    public sealed class KeyboardBinding : InputBinding
    {
        public KeyboardBinding(Key key, bool ctrl = false, bool shift = false, bool alt = false, string displayName = null)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
            DisplayName = displayName ?? string.Empty;
        }

        public Key Key { get; private set; }

        /// <summary>The character the key printed on the keyboard it was captured on, or empty for
        /// a compiled-in default. Not part of the match or the id: it is what the override's
        /// display-name twin is made from (<c>ModSettings.ApplyKeybindOverride</c>), so a chord
        /// captured on a key one keyboard calls OEM1 and prints as a backslash also fires where
        /// the backslash is the Backslash key.</summary>
        public string DisplayName { get; private set; }

        public bool Ctrl { get; private set; }

        public bool Shift { get; private set; }

        public bool Alt { get; private set; }

        public override string Id
        {
            get
            {
                return "keyboard:"
                    + Key
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
            UnityEngine.InputSystem.Controls.KeyControl keyControl,
            AccessibilityInputRouter.KeyboardStateSnapshot state,
            out Key pressedKey)
        {
            pressedKey = Key;
            return keyControl != null
                && Key == keyControl.keyCode
                && state != null
                && state.Ctrl == Ctrl
                && state.Shift == Shift
                && state.Alt == Alt;
        }
    }
}
