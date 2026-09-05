using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyIconDropdownScreen : Screen
    {
        private readonly AdventureLobbyIconDropdownAdapter _adapter;

        public AdventureLobbyIconDropdownScreen(AdventureLobbyIconDropdownAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyIconDropdownAdapter adapter = FindActiveDropdown(null);
            return adapter != null ? new AdventureLobbyIconDropdownScreen(adapter) : null;
        }

        public bool Matches(IconDropdown dropdown)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, dropdown);
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

        private static ContainerWidget BuildRoot(AdventureLobbyIconDropdownAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("adventure-lobby-icon-dropdown", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            MenuWidget options = new MenuWidget("icon-dropdown-options", adapter.Title);
            IReadOnlyList<AdventureLobbyIconDropdownAdapter.OptionItem> items = adapter.GetOptions();
            for (int i = 0; i < items.Count; i++)
            {
                AdventureLobbyIconDropdownAdapter.OptionItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                options.AddItem(new MenuItemWidget(
                    item.Id,
                    () => item.Label,
                    null,
                    () => ActivateOption(adapter, item),
                    item.FocusNative,
                    () => item.IsVisible,
                    () => item.Tooltip,
                    onUnfocus: null,
                    isEnabled: () => item.IsEnabled));
            }

            root.AddChild(options);
            root.AddChild(new ButtonWidget(
                "icon-dropdown-cancel",
                () => adapter.CancelLabel,
                adapter.Cancel,
                adapter.HideNativeTooltip,
                () => true,
                adapter.IsPresent));
            return root;
        }

        private static bool ActivateOption(AdventureLobbyIconDropdownAdapter adapter, AdventureLobbyIconDropdownAdapter.OptionItem item)
        {
            if (item == null)
            {
                return false;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyIconDropdownOptionActivating(
                adapter != null ? adapter.SourceKey as IconDropdown : null,
                item.TypeName);

            bool activated = item.Activate();
            if (!activated)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyIconDropdownOptionActivationFailed(
                    adapter != null ? adapter.SourceKey as IconDropdown : null);
            }

            return activated;
        }

        public static AdventureLobbyIconDropdownAdapter FindActiveDropdown(IconDropdown targetDropdown)
        {
            IconDropdown[] dropdowns = Resources.FindObjectsOfTypeAll<IconDropdown>();
            for (int i = 0; i < dropdowns.Length; i++)
            {
                IconDropdown dropdown = dropdowns[i];
                if (dropdown == null)
                {
                    continue;
                }

                if (targetDropdown != null && !ReferenceEquals(targetDropdown, dropdown))
                {
                    continue;
                }

                AdventureLobbyIconDropdownAdapter adapter = new AdventureLobbyIconDropdownAdapter(dropdown);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}
