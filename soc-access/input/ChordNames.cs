using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// A binding written out the way the player would SAY it - "Ctrl+Enter" - in the player's own
    /// language.
    ///
    /// The other half of <see cref="InputBinding.Id"/>, which is English-only and exists for the dev
    /// server and the logs. This one is spoken, so every part of it is a translated word: the
    /// modifiers and the joiner come out of <see cref="ModStrings.Keys"/> (a language that spells a
    /// chord "Strg und Eingabe" changes three strings, not this file), and so are the mod's own names
    /// for the keys it binds gestures to. Everything else - letters, digits, function keys - is named
    /// by Unity's own display name for the key, so a key nobody has named still reads out and this
    /// file's table only grows when a new gesture lands on an unnamed key.
    ///
    /// It is addressed by (action key, binding index) rather than by a chord, because that is what a
    /// <see cref="UI.Graph.NodeHint"/> declares: the sentence names the ACTION, and re-binding the
    /// action re-words the sentence. The index is load-bearing - Ctrl+left click is the THIRD binding
    /// of the same action as the ordinary left click, because the game runs one handler for both
    /// clicks and reads the physical Ctrl inside it.
    /// </summary>
    public static class ChordNames
    {
        /// <summary>
        /// How a key with no name of the mod's own is named: Unity's <c>KeyControl.displayName</c>
        /// for it, which is the character the player's own layout prints. Injected rather than called
        /// directly so the composition below - the modifiers, the joiner, the mod's key names - is
        /// testable with no keyboard device and no Unity input runtime present.
        /// </summary>
        public static Func<Key, string> KeyDisplayName = UnityDisplayName;

        /// <summary>The chord at <paramref name="bindingIndex"/> of the action called
        /// <paramref name="actionKey"/>, or null where the action or that binding does not
        /// exist.</summary>
        public static string Of(string actionKey, int bindingIndex)
        {
            InputAction action = AccessibilityActions.FindByKey(actionKey);
            IReadOnlyList<InputBinding> bindings = action == null ? null : action.Bindings;
            if (bindings == null || bindingIndex < 0 || bindingIndex >= bindings.Count)
            {
                return null;
            }

            return Of(bindings[bindingIndex]);
        }

        /// <summary>The same for a binding already in hand.</summary>
        public static string Of(InputBinding binding)
        {
            KeyboardBinding chord = binding as KeyboardBinding;
            if (chord != null)
            {
                return Compose(chord.Ctrl, chord.Shift, chord.Alt, KeyName(chord.Key));
            }

            // A display-name binding IS its display character - the keyboard that prints a backslash
            // on a key Unity calls OEM1 - so that character is what the player would say.
            KeyboardDisplayNameBinding literal = binding as KeyboardDisplayNameBinding;
            return literal == null
                ? null
                : Compose(literal.Ctrl, literal.Shift, literal.Alt, literal.DisplayName);
        }

        private static string Compose(bool ctrl, bool shift, bool alt, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            StringBuilder name = new StringBuilder();
            if (ctrl)
            {
                Append(name, ModText.Get(ModStrings.Keys.Ctrl));
            }

            if (shift)
            {
                Append(name, ModText.Get(ModStrings.Keys.Shift));
            }

            if (alt)
            {
                Append(name, ModText.Get(ModStrings.Keys.Alt));
            }

            Append(name, key);
            return name.ToString();
        }

        // The mod's own words for the keys it binds GESTURES to - the ones a usage hint or the carry's
        // announcement can name. Everything else answers with Unity's display name for the key, which
        // is a readable character for the letters, the digits and the punctuation.
        private static readonly Dictionary<Key, ModString> Named = new Dictionary<Key, ModString>
        {
            { Key.Enter, ModStrings.Keys.Enter },
            { Key.NumpadEnter, ModStrings.Keys.NumpadEnter },
            { Key.Space, ModStrings.Keys.Space },
            { Key.Escape, ModStrings.Keys.Escape },
            { Key.Backslash, ModStrings.Keys.Backslash },
            { Key.Tab, ModStrings.Keys.Tab },
            { Key.UpArrow, ModStrings.Keys.UpArrow },
            { Key.DownArrow, ModStrings.Keys.DownArrow },
            { Key.LeftArrow, ModStrings.Keys.LeftArrow },
            { Key.RightArrow, ModStrings.Keys.RightArrow },
            { Key.Home, ModStrings.Keys.Home },
            { Key.End, ModStrings.Keys.End },
            { Key.Backspace, ModStrings.Keys.Backspace },
            { Key.Backquote, ModStrings.Keys.Backquote }
        };

        private static string KeyName(Key key)
        {
            ModString named;
            if (Named.TryGetValue(key, out named))
            {
                return ModText.Get(named);
            }

            Func<Key, string> display = KeyDisplayName;
            string shown = null;
            if (display != null)
            {
                try
                {
                    shown = display(key);
                }
                catch (Exception)
                {
                    shown = null;
                }
            }

            // A miss answers with the key itself rather than with silence: a chord nobody can name is
            // still a chord the player pressed.
            return string.IsNullOrEmpty(shown) ? key.ToString() : shown;
        }

        private static string UnityDisplayName(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? null : keyboard[key].displayName;
        }

        private static void Append(StringBuilder name, string part)
        {
            if (name.Length == 0)
            {
                name.Append(part);
                return;
            }

            string joined = ModText.Get(ModStrings.Keys.ChordJoiner, name.ToString(), part);
            name.Length = 0;
            name.Append(joined);
        }
    }
}
