using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess
{
    public static partial class ModSettings
    {
        // ---- mod-gesture key overrides ----

        /// <summary>The player's override bindings for a mod gesture, or null when it is on its
        /// compiled-in default. Decoded once per action and kept, as these are player settings nothing
        /// outside this class changes between reads.</summary>
        public static IReadOnlyList<KeyboardBinding> GetKeybindOverride(string actionKey)
        {
            KeybindConfig config = GetKeybindConfig(actionKey);
            if (config == null || config.Entry == null || string.IsNullOrEmpty(config.Entry.Value))
            {
                return null;
            }

            if (config.Decoded == null)
            {
                config.Decoded = KeybindCodec.Decode(config.Entry.Value);
            }

            return config.Decoded;
        }

        public static bool HasKeybindOverride(string actionKey)
        {
            KeybindConfig config = GetKeybindConfig(actionKey);
            return config != null && config.Entry != null && !string.IsNullOrEmpty(config.Entry.Value);
        }

        /// <summary>Set a mod gesture's override to the given chords, persist it, and apply it to the
        /// live action. An empty or null set clears the override instead.</summary>
        public static void SetKeybindOverride(string actionKey, IReadOnlyList<InputBinding> bindings)
        {
            KeybindConfig config = GetKeybindConfig(actionKey);
            if (config == null || config.Entry == null)
            {
                return;
            }

            string encoded = KeybindCodec.Encode(bindings);
            if (string.IsNullOrEmpty(encoded))
            {
                ClearKeybindOverride(actionKey);
                return;
            }

            config.Entry.Value = encoded;
            config.Decoded = KeybindCodec.Decode(encoded);
            _config?.Save();
            ApplyKeybindOverride(actionKey, config.Decoded);
        }

        /// <summary>Drop a mod gesture's override, persist the empty entry, and return the live action
        /// to its default.</summary>
        public static void ClearKeybindOverride(string actionKey)
        {
            KeybindConfig config = GetKeybindConfig(actionKey);
            if (config == null || config.Entry == null)
            {
                return;
            }

            config.Entry.Value = string.Empty;
            config.Decoded = null;
            _config?.Save();

            InputAction action = AccessibilityActions.FindByKey(actionKey);
            if (action != null)
            {
                action.ResetToDefault();
            }
        }

        /// <summary>Clear every mod gesture's override - the Keybinds tab's reset-all.</summary>
        public static void ClearAllKeybindOverrides()
        {
            foreach (InputAction action in ModGestureCatalog.RebindableActions())
            {
                ClearKeybindOverride(action.Key);
            }
        }

        /// <summary>Bind one string entry per rebindable gesture, keyed by the action key, and
        /// re-apply any persisted override to the live action. Empty means default; non-empty means an
        /// override only. Statics re-init on every hot reload, so this is where a saved override is put
        /// back onto the freshly re-constructed action.</summary>
        private static void BindKeybinds(ConfigFile config)
        {
            _keybinds.Clear();
            foreach (InputAction action in ModGestureCatalog.RebindableActions())
            {
                KeybindConfig entry = new KeybindConfig
                {
                    Entry = config.Bind(
                        KeybindsSection,
                        action.Key,
                        string.Empty,
                        "Override binding for this mod gesture. Empty uses the default. Edited through the mod settings screen.")
                };
                _keybinds[action.Key] = entry;

                if (!string.IsNullOrEmpty(entry.Entry.Value))
                {
                    entry.Decoded = KeybindCodec.Decode(entry.Entry.Value);
                    ApplyKeybindOverride(action.Key, entry.Decoded);
                }
            }
        }

        /// <summary>Apply the given chords to the live action as its override, keeping any non-keyboard
        /// default binding (the OEM1 backslash display-name fallback of the map's secondary action) so
        /// a rebind does not silently drop it.</summary>
        private static void ApplyKeybindOverride(string actionKey, List<KeyboardBinding> chords)
        {
            InputAction action = AccessibilityActions.FindByKey(actionKey);
            if (action == null)
            {
                return;
            }

            if (chords == null || chords.Count == 0)
            {
                action.ResetToDefault();
                return;
            }

            // The override replaces every default, the display-name fallback a default may carry
            // included (map_secondary_action's backslash), so a rebound gesture no longer answers
            // the key it was moved off. What a captured chord carries instead is the character
            // its key printed, and a punctuation character gets a twin that matches by that
            // character: a backslash captured as OEM1 on one keyboard fires on the Backslash key
            // of another, and the other way round. Letters and digits get no twin, since on a
            // keyboard whose layout moves them the key the player pressed is the one to keep.
            List<InputBinding> effective = new List<InputBinding>();
            for (int i = 0; i < chords.Count; i++)
            {
                KeyboardBinding chord = chords[i];
                effective.Add(chord);
                if (IsPortableDisplayName(chord.DisplayName))
                {
                    effective.Add(new KeyboardDisplayNameBinding(chord.DisplayName, chord.Ctrl, chord.Shift, chord.Alt));
                }
            }

            action.SetOverride(effective);
        }

        /// <summary>Whether a captured key's printed character is worth matching by: one character
        /// that is neither a letter, a digit nor whitespace - the punctuation keyboards move
        /// between key codes.</summary>
        public static bool IsPortableDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName.Length != 1)
            {
                return false;
            }

            char c = displayName[0];
            return !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);
        }

        private static KeybindConfig GetKeybindConfig(string actionKey)
        {
            return Lookup(_keybinds, actionKey);
        }

        private sealed class KeybindConfig
        {
            public ConfigEntry<string> Entry { get; set; }
            public List<KeyboardBinding> Decoded { get; set; }
        }
    }
}
