using System;
using System.Collections.Generic;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// THE MOD &lt;-&gt; GAME KEYBINDING BOUNDARY, WARNED BOTH WAYS.
    ///
    /// A mod gesture and a game hotkey on the same chord do not both fire: on a screen the mod owns,
    /// the mod's gesture wins and the game's does not. That is allowed - never refused - but it is
    /// worth a queued word so the player is not surprised. Two directions, one wording (adopted from
    /// Endless Space 2): "While the mod's {0} is active, the game's {1} will not fire."
    ///
    /// Direction (b), a mod rebind landing on a game key, is checked at capture apply
    /// (<see cref="WarnIfModShadowsGame"/>) against the game's own overrideable actions. Direction
    /// (a), a game rebind landing on a mod key, rides <see cref="IInputManager.OnBindingChanged"/>,
    /// subscribed at mod init and dropped in a Stop step. Only the mod &lt;-&gt; game boundary; no
    /// mod-vs-mod, no game-vs-game.
    ///
    /// Matching is by a canonical (ctrl, shift, alt, key-token) tuple. A mod
    /// <see cref="KeyboardBinding"/> is normalised straight from its fields; the game's binding is
    /// parsed out of the display strings <c>InputBinding.ToDisplayString</c> produces - single keys
    /// ("Tab"), composites joined by " + " with full-word modifiers ("Control + Tab", "Shift +
    /// Control + Z"), and separate bindings joined by " | " ("1 | Numpad 1") - mirroring the
    /// case-insensitive shift/control/alt split the game's own OptionsMenuKeyBindContent does.
    /// </summary>
    public static class ModKeybindConflicts
    {
        private static IInputManager _subscribed;
        private static Action<ActionReference> _handler;

        /// <summary>Hook the game's binding-changed callback so a game rebind onto a mod key warns.
        /// Best-effort: on a cold boot before the input manager exists there is nothing to hook, and
        /// direction (a) is silently unavailable until the next mod load. Idempotent.</summary>
        public static void Start()
        {
            Stop();
            IInputManager manager = InputManagerStaticAccessUnsafe.Current;
            if (manager == null)
            {
                return;
            }

            _subscribed = manager;
            _handler = OnGameBindingChanged;
            manager.OnBindingChanged += _handler;
        }

        /// <summary>Drop the subscription from the exact manager it was placed on. A Stop step.</summary>
        public static void Stop()
        {
            if (_subscribed != null && _handler != null)
            {
                _subscribed.OnBindingChanged -= _handler;
            }

            _subscribed = null;
            _handler = null;
        }

        /// <summary>Direction (b): the chord a mod capture just applied, checked against the game's
        /// overrideable actions. Returns the localized warning to speak, or null when nothing of the
        /// game's is on that chord.</summary>
        public static string WarnIfModShadowsGame(InputAction modAction, InputBinding modBinding)
        {
            if (modAction == null)
            {
                return null;
            }

            Chord modChord = ChordOf(modBinding);
            if (modChord == null)
            {
                return null;
            }

            IInputManager manager = InputManagerStaticAccessUnsafe.Current;
            if (manager == null)
            {
                return null;
            }

            List<BindingContainer> all;
            try
            {
                all = manager.GetAllOverrideableActions();
            }
            catch (Exception)
            {
                return null;
            }

            if (all == null)
            {
                return null;
            }

            for (int i = 0; i < all.Count; i++)
            {
                BindingContainer container = all[i];
                if (container == null || container.action == null)
                {
                    continue;
                }

                string display = string.IsNullOrEmpty(container.currentOverride)
                    ? container.defaultBinding
                    : container.currentOverride;
                if (Matches(modChord, display))
                {
                    return Warning(modAction, container.action);
                }
            }

            return null;
        }

        private static void OnGameBindingChanged(ActionReference action)
        {
            if (action == null)
            {
                return;
            }

            IInputManager manager = _subscribed ?? InputManagerStaticAccessUnsafe.Current;
            if (manager == null)
            {
                return;
            }

            BindingContainer container;
            if (!manager.TryGetOverride(action, out container) || container == null)
            {
                return;
            }

            // Only a genuine override warns: clearing one back to the default fires the same callback,
            // and the game's defaults are not the mod's business.
            string display = container.currentOverride;
            if (string.IsNullOrWhiteSpace(display))
            {
                return;
            }

            foreach (InputAction modAction in ModGestureCatalog.RebindableActions())
            {
                IReadOnlyList<InputBinding> bindings = modAction.Bindings;
                if (bindings == null)
                {
                    continue;
                }

                for (int i = 0; i < bindings.Count; i++)
                {
                    Chord modChord = ChordOf(bindings[i]);
                    if (modChord != null && Matches(modChord, display))
                    {
                        SpeechPipeline.Output(new SpeechRequest(Warning(modAction, action), interrupt: false));
                        return;
                    }
                }
            }
        }

        private static string Warning(InputAction modAction, ActionReference gameAction)
        {
            string gameName = GameText.Get("Hotkeys/" + gameAction.Identifier, gameAction.Identifier);
            return ModText.Get(ModStrings.Screens.KeybindShadowed, modAction.Label, gameName);
        }

        /// <summary>Whether the mod chord equals any of the alternatives the game display string
        /// describes.</summary>
        public static bool Matches(Chord modChord, string gameDisplay)
        {
            if (modChord == null)
            {
                return false;
            }

            List<Chord> gameChords = ParseGameDisplay(gameDisplay);
            for (int i = 0; i < gameChords.Count; i++)
            {
                if (modChord.Equals(gameChords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The canonical tuple of a mod binding, or null for a binding with no key.</summary>
        public static Chord ChordOf(InputBinding binding)
        {
            KeyboardBinding chord = binding as KeyboardBinding;
            if (chord != null)
            {
                return new Chord(chord.Ctrl, chord.Shift, chord.Alt, KeyToken(chord.Key));
            }

            KeyboardDisplayNameBinding literal = binding as KeyboardDisplayNameBinding;
            if (literal != null && !string.IsNullOrEmpty(literal.DisplayName))
            {
                return new Chord(literal.Ctrl, literal.Shift, literal.Alt, literal.DisplayName.Trim().ToLowerInvariant());
            }

            return null;
        }

        /// <summary>Every alternative a game display string names, canonicalised. " | " separates
        /// distinct bindings, " + " a composite's parts.</summary>
        public static List<Chord> ParseGameDisplay(string display)
        {
            List<Chord> result = new List<Chord>();
            if (string.IsNullOrWhiteSpace(display))
            {
                return result;
            }

            string[] alternatives = display.Split(new[] { " | " }, StringSplitOptions.None);
            for (int a = 0; a < alternatives.Length; a++)
            {
                Chord chord = ParseAlternative(alternatives[a]);
                if (chord != null)
                {
                    result.Add(chord);
                }
            }

            return result;
        }

        private static Chord ParseAlternative(string alternative)
        {
            if (string.IsNullOrWhiteSpace(alternative))
            {
                return null;
            }

            string[] parts = alternative.Split(new[] { " + " }, StringSplitOptions.None);
            if (parts.Length == 1)
            {
                // A lone part is the key itself, even when it is a modifier key pressed alone
                // ("Left Alt"): there is no OTHER key for it to modify.
                return new Chord(false, false, false, GameToken(parts[0]));
            }

            bool ctrl = false;
            bool shift = false;
            bool alt = false;
            string main = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i] == null ? string.Empty : parts[i].Trim();
                string lower = part.ToLowerInvariant();
                if (lower.Contains("control") || lower.Contains("ctrl"))
                {
                    ctrl = true;
                }
                else if (lower.Contains("shift"))
                {
                    shift = true;
                }
                else if (lower.Contains("alt"))
                {
                    alt = true;
                }
                else
                {
                    main = part;
                }
            }

            return new Chord(ctrl, shift, alt, GameToken(main));
        }

        /// <summary>A game display part reduced to a comparable token: lower-cased and stripped of the
        /// "Numpad " prefix so its digit meets the mod's plain-digit binding.</summary>
        private static string GameToken(string part)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return string.Empty;
            }

            string token = part.Trim().ToLowerInvariant();
            if (token.StartsWith("numpad "))
            {
                token = token.Substring("numpad ".Length).Trim();
            }

            return token;
        }

        /// <summary>The mod's <see cref="UnityEngine.InputSystem.Key"/> reduced to the same token the
        /// game's display parts reduce to.</summary>
        private static string KeyToken(UnityEngine.InputSystem.Key key)
        {
            string name = key.ToString();
            if (name.Length == 1)
            {
                return name.ToLowerInvariant();
            }

            if (name.StartsWith("Digit"))
            {
                return name.Substring("Digit".Length);
            }

            if (name.StartsWith("Numpad"))
            {
                return name.Substring("Numpad".Length).ToLowerInvariant();
            }

            return name.ToLowerInvariant();
        }

        /// <summary>A binding reduced to what a conflict compares: the modifiers and one key token.
        /// </summary>
        public sealed class Chord
        {
            public Chord(bool ctrl, bool shift, bool alt, string keyToken)
            {
                Ctrl = ctrl;
                Shift = shift;
                Alt = alt;
                KeyToken = keyToken ?? string.Empty;
            }

            public bool Ctrl { get; private set; }

            public bool Shift { get; private set; }

            public bool Alt { get; private set; }

            public string KeyToken { get; private set; }

            public bool Equals(Chord other)
            {
                return other != null
                    && Ctrl == other.Ctrl
                    && Shift == other.Shift
                    && Alt == other.Alt
                    && !string.IsNullOrEmpty(KeyToken)
                    && string.Equals(KeyToken, other.KeyToken, StringComparison.Ordinal);
            }
        }
    }
}
