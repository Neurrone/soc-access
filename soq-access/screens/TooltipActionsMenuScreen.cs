using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TooltipActionsMenuScreen : Screen
    {
        public const string TooltipActionsMenuSourceKey = "TOOLTIP_ACTIONS_MENU";

        private readonly Func<bool> _close;

        public TooltipActionsMenuScreen(IReadOnlyList<TooltipAction> actions, Func<bool> close)
            : base(TooltipActionsMenuSourceKey, BuildRoot(actions, close))
        {
            _close = close;
        }

        public override bool IsPresent()
        {
            // This is an accessibility-owned transient menu, not a native game
            // screen. It is pushed and popped explicitly, so there is no runtime
            // object to probe.
            return true;
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _close != null && _close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(IReadOnlyList<TooltipAction> actions, Func<bool> close)
        {
            ContainerWidget root = new ContainerWidget("tooltip-actions-menu-screen", "Tooltip actions");
            MenuWidget menu = new MenuWidget("tooltip-actions-menu", "Tooltip actions");

            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    TooltipAction action = actions[i];
                    if (action == null)
                    {
                        continue;
                    }

                    TooltipAction capturedAction = action;
                    menu.AddItem(new MenuItemWidget(
                        "tooltip-action-" + i,
                        () => capturedAction.Label,
                        null,
                        () =>
                        {
                            close?.Invoke();
                            return capturedAction.Invoke != null && capturedAction.Invoke();
                        },
                        null,
                        null));
                }
            }

            menu.AddItem(new MenuItemWidget(
                "tooltip-actions-cancel",
                () => "Cancel",
                null,
                () => close != null && close(),
                null,
                null));

            root.AddChild(menu);
            return root;
        }
    }
}
