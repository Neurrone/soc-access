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
            EventSystem events = EventSystem.current;
            GameObject selected = events == null ? null : events.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            TMP_InputField field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }
    }
}
