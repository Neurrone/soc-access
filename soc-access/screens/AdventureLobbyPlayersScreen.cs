using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureLobbyPlayersScreen : Screen
    {
        private const int PlayerSlotsIndex = 1;

        private readonly AdventureLobbyPlayersAdapter _adapter;

        public AdventureLobbyPlayersScreen(AdventureLobbyPlayersAdapter adapter)
            : base(BuildRoot(adapter, -1))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyPlayersAdapter adapter = FindActiveLobbyMenu(null);
            return adapter != null ? new AdventureLobbyPlayersScreen(adapter) : null;
        }

        public bool Matches(LobbyMenu menu)
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

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
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

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int slotIndex = GetFocusedSlotIndex();
            _adapter.InvalidateSnapshot();
            RootWidget = BuildRoot(_adapter, slotIndex);
            RootWidget.SetFocusByIndexSilently(focusedIndex);
        }

        private int GetFocusedSlotIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(PlayerSlotsIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private static ContainerWidget BuildRoot(AdventureLobbyPlayersAdapter adapter, int focusedSlotIndex)
        {
            ContainerWidget root = new ContainerWidget("adventure-lobby-players-screen", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "lobby-map-summary",
                () => adapter.MapSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.MapSummary)));

            MenuWidget slots = BuildPlayerSlotsMenu(adapter);
            if (focusedSlotIndex >= 0)
            {
                slots.SetFocusByIndexSilently(focusedSlotIndex);
            }

            root.AddChild(slots);

            AddSelectedSlotButton(root, "selected-player-faction", adapter, slot => slot.FactionButton);
            AddSelectedSlotButton(root, "selected-player-color", adapter, slot => slot.ColorButton);
            AddSelectedSlotButton(root, "selected-player-starting-wielder", adapter, slot => slot.StartingWielderButton);
            AddSelectedSlotButton(root, "selected-player-partnership", adapter, slot => slot.PartnershipButton);
            AddSelectedSlotButton(root, "selected-player-ai-difficulty", adapter, slot => slot.AiDifficultyButton);
            AddSelectedSlotButton(root, "selected-player-join", adapter, slot => slot.JoinButton);
            AddSelectedSlotButton(root, "selected-player-settings", adapter, slot => slot.PlayerSettingsButton);
            AddSelectedSlotButton(root, "selected-player-leave", adapter, slot => slot.LeaveButton);
            AddSelectedSlotButton(root, "selected-player-toggle-ai", adapter, slot => slot.ToggleAiButton);
            AddSelectedSlotButton(root, "selected-player-kick", adapter, slot => slot.KickButton);
            AddSelectedSlotButton(root, "selected-player-actions", adapter, slot => slot.PlayerActionsButton);
            AddDlcRequirement(root, adapter);
            AddMixedFactions(root, adapter);
            AddGameSettings(root, adapter);
            AddLobbyButton(root, "set-ready", adapter.GetSetReadyButton());
            AddLobbyButton(root, "set-not-ready", adapter.GetSetNotReadyButton());
            AddLobbyButton(root, "start-game", adapter.GetStartGameButton());
            AddOptionalButton(root, "options", adapter.OptionsButton, adapter);
            AddOptionalButton(root, "back", adapter.BackButton, adapter);
            return root;
        }

        private static MenuWidget BuildPlayerSlotsMenu(AdventureLobbyPlayersAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("lobby-player-slots", adapter.PlayersLabel);
            IReadOnlyList<AdventureLobbyPlayersAdapter.PlayerSlotItem> slots = adapter.GetPlayerSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                AdventureLobbyPlayersAdapter.PlayerSlotItem slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    slot.Id,
                    () => slot.Label,
                    null,
                    () => false,
                    () =>
                    {
                        adapter.SelectedTeamId = slot.TeamId;
                        slot.FocusNative();
                    },
                    () => true,
                    () => slot.Tooltip));
            }

            return menu;
        }

        private static void AddSelectedSlotButton(
            ContainerWidget root,
            string id,
            AdventureLobbyPlayersAdapter adapter,
            System.Func<AdventureLobbyPlayersAdapter.PlayerSlotItem, AdventureLobbyPlayersAdapter.LobbyButtonItem> getButton)
        {
            root.AddChild(new ButtonWidget(
                id,
                () => GetSelectedButton(adapter, getButton)?.Label,
                () => ActivateSelectedSlotButton(id, adapter, getButton),
                () => GetSelectedButton(adapter, getButton)?.Focus(),
                () => GetSelectedButton(adapter, getButton)?.IsEnabled ?? false,
                () => GetSelectedButton(adapter, getButton)?.IsVisible ?? false,
                () => GetSelectedButton(adapter, getButton)?.Tooltip));
        }

        private static bool ActivateSelectedSlotButton(
            string id,
            AdventureLobbyPlayersAdapter adapter,
            System.Func<AdventureLobbyPlayersAdapter.PlayerSlotItem, AdventureLobbyPlayersAdapter.LobbyButtonItem> getButton)
        {
            AdventureLobbyPlayersAdapter.LobbyButtonItem button = GetSelectedButton(adapter, getButton);
            return button != null && button.Activate();
        }

        private static AdventureLobbyPlayersAdapter.LobbyButtonItem GetSelectedButton(
            AdventureLobbyPlayersAdapter adapter,
            System.Func<AdventureLobbyPlayersAdapter.PlayerSlotItem, AdventureLobbyPlayersAdapter.LobbyButtonItem> getButton)
        {
            AdventureLobbyPlayersAdapter.PlayerSlotItem slot = adapter != null ? adapter.SelectedSlot : null;
            return slot != null && getButton != null ? getButton(slot) : null;
        }

        private static void AddDlcRequirement(ContainerWidget root, AdventureLobbyPlayersAdapter adapter)
        {
            root.AddChild(new TextWidget(
                "selected-player-dlc-requirement",
                () => adapter.SelectedSlot != null ? adapter.SelectedSlot.DlcRequirementText : string.Empty,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.SelectedSlot != null && !string.IsNullOrWhiteSpace(adapter.SelectedSlot.DlcRequirementText)));
        }

        private static void AddMixedFactions(ContainerWidget root, AdventureLobbyPlayersAdapter adapter)
        {
            AdventureLobbyPlayersAdapter.MixedFactionsItem item = adapter.GetMixedFactionsItem();
            if (item == null)
            {
                return;
            }

            root.AddChild(new CheckboxWidget(
                "mixed-factions",
                () => item.Label,
                item.Toggle,
                () => item.IsChecked,
                () => item.IsVisible,
                () => item.IsEnabled,
                () => item.Tooltip));
        }

        private static void AddGameSettings(ContainerWidget root, AdventureLobbyPlayersAdapter adapter)
        {
            AdventureLobbyPlayersAdapter.LobbyPlayerSettingsItem item = adapter.GetSettingsItem();
            if (item == null)
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                "game-settings",
                () => item.Label,
                item.Activate,
                item.Focus,
                () => item.IsEnabled,
                () => item.IsVisible,
                () => item.Tooltip));
        }

        private static void AddLobbyButton(ContainerWidget root, string id, AdventureLobbyPlayersAdapter.LobbyButtonItem item)
        {
            if (item == null)
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                () => item.Label,
                item.Activate,
                item.Focus,
                () => item.IsEnabled,
                () => item.IsVisible,
                () => item.Tooltip));
        }

        private static void AddOptionalButton(
            ContainerWidget root,
            string id,
            IMenuButtonAdapter button,
            AdventureLobbyPlayersAdapter adapter)
        {
            if (button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                button.GetLabel,
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsEnabled,
                button.IsVisible,
                () => adapter.GetButtonTooltip(button)));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button != null)
            {
                NativeSelectionUtility.Select(button);
            }
        }

        internal static AdventureLobbyPlayersAdapter FindActiveLobbyMenu(LobbyMenu targetMenu)
        {
            LobbyMenu[] menus = Resources.FindObjectsOfTypeAll<LobbyMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                LobbyMenu menu = menus[i];
                if (!IsLiveSceneLobbyMenu(menu))
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                AdventureLobbyPlayersAdapter adapter = new AdventureLobbyPlayersAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneLobbyMenu(LobbyMenu menu)
        {
            if (menu == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)menu).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
