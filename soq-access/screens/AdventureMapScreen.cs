using System;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(AdventureViewInstaller), "Container");
        private static string _lastProbeDiagnostic;

        private const int GridIndex = 0;
        private const int TroopSlotsIndex = 8;
        private readonly AdventureMapAdapter _adapter;
        private readonly AdventureMapEventListener _eventListener;
        private readonly AdventureMapGrid _grid;
        private Action<int> _commanderStatisticsChangedHandler;
        private Action<CommanderChangedPayload> _commanderChangedHandler;
        private bool _isTopScreen;

        public AdventureMapScreen(AdventureMapAdapter adapter, AdventureMapEventListener eventListener)
            : this(adapter, eventListener, new AdventureMapGrid(adapter))
        {
        }

        public static Screen TryBuildActiveScreen()
        {
            return FindActiveAdventureMap();
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

        public override System.Collections.Generic.IEnumerable<ReviewBufferKind> VisibleReviewBuffers
        {
            get
            {
                foreach (ReviewBufferKind kind in base.VisibleReviewBuffers)
                {
                    yield return kind;
                }

                yield return ReviewBufferKind.AdventureMapNotifications;
            }
        }

        public override void OnPush()
        {
            _eventListener?.Attach();
            AttachListeners();
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
            DetachListeners();
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

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            _commanderStatisticsChangedHandler = HandleCommanderStatisticsChanged;
            _adapter.Facade.Commands.OnCommanderStatisticsChanged =
                (Action<int>)Delegate.Combine(
                    _adapter.Facade.Commands.OnCommanderStatisticsChanged,
                    _commanderStatisticsChangedHandler);

            if (_adapter.SelectionHandler != null)
            {
                _commanderChangedHandler = HandleCommanderChanged;
                _adapter.SelectionHandler.OnCommanderChanged =
                    (Action<CommanderChangedPayload>)Delegate.Combine(
                        _adapter.SelectionHandler.OnCommanderChanged,
                        _commanderChangedHandler);
            }
        }

        private void DetachListeners()
        {
            if (_adapter == null)
            {
                return;
            }

            if (_adapter.Facade != null && _adapter.Facade.Commands != null && _commanderStatisticsChangedHandler != null)
            {
                _adapter.Facade.Commands.OnCommanderStatisticsChanged =
                    (Action<int>)Delegate.Remove(
                        _adapter.Facade.Commands.OnCommanderStatisticsChanged,
                        _commanderStatisticsChangedHandler);
                _commanderStatisticsChangedHandler = null;
            }

            if (_adapter.SelectionHandler != null && _commanderChangedHandler != null)
            {
                _adapter.SelectionHandler.OnCommanderChanged =
                    (Action<CommanderChangedPayload>)Delegate.Remove(
                        _adapter.SelectionHandler.OnCommanderChanged,
                        _commanderChangedHandler);
                _commanderChangedHandler = null;
            }
        }

        private void HandleCommanderStatisticsChanged(int commanderId)
        {
            ICommanderState selectedCommander = _adapter != null && _adapter.SelectionHandler != null
                ? _adapter.SelectionHandler.SelectedCommander
                : null;
            if (selectedCommander == null || selectedCommander.Id != commanderId || RootWidget == null)
            {
                return;
            }

            RebuildTroopSlotsMenu();
        }

        private void HandleCommanderChanged(CommanderChangedPayload payload)
        {
            if (payload == null || payload.SelectedCommander == null || RootWidget == null)
            {
                return;
            }

            RebuildTroopSlotsMenu();
        }

        private void RebuildTroopSlotsMenu()
        {
            MenuWidget previousMenu = RootWidget.GetChildAt(TroopSlotsIndex) as MenuWidget;
            int previousMenuIndex = previousMenu != null ? previousMenu.FocusedIndex : -1;
            bool wasTroopMenuFocused = RootWidget.FocusedIndex == TroopSlotsIndex;
            MenuWidget newMenu = BuildTroopSlotsMenu(_adapter.Hud);
            if (!RootWidget.ReplaceChildAt(TroopSlotsIndex, newMenu))
            {
                return;
            }

            if (wasTroopMenuFocused && !newMenu.SetFocusByIndex(previousMenuIndex))
            {
                RootWidget.SetFocusByIndex(TroopSlotsIndex);
            }
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
                () => Portrait.BuildNativeTooltip(
                    () => portrait.TooltipTarget,
                    portrait.Localization,
                    portrait.RefreshTooltip),
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
            return TroopHudMenu.Build(
                "adventure-troop-slots",
                "Troops",
                adapter != null ? adapter.Troops : null,
                adapter != null ? adapter.IsTroopMenuVisible : (Func<bool>)null);
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

        private static AdventureMapScreen FindActiveAdventureMap()
        {
            AdventureViewInstaller[] installers = Resources.FindObjectsOfTypeAll<AdventureViewInstaller>();
            if (installers.Length == 0)
            {
                LogProbeDiagnostic("Adventure map probe found no AdventureViewInstaller instances");
                return null;
            }

            int liveInstallers = 0;
            for (int i = 0; i < installers.Length; i++)
            {
                AdventureViewInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                liveInstallers++;
                DiContainer container = GetContainer(installer);
                IClientAdventureFacade facade = TryResolve<IClientAdventureFacade>(container);
                ISelectionHandler selectionHandler = TryResolve<ISelectionHandler>(container);
                IFogManager fogManager = TryResolve<IFogManager>(container);
                IGrid grid = TryResolve<IGrid>(container);
                ICameraController cameraController = TryResolve<ICameraController>(container);
                IAdventureTooltipManager tooltipManager = TryResolve<IAdventureTooltipManager>(container);
                ILocalizationHandler localizationHandler = TryResolve<ILocalizationHandler>(container);
                ICartographyVisualManifest cartographyVisualManifest = TryResolve<ICartographyVisualManifest>(container);
                IHumanAdventureController humanAdventureController = TryResolve<IHumanAdventureController>(container);
                IHumanAdventureControllerFacade humanAdventureControllerFacade = TryResolve<IHumanAdventureControllerFacade>(container);
                IInputManager inputManager = TryResolve<IInputManager>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                AdventureMapAdapter adapter = new AdventureMapAdapter(
                    installer,
                    container,
                    facade,
                    selectionHandler,
                    fogManager,
                    grid,
                    cameraController,
                    cartographyConverter,
                    tooltipManager,
                    localizationHandler,
                    cartographyVisualManifest,
                    humanAdventureController,
                    humanAdventureControllerFacade,
                    inputManager);
                if (adapter.IsPresent())
                {
                    LogProbeDiagnostic("Adventure map probe found ready adventure map");
                    AdventureMapEventListener eventListener = new AdventureMapEventListener(
                        facade,
                        selectionHandler,
                        humanAdventureControllerFacade,
                        localizationHandler);
                    return new AdventureMapScreen(adapter, eventListener);
                }

                LogProbeDiagnostic("Adventure map probe found installer but adapter is not ready: " + adapter.GetReadinessDiagnostic());
            }

            if (liveInstallers == 0)
            {
                LogProbeDiagnostic("Adventure map probe found " + installers.Length + " installer instances but none in a loaded scene");
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(AdventureViewInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(AdventureViewInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object TryResolveByTypeName(DiContainer container, string typeName)
        {
            if (container == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            try
            {
                return container.Resolve(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void LogProbeDiagnostic(string message)
        {
            if (message == _lastProbeDiagnostic)
            {
                return;
            }

            _lastProbeDiagnostic = message;
            SoqAccessPlugin.Instance?.LogInfo(message);
        }
    }
}
