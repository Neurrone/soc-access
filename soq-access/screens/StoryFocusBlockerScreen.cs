using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    // Holds accessibility focus between story entries so the adventure map
    // below does not regain focus and speak a tile before the next story screen.
    internal sealed class StoryFocusBlockerScreen : Screen
    {
        private readonly Func<bool> _isPresent;

        public StoryFocusBlockerScreen(Func<bool> isPresent)
            : base(new ContainerWidget("story-focus-blocker", string.Empty))
        {
            _isPresent = isPresent;
        }

        public override bool IsPresent()
        {
            return _isPresent == null || _isPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return IsNonGlobalAccessibilityAction(actionKey);
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            return IsNonGlobalAccessibilityAction(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            return false;
        }

        private static bool IsNonGlobalAccessibilityAction(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                return false;
            }

            for (int i = 0; i < AccessibilityActions.NON_GLOBAL_ACTIONS.Length; i++)
            {
                InputAction action = AccessibilityActions.NON_GLOBAL_ACTIONS[i];
                if (action != null && action.Key == actionKey)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
