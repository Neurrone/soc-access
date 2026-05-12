using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MainMenuScreen : Screen
    {
        private static readonly string[] TopLevelItemIds =
        {
            "continue",
            "campaign",
            "skirmish",
            "load-game",
            "quit",
            "map-editor",
            "community-maps",
            "extras",
            "hotseat",
            "multiplayer"
        };

        private readonly MainMenuAdapter _adapter;

        public MainMenuScreen(MainMenuAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        private static ContainerWidget BuildRootWidget(MainMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("main-menu-screen", "Main menu");
            MenuWidget menu = new MenuWidget("main-menu", "Main");
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddItems(menu, adapter.TopLevelItems);
            root.AddChild(menu);
            return root;
        }

        private static void AddItems(MenuWidget root, System.Collections.Generic.IReadOnlyList<IMenuButtonAdapter> items)
        {
            if (root == null || items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                IMenuButtonAdapter item = items[i];
                if (item == null)
                {
                    continue;
                }

                root.AddItem(new MenuItemWidget(
                    GetTopLevelItemId(i),
                    item.GetLabel,
                    () => BuildMenuButtonStatus(item),
                    item.Activate,
                    null,
                    item.IsVisible));
            }
        }

        private static string GetTopLevelItemId(int index)
        {
            return index >= 0 && index < TopLevelItemIds.Length
                ? TopLevelItemIds[index]
                : "main-menu-item-" + index;
        }

        private static string BuildMenuButtonStatus(IMenuButtonAdapter item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string nativeStatus = item.GetStatus();
            if (item.IsEnabled())
            {
                return nativeStatus;
            }

            return string.IsNullOrWhiteSpace(nativeStatus) ? "disabled" : "disabled. " + nativeStatus;
        }
    }
}
