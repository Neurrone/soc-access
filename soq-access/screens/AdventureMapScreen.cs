using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapScreen : Screen
    {
        private const int GridIndex = 0;
        private readonly AdventureMapAdapter _adapter;
        private readonly AdventureMapEventListener _eventListener;
        private readonly AdventureMapGrid _grid;
        private bool _isTopScreen;

        public AdventureMapScreen(AdventureMapAdapter adapter, AdventureMapEventListener eventListener)
            : this(adapter, eventListener, new AdventureMapGrid(adapter))
        {
        }

        private AdventureMapScreen(AdventureMapAdapter adapter, AdventureMapEventListener eventListener, AdventureMapGrid grid)
            : base(BuildRoot(adapter, grid))
        {
            _adapter = adapter;
            _eventListener = eventListener;
            _grid = grid;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            _eventListener?.Attach();
            AccessibilityEventBus.Subscribe(HandleAccessibilityEvent);
        }

        public override void OnFocus()
        {
            _isTopScreen = true;
            base.OnFocus();
        }

        public override void OnUnfocus()
        {
            _isTopScreen = false;
            _adapter?.ClearFocusedTileOverlay();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            _isTopScreen = false;
            _eventListener?.Detach();
            _adapter?.ClearFocusedTileOverlay();
        }

        public void FocusGrid()
        {
            RootWidget?.SetFocusByIndex(GridIndex);
        }

        public void FocusGridTile(Vector2Int tile)
        {
            if (_grid != null && _grid.FocusTile(tile))
            {
                RootWidget?.SetFocusByIndex(GridIndex);
                return;
            }

            FocusGrid();
        }

        private void HandleAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null)
            {
                if (_isTopScreen && !hudVisibility.IsVisible)
                {
                    FocusGrid();
                }

                return;
            }

            MapCameraFocusEvent cameraFocus = accessibilityEvent as MapCameraFocusEvent;
            if (cameraFocus == null)
            {
                return;
            }

            if (_isTopScreen)
            {
                FocusGridTile(cameraFocus.Tile);
                return;
            }

            if (_grid != null && _grid.FocusTileSilently(cameraFocus.Tile))
            {
                RootWidget?.SetFocusByIndexSilently(GridIndex);
                return;
            }

            RootWidget?.SetFocusByIndexSilently(GridIndex);
        }

        private static ContainerWidget BuildRoot(AdventureMapAdapter adapter, AdventureMapGrid grid)
        {
            ContainerWidget root = new ContainerWidget("adventure_map_screen", "Adventure map");
            root.AddChild(grid);
            // TODO: minimap accessibility is deferred; keep the adventure map grid as the first tab stop.
            if (adapter == null)
            {
                return root;
            }

            CommanderHudPortraitAdapter portrait = adapter.Hud.SelectedWielderPortrait;
            root.AddChild(Portrait.Button(
                "adventure-selected-wielder",
                () => portrait.Name,
                portrait.Click,
                portrait.Focus,
                () => portrait.Tooltip,
                () => portrait.IsEnabled,
                () => portrait.IsVisible));

            root.AddChild(new TextWidget(
                "adventure-experience",
                () => adapter.Hud.ExperienceLabel,
                adapter.Hud.FocusExperience,
                false,
                () => adapter.Hud.ExperienceTooltip,
                adapter.Hud.IsExperienceVisible));
            root.AddChild(new ButtonWidget(
                "adventure-level-up",
                () => adapter.Hud.LevelUpButtonLabel,
                adapter.Hud.ClickLevelUpButton,
                adapter.Hud.FocusLevelUpButton,
                adapter.Hud.IsLevelUpButtonEnabled,
                adapter.Hud.IsLevelUpButtonVisible));
            root.AddChild(BuildEssenceMenu(adapter.Hud));
            root.AddChild(new ButtonWidget(
                "adventure-inventory",
                () => adapter.Hud.InventoryButtonLabel,
                adapter.Hud.ClickInventoryButton,
                adapter.Hud.FocusInventoryButton,
                adapter.Hud.IsInventoryButtonEnabled,
                adapter.Hud.IsInventoryButtonVisible,
                () => adapter.Hud.InventoryButtonTooltip));
            root.AddChild(new ButtonWidget(
                "adventure-move-to-destination",
                () => adapter.Hud.MoveToDestinationButtonLabel,
                adapter.Hud.ClickMoveToDestinationButton,
                adapter.Hud.FocusMoveToDestinationButton,
                adapter.Hud.IsMoveToDestinationButtonEnabled,
                adapter.Hud.IsMoveToDestinationButtonVisible,
                () => adapter.Hud.MoveToDestinationButtonTooltip));
            root.AddChild(new ButtonWidget(
                "adventure-spellbook",
                () => adapter.Hud.SpellbookButtonLabel,
                adapter.Hud.ClickSpellbookButton,
                adapter.Hud.FocusSpellbookButton,
                adapter.Hud.IsSpellbookButtonEnabled,
                adapter.Hud.IsSpellbookButtonVisible,
                () => adapter.Hud.SpellbookButtonTooltip));
            root.AddChild(BuildTroopSlotsMenu(adapter.Hud));
            root.AddChild(BuildResourcesMenu(adapter.Hud));
            root.AddChild(BuildObjectivesMenu(adapter.Hud));
            root.AddChild(BuildNotificationsMenu(adapter.Hud));
            root.AddChild(BuildTownListMenu(adapter.Hud));
            root.AddChild(BuildWielderListMenu(adapter.Hud));
            root.AddChild(new ButtonWidget(
                "adventure-options",
                () => adapter.Hud.OptionsButtonLabel,
                adapter.Hud.ClickOptionsButton,
                adapter.Hud.FocusOptionsButton,
                adapter.Hud.IsOptionsButtonEnabled,
                adapter.Hud.IsOptionsButtonVisible,
                () => adapter.Hud.OptionsButtonTooltip));
            root.AddChild(BuildKingdomOverviewMenu(adapter.Hud));
            root.AddChild(new ButtonWidget(
                "adventure-bug-report",
                () => adapter.Hud.BugReportButtonLabel,
                adapter.Hud.ClickBugReportButton,
                adapter.Hud.FocusBugReportButton,
                adapter.Hud.IsBugReportButtonEnabled,
                adapter.Hud.IsBugReportButtonVisible,
                () => adapter.Hud.BugReportButtonTooltip));
            root.AddChild(BuildTeamQueueMenu(adapter.Hud));
            root.AddChild(new ButtonWidget(
                "adventure-end-turn",
                () => adapter.Hud.EndTurnButtonLabel,
                adapter.Hud.ClickEndTurnButton,
                adapter.Hud.FocusEndTurnButton,
                adapter.Hud.IsEndTurnButtonEnabled,
                adapter.Hud.IsEndTurnButtonVisible,
                () => adapter.Hud.EndTurnButtonTooltip));
            root.AddChild(new TextWidget(
                "adventure-round",
                () => adapter.Hud.RoundTextLabel,
                null,
                false,
                (Tooltip)null,
                adapter.Hud.IsRoundTextVisible));
            return root;
        }

        private static MenuWidget BuildEssenceMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-essence", "Essence", adapter.IsEssenceMenuVisible);
            AddEssenceItem(menu, adapter, EssenceType.Order);
            AddEssenceItem(menu, adapter, EssenceType.Creation);
            AddEssenceItem(menu, adapter, EssenceType.Chaos);
            AddEssenceItem(menu, adapter, EssenceType.Arcana);
            AddEssenceItem(menu, adapter, EssenceType.Destruction);
            return menu;
        }

        private static void AddEssenceItem(MenuWidget menu, AdventureHudAdapter adapter, EssenceType essenceType)
        {
            EssenceType capturedType = essenceType;
            menu.AddItem(new MenuItemWidget(
                "adventure-essence-" + capturedType.ToString().ToLowerInvariant(),
                () => adapter.GetEssenceLabel(capturedType),
                null,
                null,
                () => adapter.FocusEssence(capturedType),
                () => adapter.IsEssenceMenuVisible(),
                () => adapter.GetEssenceTooltip(capturedType)));
        }

        private static MenuWidget BuildTroopSlotsMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-troop-slots", "Troops", adapter.IsTroopMenuVisible);
            for (int i = 0; i < 9; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-troop-slot-" + (capturedIndex + 1),
                    () => adapter.GetTroopSlotLabel(capturedIndex),
                    null,
                    null,
                    () => adapter.FocusTroopSlot(capturedIndex),
                    () => adapter.IsTroopSlotVisible(capturedIndex),
                    () => adapter.GetTroopSlotTooltip(capturedIndex)));
            }

            return menu;
        }

        private static MenuWidget BuildResourcesMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-resources", "Resources", adapter.IsResourcesMenuVisible);
            AddResourceItem(menu, adapter, ResourceType.Gold);
            AddResourceItem(menu, adapter, ResourceType.Stone);
            AddResourceItem(menu, adapter, ResourceType.Wood);
            AddResourceItem(menu, adapter, ResourceType.Glimmerweave);
            AddResourceItem(menu, adapter, ResourceType.AncientAmber);
            AddResourceItem(menu, adapter, ResourceType.CelestialOre);
            return menu;
        }

        private static void AddResourceItem(MenuWidget menu, AdventureHudAdapter adapter, ResourceType resourceType)
        {
            ResourceType capturedType = resourceType;
            menu.AddItem(new MenuItemWidget(
                "adventure-resource-" + capturedType.ToString().ToLowerInvariant(),
                () => adapter.GetResourceLabel(capturedType),
                null,
                null,
                () => adapter.FocusResource(capturedType),
                () => adapter.IsResourcesMenuVisible(),
                () => adapter.GetResourceTooltip(capturedType)));
        }

        private static MenuWidget BuildObjectivesMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-objectives", "Objectives", adapter.IsObjectivesMenuVisible);
            for (int i = 0; i < 16; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-objective-" + (capturedIndex + 1),
                    () => adapter.GetObjectiveLabel(capturedIndex),
                    null,
                    null,
                    () => adapter.FocusObjective(capturedIndex),
                    () => adapter.IsObjectiveVisible(capturedIndex),
                    () => adapter.GetObjectiveTooltip(capturedIndex),
                    adapter.UnfocusObjective));
            }

            return menu;
        }

        private static MenuWidget BuildNotificationsMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-notifications", "Notifications", adapter.IsNotificationsMenuVisible);
            for (int i = 0; i < 5; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-notification-" + (capturedIndex + 1),
                    () => adapter.GetNotificationLabel(capturedIndex),
                    null,
                    () => adapter.ClickNotification(capturedIndex),
                    () => adapter.FocusNotification(capturedIndex),
                    () => adapter.IsNotificationVisible(capturedIndex),
                    () => adapter.GetNotificationTooltip(capturedIndex)));
            }

            return menu;
        }

        private static MenuWidget BuildTownListMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-town-list", "Towns", adapter.IsTownListMenuVisible);
            for (int i = 0; i < 32; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-town-" + (capturedIndex + 1),
                    () => adapter.GetTownListEntryLabel(capturedIndex),
                    null,
                    () => adapter.ClickTownListEntry(capturedIndex),
                    () => adapter.FocusTownListEntry(capturedIndex),
                    () => adapter.IsTownListEntryVisible(capturedIndex),
                    () => adapter.GetTownListEntryTooltip(capturedIndex)));
            }

            return menu;
        }

        private static MenuWidget BuildWielderListMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-wielder-list", "Wielders", adapter.IsWielderListMenuVisible);
            for (int i = 0; i < 32; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-wielder-" + (capturedIndex + 1),
                    () => adapter.GetWielderListEntryLabel(capturedIndex),
                    null,
                    () => adapter.ClickWielderListEntry(capturedIndex),
                    () => adapter.FocusWielderListEntry(capturedIndex),
                    () => adapter.IsWielderListEntryVisible(capturedIndex),
                    () => adapter.GetWielderListEntryTooltip(capturedIndex)));
            }

            return menu;
        }

        private static MenuWidget BuildKingdomOverviewMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-kingdom-overview", "Kingdom overview", adapter.IsKingdomOverviewMenuVisible);
            for (int i = 0; i < 5; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-kingdom-overview-" + (capturedIndex + 1),
                    () => adapter.GetKingdomOverviewLabel(capturedIndex),
                    null,
                    () => adapter.ClickKingdomOverviewItem(capturedIndex),
                    () => adapter.FocusKingdomOverviewItem(capturedIndex),
                    () => adapter.IsKingdomOverviewItemVisible(capturedIndex),
                    () => adapter.GetKingdomOverviewTooltip(capturedIndex),
                    null,
                    () => adapter.IsKingdomOverviewItemEnabled(capturedIndex)));
            }

            return menu;
        }

        private static MenuWidget BuildTeamQueueMenu(AdventureHudAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("adventure-team-queue", "Turn order", adapter.IsTeamQueueMenuVisible);
            for (int i = 0; i < 16; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "adventure-team-queue-" + (capturedIndex + 1),
                    () => adapter.GetTeamQueueEntryLabel(capturedIndex),
                    null,
                    null,
                    () => adapter.FocusTeamQueueEntry(capturedIndex),
                    () => adapter.IsTeamQueueEntryVisible(capturedIndex),
                    () => adapter.GetTeamQueueEntryTooltip(capturedIndex)));
            }

            return menu;
        }
    }
}
