using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// The keys a keyboard has twice. The game's own bindings read "1 or Numpad 1", and a mod
    /// gesture on Enter or a digit is meant the same way: whichever of the pair the player has under
    /// their hand. A binding therefore matches on the CANONICAL key - the main-block one - of both
    /// the key it names and the key that went down, so a default written as Enter, or a chord
    /// captured on the numpad, answers either.
    /// </summary>
    public static class KeyAliases
    {
        public static Key Canonical(Key key)
        {
            switch (key)
            {
                case Key.NumpadEnter:
                    return Key.Enter;
                case Key.Numpad0:
                    return Key.Digit0;
                case Key.Numpad1:
                    return Key.Digit1;
                case Key.Numpad2:
                    return Key.Digit2;
                case Key.Numpad3:
                    return Key.Digit3;
                case Key.Numpad4:
                    return Key.Digit4;
                case Key.Numpad5:
                    return Key.Digit5;
                case Key.Numpad6:
                    return Key.Digit6;
                case Key.Numpad7:
                    return Key.Digit7;
                case Key.Numpad8:
                    return Key.Digit8;
                case Key.Numpad9:
                    return Key.Digit9;
                default:
                    return key;
            }
        }
    }
}
