using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE POINTER RESTING ON A CONTROL, SIMULATED.
    ///
    /// Some of this game's controls only show what they are worth while the mouse is over them: a
    /// level-up card fades its skill icon and its description in on <c>OnPointerEnter</c> and fades
    /// them out again on <c>OnPointerExit</c> (<c>CommanderLevelUpSkillComponent</c>). Selecting such
    /// a control natively does nothing for it, so a screen that wants the game to LOOK the way it
    /// would under the mouse has to send the pointer events itself.
    ///
    /// The graph has an enter hook per node and no leave hook of its own, so the pair has to be kept
    /// somewhere: this class remembers which component is currently being told it is hovered and
    /// exits that one before entering the next. Requests are idempotent - moving to the component
    /// already hovered does nothing, which is what makes it safe to call from a focus visual that
    /// runs whenever the aim changes.
    ///
    /// Endless Space 2 Access's <c>PointerFocus</c> in the smallest form this game needs.
    /// </summary>
    public static class PointerHover
    {
        private static Component _current;

        /// <summary>The component the game is currently being told the pointer rests on, or null.</summary>
        public static Component Current
        {
            get { return _current; }
        }

        /// <summary>Rest the pointer on <paramref name="component"/>, leaving whatever it rested on
        /// before. Null releases.</summary>
        public static void MoveTo(Component component)
        {
            if (component == null)
            {
                Release();
                return;
            }

            if (ReferenceEquals(_current, component))
            {
                return;
            }

            Release();
            _current = component;
            NativeSelectionUtility.PointerEnter(component);
        }

        /// <summary>Take the pointer off whatever it was resting on - the screen is going away, or
        /// focus has left the controls that need it.</summary>
        public static void Release()
        {
            Component previous = _current;
            _current = null;
            if (previous != null)
            {
                NativeSelectionUtility.PointerExit(previous);
            }
        }
    }
}
