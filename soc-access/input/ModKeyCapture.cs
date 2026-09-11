using System;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// THE MOD'S OWN KEY-BINDING CAPTURE - the opposite of <see cref="KeyCaptureFocus"/>.
    ///
    /// <see cref="KeyCaptureFocus"/> is a stand-down: while the GAME listens for a rebind key the mod
    /// answers nothing. This is the reverse - while the mod is armed the router GRABS the next key
    /// for the mod, because a mod gesture is not a game input action and no game field is up to catch
    /// it. Matching the game, there is no cancel and no timeout: the first non-modifier key becomes
    /// the binding, Escape included; the undo is a clear back to the default.
    ///
    /// A single armed action, held only while a capture is in flight. The mod options screen disarms
    /// it when it closes, and a hot reload throws the whole static away with the assembly, so there
    /// is no long-lived state to reset.
    /// </summary>
    public static class ModKeyCapture
    {
        private static InputAction _action;
        private static Action _applied;

        public static bool IsArmed
        {
            get { return _action != null; }
        }

        public static InputAction ArmedAction
        {
            get { return _action; }
        }

        /// <summary>Arm the capture for one action and speak the prompt QUEUED, so it follows rather
        /// than cuts off whatever the "+" activation itself said. <paramref name="applied"/> runs
        /// once the new chord is in force - the drawn row redrawing its chip.</summary>
        public static void Rebind(InputAction action, Action applied = null)
        {
            if (action == null)
            {
                return;
            }

            _action = action;
            _applied = applied;
            SpeechPipeline.Output(new SpeechRequest(
                ModText.Get(ModStrings.Screens.KeybindPressKey, action.Label),
                interrupt: false));
        }

        /// <summary>Drop a capture in flight with nothing bound - the disarm the screen runs when it
        /// closes over an armed capture. Not a player-facing cancel; the player has no cancel.</summary>
        public static void Cancel()
        {
            _action = null;
            _applied = null;
        }

        /// <summary>The router hands over the first non-modifier key while armed. Apply and persist the
        /// override, disarm, run the mod-&gt;game conflict check, and announce - all queued.</summary>
        public static void Complete(KeyboardBinding binding)
        {
            InputAction action = _action;
            Action applied = _applied;
            _action = null;
            _applied = null;
            if (action == null || binding == null)
            {
                return;
            }

            ModSettings.SetKeybindOverride(action.Key, new InputBinding[] { binding });
            if (applied != null)
            {
                applied();
            }

            string chord = ChordNames.Of(binding);
            SpeechPipeline.Output(new SpeechRequest(
                ModText.Get(ModStrings.Screens.KeybindSet, action.Label, chord ?? string.Empty),
                interrupt: false));

            string conflict = ModKeybindConflicts.WarnIfModShadowsGame(action, binding);
            if (!string.IsNullOrWhiteSpace(conflict))
            {
                SpeechPipeline.Output(new SpeechRequest(conflict, interrupt: false));
            }
        }
    }
}
