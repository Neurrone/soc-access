using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// Handing the keyboard to one of the game's own text fields, and taking it back again.
    ///
    /// The game gives a field a caret and nothing else: no word on the way in, none per character,
    /// none on the way out. All of that is the mod's - "editing" as the field takes the keyboard, the
    /// character typed or deleted and the one under the caret as it moves
    /// (<see cref="TextInputEchoHelper"/>), and "edited" with the new text or "Cancelled" on the way
    /// out. A screen owns one of these and drives it from its own per-frame update.
    ///
    /// THE DEFERRED HANDOVER. The field is not given the keyboard on the frame the player asked for
    /// it: Unity delivers key events to the focused control late in the frame, after the mod's
    /// update, so a field holding the keyboard while the activating Enter is still physically down is
    /// one dispatch away from acting on it - and a dialog's field answers Enter by submitting the
    /// dialog. The wait is therefore for the RELEASE of Enter, not merely for a later frame. An
    /// INJECTED activation presses no physical key, so the wait is invisible to <c>POST /input</c>
    /// and the handover lands on the very next frame; only a real key exercises it.
    ///
    /// THE WAY OUT is read off the field rather than hooked, for the same reason the echo is: Escape,
    /// Enter, a click elsewhere and the game closing the surface all end up in the same place, which
    /// is the field no longer being focused. What was typed decides which of the two words is said -
    /// TMP puts the pre-edit text back when its own Escape cancels the edit, so a text that did not
    /// change after an edit IS the cancel.
    ///
    /// Reload-safe by owning nothing: no events subscribed, no objects added to the scene, and the
    /// screen abandons a live edit when it loses focus or is popped.
    /// </summary>
    public sealed class GameTextEditor
    {
        // How long the field is given to report that it took the keyboard. TMP activates on its own
        // next update, so the answer normally arrives on the frame after the handover; a field that
        // never takes it is let go of in silence rather than leaving the screen waiting forever.
        private const int FramesToWaitForFocus = 60;

        private readonly TextInputEchoHelper _echo = new TextInputEchoHelper();

        /// <summary>The TMP field a mod editor currently holds, or null - what the stand-down asks
        /// beside the event system's selection, because a toolkit that drives its own selection (the
        /// mod.io browser) can leave the selection elsewhere while the field is focused.</summary>
        public static TMPro.TMP_InputField CurrentInput { get; private set; }

        /// <summary>Whether ANY editor is pending or editing. The arrival release in
        /// <c>GraphScreen.Update</c> asks this beside the screen's own answer, so a screen whose editor
        /// lives in a shared row builder (the mod dialogs) cannot forget to say it owns the field.</summary>
        public static bool Owned
        {
            get { return _owner != null; }
        }

        /// <summary>Whether an editor has handed a field the keyboard - the mod's OWN word on it,
        /// which the stand-down (<c>GameTextFocus.IsTyping</c>) asks before it asks the engine. TMP
        /// reports the field focused only from the LateUpdate after the handover, so for the rest of
        /// the handover frame the engine's answer is "no" while the keyboard is already the field's;
        /// a letter arriving in that frame was otherwise the mod's to search with (measured on the
        /// mod.io email box, 2026-09-10: Enter released and a letter pressed in the same frame).
        /// Bounded: an edit whose field never takes the keyboard is let go of after
        /// <see cref="FramesToWaitForFocus"/>.</summary>
        public static bool AnyEditing
        {
            get { return _owner != null && _owner._editing != null; }
        }

        private static GameTextEditor _owner;

        /// <summary>Forget which editor holds the keyboard. For the mod going away with an edit still
        /// open: the two statics above are the mod's own word on who owns the field, and they are the
        /// only state here that outlives the screen that made the editor.</summary>
        public static void Reset()
        {
            _owner = null;
            CurrentInput = null;
        }

        // Whether the end of the edit is spoken. The chat box keeps quiet: its Enter SENDS, the line
        // arriving in the history is the announcement, and the game empties the box, which would
        // otherwise read as a cancel.
        private bool _announceEnd = true;

        private Field _requested;
        private Field _editing;
        private string _snapshot;
        private bool _sawFocus;
        private int _framesWaited;

        /// <summary>An editor has been asked for and the keyboard has not changed hands yet. The
        /// owning screen answers <c>CapturesRawInput</c> with this: during the wait the mod's keys are
        /// still live, and what the player types next is meant for the field.</summary>
        public bool Pending
        {
            get { return _requested != null; }
        }

        /// <summary>A field the mod handed the keyboard to is live.</summary>
        public bool Editing
        {
            get { return _editing != null; }
        }

        /// <summary>Ask for the game's editor on <paramref name="field"/>. Nothing is said here: the
        /// word comes with the keyboard, a frame or more later.</summary>
        public void Request(IUITextMeshInputField field)
        {
            Request(field == null ? null : new Field(field));
        }

        /// <summary>The same, for a bare TMP field: the mod.io browser the community maps screens
        /// wrap draws its own text boxes rather than the game's, and the contract is the same.</summary>
        public void Request(TMPro.TMP_InputField field)
        {
            Request(field == null ? null : new Field(field));
        }

        /// <summary>As <see cref="Request(IUITextMeshInputField)"/>, saying nothing when the edit ends:
        /// for a box whose Enter is the game's own send.</summary>
        public void RequestSilentEnd(IUITextMeshInputField field)
        {
            Field f = field == null ? null : new Field(field);
            if (f != null && _requested == null && _editing == null)
            {
                _announceEnd = false;
                _requested = f;
                _owner = this;
            }
        }

        private void Request(Field field)
        {
            if (field == null || _requested != null || _editing != null)
            {
                return;
            }

            _requested = field;
            _owner = this;
        }

        /// <summary>
        /// The per-frame work, called from the owning screen's update.
        /// <paramref name="surfaceIsPresent"/> is that screen still being on the screen: when the
        /// player's Enter submitted the dialog the field and the surface go together, and an ending
        /// nobody is left to hear is not announced.
        /// </summary>
        public void Update(bool surfaceIsPresent)
        {
            if (!surfaceIsPresent)
            {
                Abandon();
                return;
            }

            if (_requested != null)
            {
                if (EnterIsDown())
                {
                    return;
                }

                Field field = _requested;
                _requested = null;
                Begin(field);
                return;
            }

            if (_editing == null)
            {
                return;
            }

            if (_editing.Focused)
            {
                _sawFocus = true;
                _echo.Update();
                return;
            }

            if (!_sawFocus && _framesWaited++ < FramesToWaitForFocus)
            {
                return;
            }

            Finish(_sawFocus);
        }

        /// <summary>Let go without a word: the mod is taking the keyboard back itself - the screen is
        /// closing or losing focus - and none of that is something the player did to the text.</summary>
        public void Abandon()
        {
            _requested = null;
            if (ReferenceEquals(_owner, this))
            {
                _owner = null;
            }

            Finish(false);
        }

        /// <summary>Whether the text the player leaves behind is a commit rather than a cancel. TMP
        /// restores what was in the box when its own Escape ends the edit, so an unchanged text after
        /// an edit is exactly the cancel.</summary>
        public static bool Committed(string before, string after)
        {
            return !string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal);
        }

        private void Begin(Field field)
        {
            if (field == null || !field.Interactable)
            {
                // Nothing was begun, so nothing is owned: left set, the ownership would keep the
                // type-ahead and the arrival release off until the screen was left.
                if (ReferenceEquals(_owner, this))
                {
                    _owner = null;
                }

                return;
            }

            // Read BEFORE the handover: the text a cancel puts back is the one that was there when
            // the player asked to edit.
            _snapshot = field.Text;
            _editing = field;
            CurrentInput = field.Input;
            _sawFocus = false;
            _framesWaited = 0;
            field.Activate();

            // Queued, so it follows whatever the activation itself said rather than cutting it off.
            Say(ModText.Get(ModStrings.UI.EditStarted));
            field.BeginEcho(_echo);
        }

        private void Finish(bool announce)
        {
            Field field = _editing;
            string before = _snapshot;
            _echo.Stop();
            field?.RestoreVirtualKeyboardCoadjutant();
            _editing = null;
            CurrentInput = null;
            if (ReferenceEquals(_owner, this) && _requested == null)
            {
                _owner = null;
            }

            bool announceEnd = _announceEnd;
            _announceEnd = true;
            _snapshot = null;
            _sawFocus = false;
            _framesWaited = 0;

            if (!announce || !announceEnd || field == null)
            {
                return;
            }

            string after = field.Text;
            if (!Committed(before, after))
            {
                Say(ModText.Get(ModStrings.UI.EditCancelled));
                return;
            }

            // The word, then what is in the box now - both queued, or the one the player is waiting
            // for is the one they never hear.
            Say(ModText.Get(ModStrings.UI.EditCommitted));
            Say(after);
        }

        // Asked of the operating system, not of Unity's input system: the mod claimed the Enter that
        // asked for this edit by marking its event handled, and a handled event never updates the
        // keyboard's state, so enterKey.isPressed reads false while the finger is still down. That is
        // exactly the frame a field must not be given the keyboard on.
        private static bool EnterIsDown()
        {
            Keyboard keyboard = Keyboard.current;
            return SongsOfConquestAccess.Input.OsKeys.EnterIsDown()
                || (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed));
        }

        private static void Say(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        /// <summary>
        /// The box being typed into, whichever toolkit drew it. Almost every field on the screen is
        /// one of the game's own (<see cref="IUITextMeshInputField"/>), but the mod.io browser behind
        /// the community maps pages draws bare TMP fields, and the editing contract - snapshot,
        /// handover, echo, the word on the way out - is the same for both. Underneath both are TMP,
        /// which is why one type can serve them.
        /// </summary>
        private sealed class Field
        {
            private readonly IUITextMeshInputField _game;
            private readonly TMPro.TMP_InputField _input;

            /// <summary>The TMP field underneath, whichever toolkit drew it.</summary>
            public TMPro.TMP_InputField Input
            {
                get { return _input; }
            }

            public Field(IUITextMeshInputField field)
            {
                _game = field;
                _input = NativeInputFieldOf(field);
            }

            public Field(TMPro.TMP_InputField field)
            {
                _input = field;
            }

            public bool Interactable
            {
                get
                {
                    try
                    {
                        return _game != null ? _game.Interactable : _input != null && _input.interactable;
                    }
                    catch (Exception error)
                    {
                        Report("asking whether the field is interactable", error);
                        return false;
                    }
                }
            }

            public string Text
            {
                get
                {
                    try
                    {
                        if (_game != null)
                        {
                            return _game.InputFieldValue ?? string.Empty;
                        }

                        return _input != null ? _input.text ?? string.Empty : string.Empty;
                    }
                    catch (Exception error)
                    {
                        Report("reading the field's text", error);
                        return string.Empty;
                    }
                }
            }

            /// <summary>
            /// Whether the field still has the keyboard, asked of TMP's own <c>isFocused</c> rather
            /// than of <see cref="IUITextMeshInputField.Focused"/>. The game's property also answers
            /// true for its gamepad "this field controls the UI input" latch, which is cleared by the
            /// field's DESELECT and not by deactivating it - so a field the game had merely
            /// deactivated still read as focused and the edit never ended (measured on the join-game
            /// popup, 2026-09-06). This is also the signal the stand-down reads, so the two halves
            /// agree about when the keyboard is the game's.
            /// </summary>
            public bool Focused
            {
                get
                {
                    try
                    {
                        if (_input != null)
                        {
                            return _input.isFocused;
                        }

                        return _game != null && _game.Focused;
                    }
                    catch (Exception error)
                    {
                        Report("asking whether the field is focused", error);
                        return false;
                    }
                }
            }

            public void Activate()
            {
                if (_game != null)
                {
                    _game.Select();
                    _game.ActivateInputField();
                    return;
                }

                if (_input != null)
                {
                    // Before selecting, not after: the coadjutant acts in OnSelect, so it must be
                    // disabled by the time Select() fires the event.
                    SuppressVirtualKeyboardCoadjutant();
                    _input.Select();
                    _input.ActivateInputField();
                }
            }

            // mod.io hangs an InputFieldCoadjutant on each of its text boxes. On a standalone build
            // (its UiSettings ships StandaloneUsesVKDelegate = true) that component's OnSelect starts
            // a coroutine that DeactivateInputField()s the box at the end of the frame, meaning to
            // hand the player a platform virtual keyboard instead - but this game never assigns
            // Browser.OpenVirtualKeyboard, so selecting the box only un-focuses it and the mod's echo
            // never sees a keystroke (diagnosed 2026-09-11: the box stayed selected with isFocused
            // false for every wait frame). Disabling that one component for the life of the edit lets
            // the box hold the keyboard and take typing directly; OnSelect never fires while it is
            // off, so no de-focusing coroutine starts. It is restored when the edit ends. The game's
            // own fields (_game) do not carry it, so this is the mod.io path only.
            private const string CoadjutantTypeName = "ModIOBrowser.Implementation.InputFieldCoadjutant";
            private UnityEngine.MonoBehaviour _suppressedCoadjutant;

            private void SuppressVirtualKeyboardCoadjutant()
            {
                try
                {
                    if (_input == null)
                    {
                        return;
                    }

                    UnityEngine.MonoBehaviour[] behaviours = _input.GetComponents<UnityEngine.MonoBehaviour>();
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        UnityEngine.MonoBehaviour behaviour = behaviours[i];
                        if (behaviour != null && behaviour.enabled && behaviour.GetType().FullName == CoadjutantTypeName)
                        {
                            behaviour.enabled = false;
                            _suppressedCoadjutant = behaviour;
                            return;
                        }
                    }
                }
                catch (Exception error)
                {
                    Report("switching the virtual keyboard's helper off", error);
                }
            }

            public void RestoreVirtualKeyboardCoadjutant()
            {
                try
                {
                    if (_suppressedCoadjutant != null)
                    {
                        _suppressedCoadjutant.enabled = true;
                        _suppressedCoadjutant = null;
                    }
                }
                catch (Exception error)
                {
                    Report("switching the virtual keyboard's helper back on", error);
                }
            }

            public void BeginEcho(TextInputEchoHelper echo)
            {
                if (echo == null)
                {
                    return;
                }

                if (_game != null)
                {
                    echo.Begin(_game);
                    return;
                }

                echo.Begin(_input);
            }

            // A field the game has destroyed throws rather than answering, and these run on every
            // build and every keystroke: each site says what threw the first time and then keeps
            // quiet. The table holds only the site names, which are fixed.
            private static readonly HashSet<string> Reported = new HashSet<string>();

            private static void Report(string what, Exception error)
            {
                if (!Reported.Add(what))
                {
                    return;
                }

                SocAccessMod.Instance?.LogWarning("Text editor: " + what + " threw: " + error);
            }

            private static TMPro.TMP_InputField NativeInputFieldOf(IUITextMeshInputField field)
            {
                try
                {
                    UITextMeshInputField concrete = field as UITextMeshInputField;
                    return concrete != null ? concrete.GetInputField() : null;
                }
                catch (Exception error)
                {
                    Report("reading the field's native input field", error);
                    return null;
                }
            }
        }
    }
}
