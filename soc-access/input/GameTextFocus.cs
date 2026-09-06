using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// Whether the game's own text input owns the keyboard right now - the STAND-DOWN signal.
    ///
    /// While a field is being typed into, every key belongs to that field: a letter is a letter, an
    /// arrow walks the caret, Backspace deletes. So the mod's input layer answers nothing at all
    /// while this is true - no screen claim, no global action, no typed-key claim - and what the
    /// player types is spoken by the echo the editing screen runs instead.
    ///
    /// The question is asked of the event system rather than of any particular field, because the
    /// mod is not always the one that opened the box: a mouse click on the game's own field puts the
    /// keyboard there too, and the layer has to stand down for that just the same.
    ///
    /// A static query over live engine state, with nothing of its own to reset, so it survives a hot
    /// reload by having no state to survive.
    /// </summary>
    public static class GameTextFocus
    {
        public static bool IsTyping()
        {
            // The field the mod's own editor handed the keyboard to, asked beside the selection: the
            // mod.io browser drives its own selection and can leave the event system pointing
            // elsewhere while its box is focused, and a letter typed then must not become a search.
            TMP_InputField editing = SongsOfConquestAccess.UI.GameTextEditor.CurrentInput;
            if (editing != null && editing.isFocused)
            {
                return true;
            }

            EventSystem events = EventSystem.current;
            GameObject selected = events == null ? null : events.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            TMP_InputField field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>Take the keyboard back from a field the GAME focused on its own (a lobby's game
        /// code box, a host popup's name box, selected by the game as the page opens). Left alone, the
        /// mod would stand down until the player found Escape; instead the field is deactivated and
        /// deselected as the mod's screen takes focus, and the field is reached again through its
        /// own edit node. Never called while a mod editor holds a handover.</summary>
        public static void Release()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events == null ? null : events.currentSelectedGameObject;
            TMP_InputField field = selected == null ? null : selected.GetComponent<TMP_InputField>();
            if (field == null || !field.isFocused)
            {
                return;
            }

            field.DeactivateInputField();
            events.SetSelectedGameObject(null);
        }
    }
}
