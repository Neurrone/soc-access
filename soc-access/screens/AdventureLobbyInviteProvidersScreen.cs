using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyInviteProvidersScreen : Screen
    {
        private readonly AdventureLobbyInviteProvidersAdapter _adapter;

        public AdventureLobbyInviteProvidersScreen(AdventureLobbyInviteProvidersAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyInviteProvidersAdapter adapter = FindActiveInviteProviders(null);
            return adapter != null ? new AdventureLobbyInviteProvidersScreen(adapter) : null;
        }

        public bool Matches(LobbyMultiplayerPanel panel)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, panel);
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

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Cancel();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(AdventureLobbyInviteProvidersAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("adventure-lobby-invite-providers", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            MenuWidget providers = new MenuWidget("invite-provider-buttons", adapter.Title);
            IReadOnlyList<AdventureLobbyInviteProvidersAdapter.ProviderButtonItem> items = adapter.GetProviderButtons();
            for (int i = 0; i < items.Count; i++)
            {
                AdventureLobbyInviteProvidersAdapter.ProviderButtonItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                providers.AddItem(new MenuItemWidget(
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

            root.AddChild(providers);
            root.AddChild(new ButtonWidget(
                "invite-provider-cancel",
                () => adapter.CancelLabel,
                adapter.Cancel,
                adapter.HideNativeTooltip,
                () => true,
                adapter.IsPresent));
            return root;
        }

        public static AdventureLobbyInviteProvidersAdapter FindActiveInviteProviders(LobbyMultiplayerPanel targetPanel)
        {
            LobbyMultiplayerPanel[] panels = Resources.FindObjectsOfTypeAll<LobbyMultiplayerPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                LobbyMultiplayerPanel panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                if (targetPanel != null && !ReferenceEquals(targetPanel, panel))
                {
                    continue;
                }

                AdventureLobbyInviteProvidersAdapter adapter = new AdventureLobbyInviteProvidersAdapter(panel);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}
