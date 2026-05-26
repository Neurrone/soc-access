using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PlatformUserMenuScreen : Screen
    {
        private readonly PlatformUserMenuAdapter _adapter;

        public PlatformUserMenuScreen(PlatformUserMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PlatformUserMenuAdapter adapter = FindActiveMenu(null);
            return adapter != null ? new PlatformUserMenuScreen(adapter) : null;
        }

        public bool Matches(PlatformUserMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
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

        private static ContainerWidget BuildRoot(PlatformUserMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("platform-user-menu", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            MenuWidget actions = new MenuWidget("platform-user-actions", adapter.Title);
            IReadOnlyList<PlatformUserMenuAdapter.ActionItem> items = adapter.GetActions();
            for (int i = 0; i < items.Count; i++)
            {
                PlatformUserMenuAdapter.ActionItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                actions.AddItem(new MenuItemWidget(
                    item.Id,
                    () => item.Label,
                    null,
                    item.Activate,
                    item.FocusNative,
                    () => item.IsVisible,
                    () => item.Tooltip,
                    onUnfocus: null,
                    isEnabled: () => item.IsEnabled));
            }

            root.AddChild(actions);
            root.AddChild(new ButtonWidget(
                "platform-user-menu-cancel",
                () => adapter.CancelLabel,
                adapter.Cancel,
                adapter.HideNativeTooltip,
                () => true,
                adapter.IsPresent));
            return root;
        }

        internal static PlatformUserMenuAdapter FindActiveMenu(PlatformUserMenu targetMenu)
        {
            PlatformUserMenu[] menus = Resources.FindObjectsOfTypeAll<PlatformUserMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PlatformUserMenu menu = menus[i];
                if (menu == null)
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                PlatformUserMenuAdapter adapter = new PlatformUserMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}
