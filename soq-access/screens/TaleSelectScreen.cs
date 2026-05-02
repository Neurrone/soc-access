using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TaleSelectScreen : Screen
    {
        private readonly TaleSelectAdapter _adapter;

        public TaleSelectScreen(TaleSelectAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(TaleSelectAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "tale-select-screen",
                adapter != null ? adapter.GetTitle() : string.Empty);
            root.AddChild(new TextWidget(
                "title",
                () => adapter != null ? adapter.GetTitle() : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));

            MenuWidget menu = new MenuWidget("tale-select-menu", "Campaigns and tales");
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddTaleItems(menu, adapter);
            root.AddChild(menu);
            AddOptionalButton(root, adapter.OptionsButton);
            AddOptionalButton(root, adapter.BackButton);
            return root;
        }

        private static void AddTaleItems(MenuWidget menu, TaleSelectAdapter adapter)
        {
            if (menu == null || adapter == null || adapter.Tales == null)
            {
                return;
            }

            for (int i = 0; i < adapter.Tales.Count; i++)
            {
                TaleButtonAdapter item = adapter.Tales[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.FocusNative,
                    item.IsVisible));
            }
        }

        private static void AddOptionalButton(ContainerWidget root, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                button.Id,
                button.GetLabel(),
                button.Activate,
                () => FocusNativeButton(button.Button),
                () => button.IsVisible(),
                () => button.IsVisible()));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            Component component = button;
            NativeSelectionUtility.Select(component);
        }
    }
}
