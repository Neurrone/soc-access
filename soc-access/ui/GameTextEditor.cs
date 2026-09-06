using System;
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

        private IUITextMeshInputField _requested;
        private IUITextMeshInputField _editing;
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
            if (field == null || _requested != null || _editing != null)
            {
                return;
            }

            _requested = field;
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

                IUITextMeshInputField field = _requested;
                _requested = null;
                Begin(field);
                return;
            }

            if (_editing == null)
            {
                return;
            }

            if (IsFocused(_editing))
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
            Finish(false);
        }

        /// <summary>Whether the text the player leaves behind is a commit rather than a cancel. TMP
        /// restores what was in the box when its own Escape ends the edit, so an unchanged text after
        /// an edit is exactly the cancel.</summary>
        public static bool Committed(string before, string after)
        {
            return !string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal);
        }

        private void Begin(IUITextMeshInputField field)
        {
            if (field == null || !field.Interactable)
            {
                return;
            }

            // Read BEFORE the handover: the text a cancel puts back is the one that was there when
            // the player asked to edit.
            _snapshot = TextOf(field);
            _editing = field;
            _sawFocus = false;
            _framesWaited = 0;
            field.Select();
            field.ActivateInputField();

            // Queued, so it follows whatever the activation itself said rather than cutting it off.
            Say(ModText.Get(ModStrings.UI.EditStarted));
            _echo.Begin(field);
        }

        private void Finish(bool announce)
        {
            IUITextMeshInputField field = _editing;
            string before = _snapshot;
            _echo.Stop();
            _editing = null;
            _snapshot = null;
            _sawFocus = false;
            _framesWaited = 0;

            if (!announce || field == null)
            {
                return;
            }

            string after = TextOf(field);
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

        private static bool EnterIsDown()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
        }

        /// <summary>
        /// Whether the field still has the keyboard, asked of TMP's own <c>isFocused</c> rather than of
        /// <see cref="IUITextMeshInputField.Focused"/>. The game's property also answers true for its
        /// gamepad "this field controls the UI input" latch, which is cleared by the field's DESELECT
        /// and not by deactivating it - so a field the game had merely deactivated still read as
        /// focused and the edit never ended (measured on the join-game popup, 2026-09-06). This is
        /// also the signal the stand-down reads, so the two halves agree about when the keyboard is
        /// the game's.
        /// </summary>
        private static bool IsFocused(IUITextMeshInputField field)
        {
            try
            {
                UITextMeshInputField concrete = field as UITextMeshInputField;
                TMPro.TMP_InputField input = concrete != null ? concrete.GetInputField() : null;
                return input != null ? input.isFocused : field != null && field.Focused;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string TextOf(IUITextMeshInputField field)
        {
            try
            {
                return field == null ? string.Empty : field.InputFieldValue ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void Say(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }
    }
}
