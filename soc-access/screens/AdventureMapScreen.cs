using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(AdventureViewInstaller), "Container");
        private static readonly ResourceType[] ResourceSummaryOrder =
        {
            ResourceType.Gold,
            ResourceType.Wood,
            ResourceType.Stone,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };
        private static string _lastProbeDiagnostic;

        private const string ReturnToGridSoundKey = "Common_ClosePauseMenu";
        private const int GridIndex = 0;
        private const int TroopSlotsIndex = 14;
        private const int ResourcesIndex = 15;
        private const int ObjectivesIndex = 16;
        private const int NotificationsIndex = 17;
        private readonly AdventureMapAdapter _adapter;
        private readonly AdventureMapEventListener _eventListener;
        private readonly AdventureMapGrid _grid;
        private TeleportMenuAdapter _teleportMenuAdapter;
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
            : base(new ContainerWidget("adventure_map_screen", ModText.Get(ModStrings.Screens.AdventureMap)))
        {
            _adapter = adapter;
            _eventListener = eventListener;
            _grid = grid;
            RootWidget = BuildRoot(adapter, grid);
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
            _grid?.SetBeaconAudible(true);
        }

        public override void OnUnfocus()
        {
            _isTopScreen = false;
            _grid?.SetBeaconAudible(false);
            _adapter?.ClearFocusedTileOverlay();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            DetachListeners();
            _isTopScreen = false;
            _grid?.DisposeAudio();
            _eventListener?.Detach();
            _adapter?.ClearFocusedTileOverlay();
        }

        public override void Update()
        {
            base.Update();
            _eventListener?.Update();
        }

        public void FocusGrid()
        {
            RootWidget?.SetFocusByIndex(GridIndex);
        }

        public void EnterTeleportDestinationMode(TeleportMenuAdapter adapter)
        {
            if (adapter == null || !adapter.IsPresent())
            {
                return;
            }

            _teleportMenuAdapter = adapter;
            FocusCurrentTeleportDestination(speakInstruction: true);
        }

        public void ExitTeleportDestinationMode(TeleportMenu menu)
        {
            if (_teleportMenuAdapter == null)
            {
                return;
            }

            if (menu != null && !ReferenceEquals(_teleportMenuAdapter.SourceKey, menu))
            {
                return;
            }

            _teleportMenuAdapter = null;
            FocusGrid();
        }

        public bool MatchesTeleportMenu(TeleportMenu menu)
        {
            return _teleportMenuAdapter != null
                && (menu == null || ReferenceEquals(_teleportMenuAdapter.SourceKey, menu));
        }

        public override bool HasClaimed(string actionKey)
        {
            if (IsTeleportDestinationModeActive() && actionKey == AccessibilityActions.Cancel.Key)
            {
                return true;
            }

            if (IsTeleportDestinationModeActive() && IsTeleportSuppressedScreenAction(actionKey))
            {
                return true;
            }

            if (actionKey == AccessibilityActions.FocusHudTroops.Key)
            {
                return CanFocusHudWidget(TroopSlotsIndex);
            }

            if (actionKey == AccessibilityActions.FocusHudResources.Key)
            {
                return CanFocusHudWidget(ResourcesIndex);
            }

            if (actionKey == AccessibilityActions.FocusHudObjectives.Key)
            {
                return CanFocusHudWidget(ObjectivesIndex);
            }

            if (actionKey == AccessibilityActions.FocusHudNotifications.Key)
            {
                return CanFocusHudWidget(NotificationsIndex);
            }

            if (actionKey != AccessibilityActions.Cancel.Key)
            {
                return base.HasClaimed(actionKey);
            }

            return base.HasClaimed(actionKey) || !IsGridFocused();
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            if (IsTeleportDestinationModeActive() && IsTeleportSuppressedFocusedAction(actionKey))
            {
                return true;
            }

            return base.HasFocusedWidgetClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action == null)
            {
                return base.OnActionJustPressed(action);
            }

            if (IsTeleportDestinationModeActive())
            {
                if (action.Key == AccessibilityActions.Cancel.Key)
                {
                    return CancelTeleportDestination();
                }

                if (IsTeleportSuppressedAction(action.Key))
                {
                    return true;
                }

                if (IsGridFocused() && action.Key == AccessibilityActions.Activate.Key)
                {
                    return ConfirmTeleportDestinationFromGrid();
                }

                if (IsGridFocused() && action.Key == AccessibilityActions.MapSecondaryAction.Key)
                {
                    return true;
                }
            }

            if (action.Key == AccessibilityActions.FocusHudResources.Key)
            {
                return FocusHudResources();
            }

            if (action.Key == AccessibilityActions.FocusHudTroops.Key)
            {
                return FocusHudTroops();
            }

            if (action.Key == AccessibilityActions.FocusHudObjectives.Key)
            {
                return FocusHudObjectives();
            }

            if (action.Key == AccessibilityActions.FocusHudNotifications.Key)
            {
                return FocusHudNotifications();
            }

            if (action.Key != AccessibilityActions.Cancel.Key)
            {
                return base.OnActionJustPressed(action);
            }

            // Let focused controls cancel their own state first. Otherwise Escape
            // returns HUD focus to the grid; on the grid, native pause owns it.
            if (RootWidget != null && RootWidget.HandleAction(action))
            {
                return true;
            }

            if (IsGridFocused())
            {
                return false;
            }

            FocusGrid();
            NativeSoundUtility.PostEvent(ReturnToGridSoundKey);
            return true;
        }

        public bool FocusHudResources()
        {
            return FocusHudWidget(ResourcesIndex);
        }

        public bool FocusHudTroops()
        {
            return FocusHudWidget(TroopSlotsIndex);
        }

        public bool FocusHudObjectives()
        {
            return FocusHudWidget(ObjectivesIndex);
        }

        public bool FocusHudNotifications()
        {
            return FocusHudWidget(NotificationsIndex);
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

        public bool SummarizeResources()
        {
            AdventureHudAdapter hud = _adapter != null ? _adapter.Hud : null;
            if (hud == null)
            {
                return false;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < ResourceSummaryOrder.Length; i++)
            {
                ResourceType resourceType = ResourceSummaryOrder[i];
                string name = hud.GetResourceName(resourceType);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                parts.Add(ModText.Get(ModStrings.Common.ResourceAmount, hud.GetResourceAmount(resourceType), name));
            }

            if (parts.Count == 0)
            {
                return false;
            }

            SpeechPipeline.Output(new SpeechRequest(ModText.JoinList(parts), interrupt: false));
            return true;
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
            if (cameraFocus != null)
            {
                FocusMapTileFromEvent(cameraFocus.Tile);
                return;
            }

            MapWielderTeleportedEvent teleport = accessibilityEvent as MapWielderTeleportedEvent;
            if (teleport != null)
            {
                FocusMapTileFromEvent(teleport.Tile);
            }
        }

        private void FocusMapTileFromEvent(Vector2Int tile)
        {
            if (_isTopScreen)
            {
                FocusGridTile(tile);
                return;
            }

            if (_grid != null && _grid.FocusTileSilently(tile))
            {
                RootWidget?.SetFocusByIndexSilently(GridIndex);
                return;
            }

            RootWidget?.SetFocusByIndexSilently(GridIndex);
        }

        private bool IsGridFocused()
        {
            return ReferenceEquals(RootWidget?.FocusedChild, _grid)
                && ReferenceEquals(UIManager.CurrentWidget, _grid);
        }

        private bool IsTeleportDestinationModeActive()
        {
            return _teleportMenuAdapter != null && _teleportMenuAdapter.IsPresent();
        }

        private TeleportMenuAdapter GetTeleportMenuAdapter()
        {
            return IsTeleportDestinationModeActive() ? _teleportMenuAdapter : null;
        }

        private void FocusCurrentTeleportDestination(bool speakInstruction)
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null)
            {
                return;
            }

            if (speakInstruction)
            {
                string instruction = teleport.InstructionText;
                if (!string.IsNullOrWhiteSpace(instruction))
                {
                    SpeechPipeline.Output(new SpeechRequest(instruction, interrupt: true));
                }
            }

            FocusGridTile(teleport.CurrentDestination);
        }

        private bool SelectPreviousTeleportDestination()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null || !teleport.SelectPrevious())
            {
                return false;
            }

            FocusCurrentTeleportDestination(speakInstruction: false);
            return true;
        }

        private bool SelectNextTeleportDestination()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null || !teleport.SelectNext())
            {
                return false;
            }

            FocusCurrentTeleportDestination(speakInstruction: false);
            return true;
        }

        private bool ConfirmTeleportDestinationFromGrid()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null)
            {
                return true;
            }

            if (_grid == null || _grid.CursorTile != teleport.CurrentDestination)
            {
                return true;
            }

            return teleport.Confirm();
        }

        private bool ConfirmTeleportDestination()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            return teleport != null && teleport.Confirm();
        }

        private bool CancelTeleportDestination()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null)
            {
                return false;
            }

            bool cancelled = teleport.Cancel();
            if (!cancelled)
            {
                return false;
            }

            FocusSelectedWielderTile();
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Cancelled), interrupt: true));

            return true;
        }

        private void FocusSelectedWielderTile()
        {
            Vector2Int position;
            if (_adapter != null && _adapter.TryGetSelectedWielderPosition(out position))
            {
                FocusGridTile(position);
                return;
            }

            FocusGrid();
        }

        private string GetTeleportDestinationLabel()
        {
            TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
            if (teleport == null)
            {
                return string.Empty;
            }

            return ModText.Get(ModStrings.Spatial.DestinationAt, FormatTile(teleport.CurrentDestination));
        }

        private static string FormatTile(Vector2Int tile)
        {
            return "(" + tile.x + ", " + tile.y + ")";
        }

        private static bool IsTeleportSuppressedAction(string actionKey)
        {
            return IsTeleportSuppressedScreenAction(actionKey)
                || IsTeleportSuppressedFocusedAction(actionKey);
        }

        private static bool IsTeleportSuppressedScreenAction(string actionKey)
        {
            return actionKey == AccessibilityActions.FocusHudTroops.Key
                || actionKey == AccessibilityActions.FocusHudResources.Key
                || actionKey == AccessibilityActions.FocusHudObjectives.Key
                || actionKey == AccessibilityActions.FocusHudNotifications.Key;
        }

        private static bool IsTeleportSuppressedFocusedAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextWielder.Key
                || actionKey == AccessibilityActions.NextSettlement.Key
                || actionKey == AccessibilityActions.SummarizeReachableEntities.Key;
        }

        private bool CanFocusHudWidget(int index)
        {
            if (IsTeleportDestinationModeActive())
            {
                return false;
            }

            Widget widget = RootWidget != null ? RootWidget.GetChildAt(index) : null;
            return widget != null && widget.IsVisible;
        }

        private bool FocusHudWidget(int index)
        {
            return CanFocusHudWidget(index) && RootWidget != null && RootWidget.SetFocusByIndex(index);
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
            MenuWidget newMenu = BuildTroopSlotsMenu(_adapter.Hud, IsTeleportDestinationModeActive);
            if (!RootWidget.ReplaceChildAt(TroopSlotsIndex, newMenu))
            {
                return;
            }

            if (wasTroopMenuFocused && !newMenu.SetFocusByIndex(previousMenuIndex))
            {
                RootWidget.SetFocusByIndex(TroopSlotsIndex);
            }
        }

        private void AddTeleportDestinationModeWidgets(ContainerWidget root)
        {
            root.AddChild(new TextWidget(
                "adventure-teleport-instruction",
                () =>
                {
                    TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
                    return teleport != null ? teleport.InstructionText : string.Empty;
                },
                null,
                includeParentLabelInAnnouncement: false,
                tooltip: null,
                isVisible: IsTeleportDestinationModeActive));

            root.AddChild(new ButtonWidget(
                "adventure-teleport-previous",
                () =>
                {
                    TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
                    return teleport != null ? teleport.PreviousLabel : string.Empty;
                },
                SelectPreviousTeleportDestination,
                null,
                () => true,
                IsTeleportDestinationModeActive));

            root.AddChild(new TextWidget(
                "adventure-teleport-current-destination",
                GetTeleportDestinationLabel,
                null,
                includeParentLabelInAnnouncement: false,
                tooltip: null,
                isVisible: IsTeleportDestinationModeActive));

            root.AddChild(new ButtonWidget(
                "adventure-teleport-next",
                () =>
                {
                    TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
                    return teleport != null ? teleport.NextLabel : string.Empty;
                },
                SelectNextTeleportDestination,
                null,
                () => true,
                IsTeleportDestinationModeActive));

            root.AddChild(new ButtonWidget(
                "adventure-teleport-confirm",
                () =>
                {
                    TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
                    return teleport != null ? teleport.ConfirmLabel : string.Empty;
                },
                ConfirmTeleportDestination,
                null,
                () => true,
                IsTeleportDestinationModeActive));

            root.AddChild(new ButtonWidget(
                "adventure-teleport-cancel",
                () =>
                {
                    TeleportMenuAdapter teleport = GetTeleportMenuAdapter();
                    return teleport != null ? teleport.CancelLabel : string.Empty;
                },
                CancelTeleportDestination,
                null,
                () => true,
                IsTeleportDestinationModeActive));
        }

        private Func<bool> VisibleWhenNotTeleport(Func<bool> isVisible)
        {
            return () => !IsTeleportDestinationModeActive()
                && (isVisible == null || isVisible());
        }

        private static Func<bool> VisibleWhenNotTeleport(Func<bool> isTeleportDestinationModeActive, Func<bool> isVisible)
        {
            return () => (isTeleportDestinationModeActive == null || !isTeleportDestinationModeActive())
                && (isVisible == null || isVisible());
        }

        private ContainerWidget BuildRoot(AdventureMapAdapter adapter, AdventureMapGrid grid)
        {
            ContainerWidget root = new ContainerWidget("adventure_map_screen", ModText.Get(ModStrings.Screens.AdventureMap));
            root.AddChild(grid);
            AddTeleportDestinationModeWidgets(root);
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
                VisibleWhenNotTeleport(() => portrait.IsVisible)));

            root.AddChild(new TextWidget(
                "adventure-experience",
                () => adapter.Hud.ExperienceLabel,
                adapter.Hud.FocusExperience,
                false,
                () => adapter.Hud.ExperienceTooltip,
                VisibleWhenNotTeleport(adapter.Hud.IsExperienceVisible)));
            root.AddChild(new ButtonWidget(
                "adventure-level-up",
                () => adapter.Hud.LevelUpButtonLabel,
                adapter.Hud.ClickLevelUpButton,
                adapter.Hud.FocusLevelUpButton,
                adapter.Hud.IsLevelUpButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsLevelUpButtonVisible)));
            root.AddChild(BuildEssenceMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(new ButtonWidget(
                "adventure-inventory",
                () => adapter.Hud.InventoryButtonLabel,
                adapter.Hud.ClickInventoryButton,
                adapter.Hud.FocusInventoryButton,
                adapter.Hud.IsInventoryButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsInventoryButtonVisible),
                () => adapter.Hud.InventoryButtonTooltip));
            root.AddChild(new ButtonWidget(
                "adventure-move-to-destination",
                () => adapter.Hud.MoveToDestinationButtonLabel,
                adapter.Hud.ClickMoveToDestinationButton,
                adapter.Hud.FocusMoveToDestinationButton,
                adapter.Hud.IsMoveToDestinationButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsMoveToDestinationButtonVisible),
                () => adapter.Hud.MoveToDestinationButtonTooltip));
            root.AddChild(new ButtonWidget(
                "adventure-spellbook",
                () => adapter.Hud.SpellbookButtonLabel,
                adapter.Hud.ClickSpellbookButton,
                adapter.Hud.FocusSpellbookButton,
                adapter.Hud.IsSpellbookButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsSpellbookButtonVisible),
                () => adapter.Hud.SpellbookButtonTooltip));
            root.AddChild(BuildTroopSlotsMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(BuildResourcesMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(BuildObjectivesMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(BuildNotificationsMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(BuildTownListMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(BuildWielderListMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(new ButtonWidget(
                "adventure-chat",
                () => ChatPatches.CurrentAdapter != null ? ChatPatches.CurrentAdapter.ButtonLabel : ModText.Get(ModStrings.Screens.Chat),
                () => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.Open(),
                () => ChatPatches.CurrentAdapter?.FocusButton(),
                () => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.IsButtonEnabled(),
                VisibleWhenNotTeleport(() => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.IsButtonVisible()),
                () => ChatPatches.CurrentAdapter != null ? ChatPatches.CurrentAdapter.ButtonTooltip : null));
            root.AddChild(new ButtonWidget(
                "adventure-options",
                () => adapter.Hud.OptionsButtonLabel,
                adapter.Hud.ClickOptionsButton,
                adapter.Hud.FocusOptionsButton,
                adapter.Hud.IsOptionsButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsOptionsButtonVisible),
                () => adapter.Hud.OptionsButtonTooltip));
            root.AddChild(BuildKingdomOverviewMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(new ButtonWidget(
                "adventure-bug-report",
                () => adapter.Hud.BugReportButtonLabel,
                adapter.Hud.ClickBugReportButton,
                adapter.Hud.FocusBugReportButton,
                adapter.Hud.IsBugReportButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsBugReportButtonVisible),
                () => adapter.Hud.BugReportButtonTooltip));
            root.AddChild(BuildTeamQueueMenu(adapter.Hud, IsTeleportDestinationModeActive));
            root.AddChild(new ButtonWidget(
                "adventure-end-turn",
                () => adapter.Hud.EndTurnButtonLabel,
                adapter.Hud.ClickEndTurnButton,
                adapter.Hud.FocusEndTurnButton,
                adapter.Hud.IsEndTurnButtonEnabled,
                VisibleWhenNotTeleport(adapter.Hud.IsEndTurnButtonVisible),
                () => adapter.Hud.EndTurnButtonTooltip));
            root.AddChild(new TextWidget(
                "adventure-round",
                () => adapter.Hud.RoundTextLabel,
                null,
                false,
                (Tooltip)null,
                VisibleWhenNotTeleport(adapter.Hud.IsRoundTextVisible)));
            return root;
        }

        private static MenuWidget BuildEssenceMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-essence",
                GameText.Get("Common/CommanderInventory/Essences", string.Empty),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsEssenceMenuVisible));
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

        private static MenuWidget BuildTroopSlotsMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            return TroopHudMenu.Build(
                "adventure-troop-slots",
                GameText.Get("Commanders/Tooltip/Troops", string.Empty),
                adapter != null ? adapter.Troops : null,
                adapter != null ? VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsTroopMenuVisible) : (Func<bool>)null);
        }

        private static MenuWidget BuildResourcesMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-resources",
                ModText.Get(ModStrings.Screens.Resources),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsResourcesMenuVisible));
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

        private static MenuWidget BuildObjectivesMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-objectives",
                ModText.Get(ModStrings.Screens.Objectives),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsObjectivesMenuVisible));
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

        private static MenuWidget BuildNotificationsMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-notifications",
                ModText.Get(ModStrings.Screens.Notifications),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsNotificationsMenuVisible));
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

        private static MenuWidget BuildTownListMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-town-list",
                string.Empty,
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsTownListMenuVisible));
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

        private static MenuWidget BuildWielderListMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-wielder-list",
                ModText.Get(ModStrings.Screens.Wielders),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsWielderListMenuVisible));
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

        private static MenuWidget BuildKingdomOverviewMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-kingdom-overview",
                string.Empty,
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsKingdomOverviewMenuVisible));
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

        private static MenuWidget BuildTeamQueueMenu(AdventureHudAdapter adapter, Func<bool> isTeleportDestinationModeActive)
        {
            MenuWidget menu = new MenuWidget(
                "adventure-team-queue",
                ModText.Get(ModStrings.Screens.TurnOrder),
                VisibleWhenNotTeleport(isTeleportDestinationModeActive, adapter.IsTeamQueueMenuVisible));
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
                ISystemPopups systemPopups = TryResolve<ISystemPopups>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                AdventureMapRevealedRegistry revealedRegistry = GetAdventureMapRevealedRegistry();
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
                    inputManager,
                    systemPopups,
                    revealedRegistry);
                if (adapter.IsPresent())
                {
                    LogProbeDiagnostic("Adventure map probe found ready adventure map");
                    AdventureMapEventListener eventListener = new AdventureMapEventListener(
                        facade,
                        selectionHandler,
                        humanAdventureControllerFacade,
                        localizationHandler,
                        fogManager,
                        revealedRegistry);
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

        private static AdventureMapRevealedRegistry GetAdventureMapRevealedRegistry()
        {
            AdventureMapScannerState scannerState = SocAccessPlugin.Instance?.AdventureMapScannerState;
            return scannerState != null ? scannerState.RevealedRegistry : new AdventureMapRevealedRegistry();
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
            SocAccessPlugin.Instance?.LogInfo(message);
        }
    }
}
