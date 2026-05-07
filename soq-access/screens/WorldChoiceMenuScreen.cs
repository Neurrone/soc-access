using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class WorldChoiceMenuScreen : Screen
    {
        private readonly WorldChoiceMenuAdapter _adapter;

        public WorldChoiceMenuScreen(WorldChoiceMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(WorldChoiceMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("world-choice-menu", "World choice menu");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "world-choice-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(TroopHudMenu.Build(
                "world-choice-troops",
                "Troops",
                adapter.Troops,
                () => true));

            root.AddChild(new TextWidget(
                "world-choice-body",
                () => adapter.Body,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildChoiceMenu(adapter));

            root.AddChild(new ButtonWidget(
                "world-choice-confirm",
                "Confirm",
                adapter.ActivateConfirm,
                adapter.HideNativeTooltip,
                adapter.IsConfirmEnabled));

            root.AddChild(new ButtonWidget(
                "world-choice-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildChoiceMenu(WorldChoiceMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("world-choice-rewards", "Rewards");
            IReadOnlyList<WorldChoiceMenuAdapter.ChoiceItem> choices = adapter.GetChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                WorldChoiceMenuAdapter.ChoiceItem choice = choices[i];
                menu.AddItem(new MenuItemWidget(
                    choice.Id,
                    () => choice.Label,
                    () => choice.Status,
                    () => false,
                    choice.OnFocus,
                    choice.IsVisible,
                    choice.Tooltip));
            }

            return menu;
        }
    }
}
