using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PauseMenuScreen : Screen
    {
        private readonly PauseMenuAdapter _adapter;

        public PauseMenuScreen(PauseMenuAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        private static ContainerWidget BuildRootWidget(PauseMenuAdapter adapter)
        {
            string title = adapter != null && !string.IsNullOrWhiteSpace(adapter.Title)
                ? adapter.Title
                : "Pause menu";
            ContainerWidget root = new ContainerWidget("pause-menu-screen", title);
            MenuWidget menu = new MenuWidget("pause-menu", "Pause menu");

            if (adapter != null)
            {
                AddItems(menu, adapter.Items);
            }

            root.AddChild(menu);
            return root;
        }

        private static void AddItems(MenuWidget menu, IReadOnlyList<PauseMenuAdapter.Item> items)
        {
            if (menu == null || items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PauseMenuAdapter.Item item = items[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.Select,
                    item.IsVisible,
                    (Tooltip)null));
            }
        }
    }
}
