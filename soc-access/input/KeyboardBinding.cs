using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    public sealed class KeyboardBinding : InputBinding
    {
        public KeyboardBinding(Key key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public Key Key { get; private set; }

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
