using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventurePlayerMenuScreen : Screen
    {
        private const int PlayersMenuIndex = 0;
        private static readonly ResourceType[] ResourceSummaryOrder =
        {
            ResourceType.Gold,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };

        private readonly AdventurePlayerMenuAdapter _adapter;

        public AdventurePlayerMenuScreen(AdventurePlayerMenuAdapter adapter)
            : base(BuildRoot(adapter, -1))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventurePlayerMenu[] menus = Resources.FindObjectsOfTypeAll<AdventurePlayerMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                AdventurePlayerMenuAdapter adapter = new AdventurePlayerMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new AdventurePlayerMenuScreen(adapter);
                }
            }

            return null;
        }

        public bool Matches(AdventurePlayerMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.Source, menu);
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

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int playerFocusedIndex = GetFocusedPlayerIndex();
            RootWidget = BuildRoot(_adapter, playerFocusedIndex);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private int GetFocusedPlayerIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(PlayersMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private static ContainerWidget BuildRoot(AdventurePlayerMenuAdapter adapter, int focusedPlayerIndex)
        {
            ContainerWidget root = new ContainerWidget("adventure-players", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            MenuWidget players = BuildPlayersMenu(adapter);
            if (focusedPlayerIndex >= 0)
            {
                players.SetFocusByIndexSilently(focusedPlayerIndex);
            }

            root.AddChild(players);
            AddSelectedPlayerAction(root, "selected-player-non-aggression-pact", adapter, player => player.NonAggressionPact);
            AddSelectedPlayerAction(root, "selected-player-resources", adapter, player => player.Resources);
            AddSelectedPlayerAction(root, "selected-player-towns", adapter, player => player.Towns);
            AddSelectedPlayerAction(root, "selected-player-platform-actions", adapter, player => player.PlatformActions);
            AddSelectedPlayerAction(root, "selected-player-spectate-battle", adapter, player => player.SpectateBattle);
            root.AddChild(BuildSelectedPlayerResourcesMenu(adapter));
            root.AddChild(new ButtonWidget(
                "adventure-players-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true,
                adapter.IsPresent));
            return root;
        }

        private static MenuWidget BuildPlayersMenu(AdventurePlayerMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-players-list", adapter.Title);
            IReadOnlyList<AdventurePlayerMenuAdapter.PlayerItem> players = adapter.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                AdventurePlayerMenuAdapter.PlayerItem player = players[i];
                if (player == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    player.Id,
                    () => BuildPlayerLabel(player),
                    () => player.ScoreText,
                    () => false,
                    () =>
                    {
                        adapter.SelectedTeamId = player.TeamId;
                        player.FocusNative();
                    },
                    () => true,
                    () => player.Tooltip));
            }

            return menu;
        }

        private static MenuWidget BuildSelectedPlayerResourcesMenu(AdventurePlayerMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "selected-player-resources-summary",
                ModText.Get(ModStrings.Screens.Resources),
                () => adapter != null && adapter.SelectedPlayer != null && adapter.SelectedPlayer.HasResourceSummary);
            for (int i = 0; i < ResourceSummaryOrder.Length; i++)
            {
                AddSelectedPlayerResourceItem(menu, adapter, ResourceSummaryOrder[i]);
            }

            return menu;
        }

        private static void AddSelectedPlayerResourceItem(
            MenuWidget menu,
            AdventurePlayerMenuAdapter adapter,
            ResourceType resourceType)
        {
            ResourceType capturedType = resourceType;
            menu.AddItem(new MenuItemWidget(
                "selected-player-resource-" + capturedType.ToString().ToLowerInvariant(),
                () => adapter != null && adapter.SelectedPlayer != null
                    ? adapter.SelectedPlayer.GetResourceLabel(capturedType)
                    : string.Empty,
                null,
                null,
                () =>
                {
                    if (adapter != null && adapter.SelectedPlayer != null)
                    {
                        adapter.SelectedPlayer.FocusResource(capturedType);
                    }
                },
                () => adapter != null && adapter.SelectedPlayer != null && adapter.SelectedPlayer.HasResourceSummary,
                () => adapter != null && adapter.SelectedPlayer != null
                    ? adapter.SelectedPlayer.GetResourceTooltip(capturedType)
                    : null));
        }

        private static string BuildPlayerLabel(AdventurePlayerMenuAdapter.PlayerItem player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, player.Name);
            AddIfNotEmpty(parts, player.TeamLabel);
            AddIfNotEmpty(parts, player.ColorLabel);
            AddIfNotEmpty(parts, player.RelationLabel);
            AddIfNotEmpty(parts, player.AiLabel);
            return ModText.JoinListWithCommas(parts);
        }

        private static void AddSelectedPlayerAction(
            ContainerWidget root,
            string id,
            AdventurePlayerMenuAdapter adapter,
            System.Func<AdventurePlayerMenuAdapter.PlayerItem, AdventurePlayerMenuAdapter.ActionItem> getAction)
        {
            root.AddChild(new ButtonWidget(
                id,
                () => GetSelectedAction(adapter, getAction)?.Label,
                () => ActivateSelectedAction(adapter, getAction),
                () => GetSelectedAction(adapter, getAction)?.Focus(),
                () => GetSelectedAction(adapter, getAction)?.IsEnabled ?? false,
                () => GetSelectedAction(adapter, getAction)?.IsVisible ?? false,
                () => GetSelectedAction(adapter, getAction)?.Tooltip));
        }

        private static bool ActivateSelectedAction(
            AdventurePlayerMenuAdapter adapter,
            System.Func<AdventurePlayerMenuAdapter.PlayerItem, AdventurePlayerMenuAdapter.ActionItem> getAction)
        {
            AdventurePlayerMenuAdapter.ActionItem action = GetSelectedAction(adapter, getAction);
            return action != null && action.Activate();
        }

        private static AdventurePlayerMenuAdapter.ActionItem GetSelectedAction(
            AdventurePlayerMenuAdapter adapter,
            System.Func<AdventurePlayerMenuAdapter.PlayerItem, AdventurePlayerMenuAdapter.ActionItem> getAction)
        {
            AdventurePlayerMenuAdapter.PlayerItem player = adapter != null ? adapter.SelectedPlayer : null;
            return player != null && getAction != null ? getAction(player) : null;
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (parts != null && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }
    }
}
