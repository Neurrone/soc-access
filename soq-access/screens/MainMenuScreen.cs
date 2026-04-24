using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MainMenuScreen : Screen
    {
        private readonly MainMenuAdapter _adapter;

        public MainMenuScreen(MainMenuAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRootWidget(adapter))
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
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.Focus,
                    item.IsVisible));
            }
        }
    }
}
