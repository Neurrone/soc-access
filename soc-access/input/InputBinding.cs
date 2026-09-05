using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SongsOfConquestAccess.Input
{
    public abstract class InputBinding
    {
        // Stable identifier for this physical binding, derived from binding data
        // and used only for active/release tracking. It is not user-facing.
        public abstract string Id { get; }

        public virtual bool IsModified
        {
            get { return false; }
        }

        public abstract bool MatchesKeyDown(
            KeyControl keyControl,
            AccessibilityInputRouter.KeyboardStateSnapshot state,
            out Key pressedKey);
    }
}
