using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    internal sealed class KeyboardBinding : InputBinding
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

        public bool UsesKey(Key key)
        {
            return Key == key;
        }

        public bool MatchesKeyDown(Key key, AccessibilityInputRouter.KeyboardStateSnapshot state)
        {
            return Key == key
                && state != null
                && state.Ctrl == Ctrl
                && state.Shift == Shift
                && state.Alt == Alt;
        }
    }
}
