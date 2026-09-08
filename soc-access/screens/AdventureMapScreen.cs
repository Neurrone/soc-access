using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.Menu;
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
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The adventure map: a MODE whose cursor is not the focus cursor, plus the HUD panels the game
    /// draws around it.
    ///
    /// THE MAP IS ONE NODE with a fixed identity, alone in the stop the game's map occupies. Its
    /// label is the tile the cursor stands on, its review buffer the tile's own tooltip, Enter and
    /// Backslash the tile's primary and secondary actions. <see cref="AdventureMapGrid"/> survives as
    /// the cursor and owns every key that walks it - the moves, the skips, the scanner, the
    /// bookmarks, the beacons, the sonar sweep - answered through <see cref="ModeClaims"/>, which the
    /// navigator asks BEFORE its own set while the map node is focused. Because the node's id never
    /// changes as the cursor moves, the navigator never announces a move; the grid says each landing
    /// itself, and the review buffer refills because the node's readout changed.
    ///
    /// THE HUD STOPS follow it in drawn order, and each exists only while the game draws its panel:
    /// the selected wielder, their army, the resources, the kingdom buttons, the wielder list, the
    /// town list, the objectives, the notifications, and the turn. There are no placeholders: a panel
    /// the game is not drawing is not declared, so Tab walks exactly what is on the screen. T, R, B
    /// and N jump to the troops, resources, objectives and notifications, claimed only while their
    /// panel is drawn.
    ///
    /// ESCAPE: on the map node it is the game's, which opens the pause menu. On a HUD stop the screen
    /// takes it and lands the cursor back on the map with the game's own close-menu noise. While the
    /// teleport menu is up it is the game's throughout, because <c>TeleportMenu.Show</c> registers
    /// <c>UI.ExitMenu</c> on its own Cancel outside any gamepad branch.
    ///
    /// TELEPORT: while the menu is up the map stop is named by the menu's instruction text, no HUD
    /// stop is built at all, and a stop of the menu's four buttons follows the map. Enter on the map
    /// confirms only while the cursor is on the destination the menu is showing; the secondary action
    /// is refused. Selecting a destination walks the cursor there and reads the tile. When the menu
    /// closes the cursor lands on the selected wielder, and a cancelled menu says so.
    ///
    /// Type-ahead is OFF (owner ruling 2026-09-08), which is what leaves the map's letter keys - W,
    /// S, A, D, P, L, K, J - free for the mode and the game's own hotkeys (C, V, E) alone.
    ///
    /// Measured on the "test" fixture 2026-09-08: the kingdom band draws four overview buttons (the
    /// player button is only drawn in a game with other players), and neither the chat button nor the
    /// bug report button is drawn, so both are declared at the end of the Kingdom stop UNVERIFIED
    /// against a drawn position. The town list had no town, so the Towns stop is built from the
    /// adapter and is likewise unverified.
    /// </summary>
    public sealed class AdventureMapScreen : LiveScreen<AdventureMapAdapter>
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

        /// <summary>The order the HUD draws the resource strip in, which is not the order the
        /// spoken summary reads them in.</summary>
        private static readonly ResourceType[] ResourceRowOrder =
        {
            ResourceType.Gold,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };

        private static readonly EssenceType[] EssenceRowOrder =
        {
            EssenceType.Order,
            EssenceType.Creation,
            EssenceType.Chaos,
            EssenceType.Arcana,
            EssenceType.Destruction
        };

        private const int ObjectiveSlots = 16;
        private const int NotificationSlots = 5;
        private const int TownSlots = 32;
        private const int WielderSlots = 32;
        private const int KingdomOverviewSlots = 5;
        private const int TeamQueueSlots = 16;

        private const string ReturnToGridSoundKey = "Common_ClosePauseMenu";

        private const string MapStop = "adventure-map:map";
        private const string WielderStop = "adventure-map:wielder";
        private const string TroopsStop = "adventure-map:troops";
        private const string ResourcesStop = "adventure-map:resources";
        private const string KingdomStop = "adventure-map:kingdom";
        private const string WieldersStop = "adventure-map:wielders";
        private const string TownsStop = "adventure-map:towns";
        private const string ObjectivesStop = "adventure-map:objectives";
        private const string NotificationsStop = "adventure-map:notifications";
        private const string TurnStop = "adventure-map:turn";
        private const string TeleportStop = "adventure-map:teleport";

        private const string TroopsKey = "adventure-map:army";
        private const string ObjectiveKeyPrefix = "adventure-map:objective:";
        private const string NotificationKeyPrefix = "adventure-map:notification:";
        private const string TownKeyPrefix = "adventure-map:town:";
        private const string WielderKeyPrefix = "adventure-map:wielder:";
        private const string KingdomKeyPrefix = "adventure-map:kingdom:";
        private const string TeamQueueKeyPrefix = "adventure-map:turn-order:";

        /// <summary>The one node the whole map is. Fixed, so walking the cursor is never a move as
        /// far as the navigator is concerned.</summary>
        public static readonly ControlId MapNodeId = ControlId.Structural("adventure-map:tile");

        private static string _lastProbeDiagnostic;

        private AdventureMapEventListener _eventListener;

        // The tile cursor, built over the adapter it walks. Rebuilt when the slot is pointed at a
        // DIFFERENT adventure and kept otherwise, so a dialog covering the map does not move the
        // cursor: the map is only deactivated by the story gap and the loading screen, and it keeps
        // its state across both.
        private AdventureMapGrid _grid;
        private AdventureMapAdapter _gridAdapter;
        private TeleportMenuAdapter _teleportMenuAdapter;
        private bool _isTopScreen;

        // The tile tooltip is expensive to compose (the game's whole details capture) and the graph
        // is rebuilt every frame, so it is composed once per tile, which is exactly as often as the
        // widget engine's focus commit composed it.
        private Vector2Int _tooltipTile;
        private Tooltip _tooltip;
        private bool _tooltipRead;

        // The tile itself is the same story: reading one runs a reachability fill, a loop over every
        // commander, a route preview and two shortest-path queries, and the focused node's label
        // resolves it about twice a frame. It is read once per tile and kept until the cursor moves
        // or the map changes under it, which is what the event listener's hook below reports.
        private Vector2Int _tileTile;
        private AdventureMapTile _tile;
        private bool _tileRead;

        /// <summary>After a hot reload: point the slot at the adventure already installed.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<AdventureMapScreen>(FindActive());
        }

        /// <summary>The adventure the game has installed and made ready, or null. The one scan.
        /// </summary>
        public static AdventureMapAdapter FindActive()
        {
            return FindActiveAdventureMap();
        }

        /// <summary>The cursor is built over one adventure: a new one gets a new grid, and the audio
        /// and overlay of the old one are let go with it.</summary>
        private AdventureMapGrid Grid()
        {
            if (_grid != null && ReferenceEquals(_gridAdapter, Live))
            {
                return _grid;
            }

            if (_grid != null)
            {
                _grid.HideOverlay();
                _grid.DisposeAudio();
            }

            _gridAdapter = Live;
            _grid = Live == null ? null : new AdventureMapGrid(Live);
            InvalidateTile();
            return _grid;
        }

        public override string Key
        {
            get { return "adventure-map"; }
        }

        /// <summary>Layer 10: the world, under everything drawn on it.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.AdventureMap); }
        }

        /// <summary>Off, so no letter is ever taken for a search: the mod's own map letters and the
        /// game's own hotkeys share the keyboard here.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        /// <summary>The adventure view installed and ready, and neither of the two things that take
        /// the map AWAY: a story sequence running (the camera and the keyboard are the story's) and
        /// the loading screen. A popup does NOT deactivate the map - it covers it by layer, so the
        /// HUD stays drawn underneath and the event listener and its audio are not torn down and
        /// rebuilt for every dialog.</summary>
        public override bool IsActive()
        {
            if (Live == null || !Live.IsPresent())
            {
                return false;
            }

            ScreenDetector detector = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ScreenDetector;
            if (detector != null && detector.StorySequenceActive)
            {
                return false;
            }

            ScreenManager screens = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ScreenManager;
            LoadingCompleteScreen loading = screens == null ? null : screens.Registered<LoadingCompleteScreen>();
            return loading == null || loading.Live == null;
        }

        /// <summary>The cursor survives the story gap and the loading screen, which are the only two
        /// things that take the map off the stack.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        public override IEnumerable<ReviewBufferKind> VisibleReviewBuffers
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

        /// <summary>The map's events are listened to for exactly as long as the map is up. Built here
        /// rather than held forever, because the listener is bound to the adventure the slot points
        /// at.</summary>
        public override void OnPush()
        {
            _eventListener = Live == null
                ? null
                : new AdventureMapEventListener(
                    Live.Facade,
                    Live.SelectionHandler,
                    Live.HumanAdventureControllerFacade,
                    Live.LocalizationHandler,
                    Live.FogManager,
                    GetAdventureMapRevealedRegistry());
            if (_eventListener != null)
            {
                _eventListener.OnMapChanged = InvalidateTile;
            }

            _eventListener?.Attach();
            AccessibilityEventBus.Subscribe(HandleAccessibilityEvent);
        }

        public override void OnFocus()
        {
            _isTopScreen = true;
            base.OnFocus();
            Grid()?.SetBeaconAudible(true);
        }

        public override void OnUnfocus()
        {
            _isTopScreen = false;
            Grid()?.SetBeaconAudible(false);
            Grid()?.HideOverlay();
            base.OnUnfocus();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            _isTopScreen = false;
            Grid()?.DisposeAudio();
            _eventListener?.Detach();
            _eventListener = null;
            Grid()?.HideOverlay();
            base.OnPop();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            _eventListener?.Update();
        }

        // ---- the graph ----

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildMap(builder);

            // The teleport menu takes the whole screen over: the HUD is not workable under it and is
            // not declared, so Tab walks the map and the menu's four buttons and nothing else.
            if (TeleportMenu != null)
            {
                BuildTeleport(builder);
                return;
            }

            AdventureHudAdapter hud = Live.Hud;
            if (hud == null)
            {
                return;
            }

            BuildWielder(builder, hud);
            BuildTroops(builder, hud);
            BuildResources(builder, hud);
            BuildKingdom(builder, hud);
            BuildWielders(builder, hud);
            BuildTowns(builder, hud);
            BuildObjectives(builder, hud);
            BuildNotifications(builder, hud);
            BuildTurn(builder, hud);
        }

        /// <summary>The map itself: one node, named by the tile under the cursor, in a stop the
        /// teleport menu renames to its own instruction while it is up.</summary>
        private void BuildMap(GraphBuilder builder)
        {
            builder.BeginStop(MapStop);
            builder.PushContext(MapContext());

            NodeVtable vtable = GraphNodes.Text(() => AdventureMapGrid.Describe(CursorTile()), null, TileTooltip());
            vtable.OnActivate = ActivateTile;
            vtable.OnContextual = ContextualTile;
            vtable.OnFocusVisual = () => Grid()?.ShowOverlay();
            vtable.OnBlurVisual = () => Grid()?.HideOverlay();
            // While the teleport menu is up the stop already carries the game's instruction and Enter
            // means confirm, so the tile's own click hints say nothing then.
            TileInstructionHints.Add(vtable, TileTooltip, () => TeleportMenu == null);
            builder.AddItem(new SyntheticNode(MapNodeId, vtable));

            builder.PopContext();
        }

        private string MapContext()
        {
            TeleportMenuAdapter teleport = TeleportMenu;
            string instruction = teleport != null ? teleport.InstructionText : null;
            return string.IsNullOrWhiteSpace(instruction) ? ModText.Get(ModStrings.Screens.Map) : instruction;
        }

        /// <summary>The tile under the cursor, read once and kept until the cursor moves or the map
        /// changes, the same shape as <see cref="TileTooltip"/> below.</summary>
        private AdventureMapTile CursorTile()
        {
            Vector2Int tile = Grid().CursorTile;
            if (_tileRead && tile == _tileTile)
            {
                return _tile;
            }

            _tileTile = tile;
            _tileRead = true;
            _tile = Live.GetTile(tile);
            return _tile;
        }

        /// <summary>Every change the map listens to - a selection, a move, a teleport, a command, a
        /// spawned entity, the fog - drops the kept tile, so nothing the game changes under a still
        /// cursor is read from the cache.</summary>
        private void InvalidateTile()
        {
            _tileRead = false;
            _tile = null;
        }

        private Tooltip TileTooltip()
        {
            Vector2Int tile = Grid().CursorTile;
            if (_tooltipRead && tile == _tooltipTile)
            {
                return _tooltip;
            }

            _tooltipTile = tile;
            _tooltipRead = true;
            _tooltip = Live.GetTooltip(tile);
            return _tooltip;
        }

        /// <summary>Enter on the map. While the teleport menu is up it confirms the destination and
        /// only while the cursor is standing on it; anywhere else it is refused in silence, as it is
        /// today.</summary>
        private void ActivateTile()
        {
            TeleportMenuAdapter teleport = TeleportMenu;
            if (teleport != null)
            {
                if (Grid().CursorTile == teleport.CurrentDestination)
                {
                    teleport.Confirm();
                }

                return;
            }

            Live.HandlePrimaryAction(Grid().CursorTile);
        }

        private void ContextualTile()
        {
            if (TeleportMenu != null)
            {
                return;
            }

            Live.HandleSecondaryAction(Grid().CursorTile);
        }

        // ---- the wielder band ----

        /// <summary>The selected wielder: their portrait, their experience, the level-up button while
        /// the game draws it, their essences, and the three buttons that open their pages.</summary>
        private void BuildWielder(GraphBuilder builder, AdventureHudAdapter hud)
        {
            CommanderHudPortraitAdapter portrait = hud.SelectedWielderPortrait;
            bool portraitDrawn = portrait != null && portrait.IsVisible;
            bool experienceDrawn = hud.IsExperienceVisible();
            bool essencesDrawn = hud.IsEssenceMenuVisible();
            bool buttonsDrawn = hud.IsInventoryButtonVisible()
                || hud.IsMoveToDestinationButtonVisible()
                || hud.IsSpellbookButtonVisible();
            if (!portraitDrawn && !experienceDrawn && !essencesDrawn && !buttonsDrawn)
            {
                return;
            }

            builder.BeginStop(WielderStop);
            string name = portraitDrawn ? portrait.Name : null;
            bool named = !string.IsNullOrWhiteSpace(name);
            if (named)
            {
                builder.PushContext(name);
            }

            if (portraitDrawn)
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => portrait.Name,
                    () => portrait.Click(),
                    () => portrait.IsEnabled,
                    PortraitTooltip(portrait));
                vtable.OnFocusVisual = () => portrait.Focus();
                Component drawnBy = portrait.TooltipTarget;
                builder.AddItem(drawnBy != null
                    ? (NodeDeclaration)new DrawnNode(ControlId.For(drawnBy, "adventure-map:portrait"), vtable, drawnBy)
                    : new SyntheticNode(ControlId.Structural("adventure-map:portrait"), vtable));
            }

            if (experienceDrawn)
            {
                NodeVtable vtable = GraphNodes.Text(() => hud.ExperienceLabel, null, hud.ExperienceTooltip);
                vtable.OnFocusVisual = hud.FocusExperience;
                builder.AddItem(new SyntheticNode(ControlId.Structural("adventure-map:experience"), vtable));
            }

            if (hud.IsLevelUpButtonVisible())
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => hud.LevelUpButtonLabel,
                    () => hud.ClickLevelUpButton(),
                    hud.IsLevelUpButtonEnabled);
                vtable.OnFocusVisual = hud.FocusLevelUpButton;
                builder.AddItem(new SyntheticNode(ControlId.Structural("adventure-map:level-up"), vtable));
            }

            BuildEssences(builder, hud, essencesDrawn);

            AddHudButton(
                builder,
                "adventure-map:wielder-sheet",
                hud.IsInventoryButtonVisible(),
                () => hud.InventoryButtonLabel,
                () => hud.ClickInventoryButton(),
                hud.IsInventoryButtonEnabled,
                hud.InventoryButtonTooltip,
                hud.FocusInventoryButton);
            AddHudButton(
                builder,
                "adventure-map:movement",
                hud.IsMoveToDestinationButtonVisible(),
                () => hud.MoveToDestinationButtonLabel,
                () => hud.ClickMoveToDestinationButton(),
                hud.IsMoveToDestinationButtonEnabled,
                hud.MoveToDestinationButtonTooltip,
                hud.FocusMoveToDestinationButton);
            AddHudButton(
                builder,
                "adventure-map:spellbook",
                hud.IsSpellbookButtonVisible(),
                () => hud.SpellbookButtonLabel,
                () => hud.ClickSpellbookButton(),
                hud.IsSpellbookButtonEnabled,
                hud.SpellbookButtonTooltip,
                hud.FocusSpellbookButton);

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The wielder's stats as the game draws them on the portrait, recomposed through the
        /// game's own hover refresh before they are read.</summary>
        private static Tooltip PortraitTooltip(CommanderHudPortraitAdapter portrait)
        {
            Component target = portrait.TooltipTarget;
            if (target == null || portrait.Localization == null)
            {
                return null;
            }

            return new Tooltip(
                () =>
                {
                    portrait.RefreshTooltip();
                    return NativeTooltipUtility.GetTooltipLinesForComponent(target, portrait.Localization);
                },
                VisualTooltipMetadata.ForComponent(target));
        }

        private static void BuildEssences(GraphBuilder builder, AdventureHudAdapter hud, bool drawn)
        {
            if (!drawn)
            {
                return;
            }

            string caption = GameText.Get("Common/CommanderInventory/Essences", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
            }

            builder.SetRegion("adventure-map:essences");
            for (int i = 0; i < EssenceRowOrder.Length; i++)
            {
                EssenceType essence = EssenceRowOrder[i];
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("adventure-map:essence:" + essence),
                    Focused(
                        GraphNodes.Text(() => hud.GetEssenceLabel(essence), null, hud.GetEssenceTooltip(essence)),
                        () => hud.FocusEssence(essence))));
            }

            builder.SetRegion(null);
            if (named)
            {
                builder.PopContext();
            }
        }

        // ---- the army ----

        private static void BuildTroops(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsTroopMenuVisible() || hud.Troops == null)
            {
                return;
            }

            builder.BeginStop(TroopsStop);
            string caption = GameText.Get("Commanders/Tooltip/Troops", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
            }

            TroopHudRows.Rows(builder, hud.Troops, TroopHudRows.RowPrefix(TroopsKey));

            if (named)
            {
                builder.PopContext();
            }
        }

        // ---- the resource strip ----

        private static void BuildResources(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsResourcesMenuVisible())
            {
                return;
            }

            builder.BeginStop(ResourcesStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Resources));
            for (int i = 0; i < ResourceRowOrder.Length; i++)
            {
                ResourceType resource = ResourceRowOrder[i];
                builder.AddItem(new SyntheticNode(
                    ResourceNodeId(resource),
                    Focused(
                        GraphNodes.Text(() => hud.GetResourceLabel(resource), null, hud.GetResourceTooltip(resource)),
                        () => hud.FocusResource(resource))));
            }

            builder.PopContext();
        }

        private static ControlId ResourceNodeId(ResourceType resource)
        {
            return ControlId.Structural("adventure-map:resource:" + resource);
        }

        // ---- the kingdom band ----

        /// <summary>The game menu and the overview buttons beside it, plus the chat and bug report
        /// buttons where a session draws them.</summary>
        private static void BuildKingdom(GraphBuilder builder, AdventureHudAdapter hud)
        {
            bool optionsDrawn = hud.IsOptionsButtonVisible();
            bool overviewDrawn = hud.IsKingdomOverviewMenuVisible();
            ChatAdapter chat = ChatPatches.CurrentAdapter;
            bool chatDrawn = chat != null && chat.IsButtonVisible();
            bool bugReportDrawn = hud.IsBugReportButtonVisible();
            if (!optionsDrawn && !overviewDrawn && !chatDrawn && !bugReportDrawn)
            {
                return;
            }

            builder.BeginStop(KingdomStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Kingdom));

            AddHudButton(
                builder,
                "adventure-map:game-menu",
                optionsDrawn,
                () => hud.OptionsButtonLabel,
                () => hud.ClickOptionsButton(),
                hud.IsOptionsButtonEnabled,
                hud.OptionsButtonTooltip,
                hud.FocusOptionsButton);

            for (int i = 0; overviewDrawn && i < KingdomOverviewSlots; i++)
            {
                int index = i;
                AddHudButton(
                    builder,
                    KingdomKeyPrefix + index,
                    hud.IsKingdomOverviewItemVisible(index),
                    () => hud.GetKingdomOverviewLabel(index),
                    () => hud.ClickKingdomOverviewItem(index),
                    () => hud.IsKingdomOverviewItemEnabled(index),
                    hud.GetKingdomOverviewTooltip(index),
                    () => hud.FocusKingdomOverviewItem(index));
            }

            AddHudButton(
                builder,
                "adventure-map:chat",
                chatDrawn,
                () => chat.ButtonLabel,
                () => chat.Open(),
                () => chat.IsButtonEnabled(),
                chatDrawn ? chat.ButtonTooltip : null,
                () => chat.FocusButton());
            AddHudButton(
                builder,
                "adventure-map:bug-report",
                bugReportDrawn,
                () => hud.BugReportButtonLabel,
                () => hud.ClickBugReportButton(),
                hud.IsBugReportButtonEnabled,
                hud.BugReportButtonTooltip,
                hud.FocusBugReportButton);

            builder.PopContext();
        }

        // ---- the wielder list ----

        private static void BuildWielders(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsWielderListMenuVisible())
            {
                return;
            }

            builder.BeginStop(WieldersStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Wielders));

            if (hud.IsWielderAmountVisible())
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("adventure-map:wielder-amount"),
                    GraphNodes.Text(() => hud.WielderAmountLabel, null, hud.WielderAmountTooltip)));
            }

            for (int i = 0; i < WielderSlots; i++)
            {
                int index = i;
                AddHudButton(
                    builder,
                    WielderKeyPrefix + index,
                    hud.IsWielderListEntryVisible(index),
                    () => hud.GetWielderListEntryLabel(index),
                    () => hud.ClickWielderListEntry(index),
                    null,
                    hud.GetWielderListEntryTooltip(index),
                    () => hud.FocusWielderListEntry(index));
            }

            builder.PopContext();
        }

        // ---- the town list ----

        private static void BuildTowns(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsTownListMenuVisible())
            {
                return;
            }

            builder.BeginStop(TownsStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Towns));
            for (int i = 0; i < TownSlots; i++)
            {
                int index = i;
                AddHudButton(
                    builder,
                    TownKeyPrefix + index,
                    hud.IsTownListEntryVisible(index),
                    () => hud.GetTownListEntryLabel(index),
                    () => hud.ClickTownListEntry(index),
                    null,
                    hud.GetTownListEntryTooltip(index),
                    () => hud.FocusTownListEntry(index));
            }

            builder.PopContext();
        }

        // ---- the objectives ----

        private static void BuildObjectives(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsObjectivesMenuVisible())
            {
                return;
            }

            builder.BeginStop(ObjectivesStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Objectives));
            for (int i = 0; i < ObjectiveSlots; i++)
            {
                int index = i;
                if (!hud.IsObjectiveVisible(index))
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Text(
                    () => hud.GetObjectiveLabel(index),
                    null,
                    hud.GetObjectiveTooltip(index));
                vtable.OnFocusVisual = () => hud.FocusObjective(index);
                vtable.OnBlurVisual = hud.UnfocusObjective;
                builder.AddItem(new SyntheticNode(ControlId.Structural(ObjectiveKeyPrefix + index), vtable));
            }

            builder.PopContext();
        }

        // ---- the notifications ----

        /// <summary>Each notification the HUD is showing. Enter is the game's click on it; Backslash
        /// is the right click the game answers by dismissing it, offered as a usage hint.</summary>
        private static void BuildNotifications(GraphBuilder builder, AdventureHudAdapter hud)
        {
            if (!hud.IsNotificationsMenuVisible())
            {
                return;
            }

            builder.BeginStop(NotificationsStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Notifications));
            for (int i = 0; i < NotificationSlots; i++)
            {
                int index = i;
                if (!hud.IsNotificationVisible(index))
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Button(
                    () => hud.GetNotificationLabel(index),
                    () => hud.ClickNotification(index),
                    null,
                    hud.GetNotificationTooltip(index));
                vtable.OnFocusVisual = () => hud.FocusNotification(index);
                vtable.OnContextual = () => hud.DismissNotification(index);
                NodeHints.Add(
                    vtable,
                    ModStrings.Screens.NotificationDismissHint,
                    AccessibilityActions.UiRightClick.Key);
                builder.AddItem(new SyntheticNode(ControlId.Structural(NotificationKeyPrefix + index), vtable));
            }

            builder.PopContext();
        }

        // ---- the turn ----

        /// <summary>The round the game is on, the End Turn button - which Tab lands on, because it is
        /// the one thing anybody comes to this corner to press - and the team queue beside it.
        /// </summary>
        private static void BuildTurn(GraphBuilder builder, AdventureHudAdapter hud)
        {
            bool roundDrawn = hud.IsRoundTextVisible();
            bool endTurnDrawn = hud.IsEndTurnButtonVisible();
            bool queueDrawn = hud.IsTeamQueueMenuVisible();
            if (!roundDrawn && !endTurnDrawn && !queueDrawn)
            {
                return;
            }

            builder.BeginStop(TurnStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.TurnOrder));

            if (roundDrawn)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("adventure-map:round"),
                    GraphNodes.Text(() => hud.RoundTextLabel)));
            }

            if (endTurnDrawn)
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => hud.EndTurnButtonLabel,
                    () => hud.ClickEndTurnButton(),
                    hud.IsEndTurnButtonEnabled,
                    hud.EndTurnButtonTooltip);
                vtable.OnFocusVisual = hud.FocusEndTurnButton;
                builder.AddItem(new SyntheticNode(EndTurnNodeId, vtable));
                builder.LandStopOn(EndTurnNodeId);
            }

            if (queueDrawn)
            {
                builder.SetRegion("adventure-map:turn-order");
                for (int i = 0; i < TeamQueueSlots; i++)
                {
                    int index = i;
                    if (!hud.IsTeamQueueEntryVisible(index))
                    {
                        continue;
                    }

                    builder.AddItem(new SyntheticNode(
                        ControlId.Structural(TeamQueueKeyPrefix + index),
                        Focused(
                            GraphNodes.Text(() => hud.GetTeamQueueEntryLabel(index), null, hud.GetTeamQueueEntryTooltip(index)),
                            () => hud.FocusTeamQueueEntry(index))));
                }

                builder.SetRegion(null);
            }

            builder.PopContext();
        }

        private static ControlId EndTurnNodeId
        {
            get { return ControlId.Structural("adventure-map:end-turn"); }
        }

        // ---- the teleport menu ----

        private void BuildTeleport(GraphBuilder builder)
        {
            TeleportMenuAdapter teleport = TeleportMenu;
            if (teleport == null)
            {
                return;
            }

            builder.BeginStop(TeleportStop);
            AddTeleportButton(builder, "previous", () => teleport.PreviousLabel, SelectPreviousTeleportDestination);
            AddTeleportButton(builder, "next", () => teleport.NextLabel, SelectNextTeleportDestination);
            AddTeleportButton(builder, "confirm", () => teleport.ConfirmLabel, () => teleport.Confirm());
            AddTeleportButton(builder, "cancel", () => teleport.CancelLabel, () => teleport.Cancel());
        }

        private static void AddTeleportButton(GraphBuilder builder, string key, Func<string> label, Action activate)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.Structural("adventure-map:teleport:" + key),
                GraphNodes.Button(label, activate)));
        }

        // ---- shared node plumbing ----

        private static void AddHudButton(
            GraphBuilder builder,
            string key,
            bool drawn,
            Func<string> label,
            Func<bool> activate,
            Func<bool> enabled,
            Tooltip tooltip,
            Action focus)
        {
            if (!drawn)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(label, () => activate(), enabled, tooltip);
            vtable.OnFocusVisual = focus;
            builder.AddItem(new SyntheticNode(ControlId.Structural(key), vtable));
        }

        /// <summary>Selecting the game's own control is what makes it draw the details the mod then
        /// reads, so every HUD line focuses its control.</summary>
        private static NodeVtable Focused(NodeVtable vtable, Action focus)
        {
            vtable.OnFocusVisual = focus;
            return vtable;
        }

        // ---- keys ----

        /// <summary>
        /// The tile cursor's whole key set, while the map node is the one the cursor is on. Asked
        /// BEFORE the navigator's own set, which is what makes the arrows walk the map rather than the
        /// tree; on any other stop none of it is claimed and the arrows are the tree's again.
        /// </summary>
        public override bool ModeClaims(string actionKey)
        {
            return IsMapFocused() && Grid() != null && ModeAction(actionKey) != null;
        }

        /// <summary>
        /// The map action a key means here, or null where the key is not the cursor's. The mod's own
        /// map keys answer for themselves; the graph's navigation keys are TRANSLATED onto them, so
        /// Up walks a tile north whichever of the two actions the input layer resolves the arrow to -
        /// and so the dev server's injections behave exactly as the physical keys do.
        /// </summary>
        private string ModeAction(string actionKey)
        {
            if (Grid().ClaimsAction(actionKey))
            {
                return actionKey;
            }

            if (actionKey == AccessibilityActions.UiUp.Key)
            {
                return AccessibilityActions.MapMoveNorth.Key;
            }

            if (actionKey == AccessibilityActions.UiDown.Key)
            {
                return AccessibilityActions.MapMoveSouth.Key;
            }

            if (actionKey == AccessibilityActions.UiLeft.Key)
            {
                return AccessibilityActions.MapMoveWest.Key;
            }

            if (actionKey == AccessibilityActions.UiRight.Key)
            {
                return AccessibilityActions.MapMoveEast.Key;
            }

            if (actionKey == AccessibilityActions.UiCoarseDecrease.Key)
            {
                return AccessibilityActions.MapSkipWest.Key;
            }

            if (actionKey == AccessibilityActions.UiCoarseIncrease.Key)
            {
                return AccessibilityActions.MapSkipEast.Key;
            }

            if (actionKey == AccessibilityActions.UiHome.Key)
            {
                return AccessibilityActions.ScannerJumpToResult.Key;
            }

            if (actionKey == AccessibilityActions.UiEnd.Key)
            {
                return AccessibilityActions.ScannerSpeakDistanceAndDirection.Key;
            }

            if (actionKey == AccessibilityActions.UiClearSearch.Key)
            {
                return AccessibilityActions.ScannerReturnFromJump.Key;
            }

            return null;
        }

        /// <summary>The keys that are the SCREEN's rather than the mode's, asked after the navigator's
        /// own set: the four HUD hotkeys, and the game's quick splits on a troop row.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return HotkeyLanding(actionKey) != null
                || TroopHudRows.ClaimsAction(actionKey, Navigator, Troops, TroopsKey);
        }

        public override bool OnAction(string actionKey)
        {
            if (IsMapFocused() && Grid() != null)
            {
                string mapAction = ModeAction(actionKey);
                if (mapAction != null)
                {
                    return Grid().HandleAction(AccessibilityActions.FindByKey(mapAction));
                }
            }

            ControlId landing = HotkeyLanding(actionKey);
            if (landing != null)
            {
                Navigator.FocusNode(landing);
                return true;
            }

            return TroopHudRows.OnAction(actionKey, Navigator, Troops, TroopsKey);
        }

        /// <summary>Escape belongs to the screen only where the cursor has left the map: it lands back
        /// on the map. On the map it is the game's, which is what opens the pause menu, and while the
        /// teleport menu is up it is the menu's own Cancel.</summary>
        public override bool ConsumesBack
        {
            get { return TeleportMenu == null && !IsMapFocused(); }
        }

        public override bool Back()
        {
            if (!ConsumesBack)
            {
                return false;
            }

            Navigator.FocusNode(MapNodeId);
            NativeSoundUtility.PostEvent(ReturnToGridSoundKey);
            return true;
        }

        /// <summary>Where T, R, B and N land - the first line of the panel each names, and null while
        /// that panel is not drawn or the teleport menu has the screen.</summary>
        private ControlId HotkeyLanding(string actionKey)
        {
            AdventureHudAdapter hud = Live != null ? Live.Hud : null;
            if (hud == null || TeleportMenu != null)
            {
                return null;
            }

            if (actionKey == AccessibilityActions.FocusHudTroops.Key)
            {
                return hud.IsTroopMenuVisible() && hud.Troops != null && hud.Troops.GetSlots().Count > 0
                    ? ControlId.Structural(TroopHudRows.RowPrefix(TroopsKey) + "0")
                    : null;
            }

            if (actionKey == AccessibilityActions.FocusHudResources.Key)
            {
                return hud.IsResourcesMenuVisible() ? ResourceNodeId(ResourceRowOrder[0]) : null;
            }

            if (actionKey == AccessibilityActions.FocusHudObjectives.Key)
            {
                return hud.IsObjectivesMenuVisible()
                    ? FirstDrawn(ObjectiveKeyPrefix, ObjectiveSlots, hud.IsObjectiveVisible)
                    : null;
            }

            if (actionKey == AccessibilityActions.FocusHudNotifications.Key)
            {
                return hud.IsNotificationsMenuVisible()
                    ? FirstDrawn(NotificationKeyPrefix, NotificationSlots, hud.IsNotificationVisible)
                    : null;
            }

            return null;
        }

        private static ControlId FirstDrawn(string prefix, int slots, Func<int, bool> drawn)
        {
            for (int i = 0; i < slots; i++)
            {
                if (drawn(i))
                {
                    return ControlId.Structural(prefix + i);
                }
            }

            return null;
        }

        private bool IsMapFocused()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null
                && ReferenceEquals(navigator.Screen, this)
                && MapNodeId.Equals(navigator.FocusedKey);
        }

        private TroopHudAdapter Troops
        {
            get
            {
                AdventureHudAdapter hud = Live != null ? Live.Hud : null;
                return hud != null ? hud.Troops : null;
            }
        }

        // ---- the teleport mode ----

        public void EnterTeleportDestinationMode(TeleportMenuAdapter adapter)
        {
            if (adapter == null || !adapter.IsPresent())
            {
                return;
            }

            _teleportMenuAdapter = adapter;
            string instruction = adapter.InstructionText;
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                SpeechPipeline.Output(new SpeechRequest(instruction, interrupt: true));
            }

            Navigator?.FocusNode(MapNodeId, announce: false);
            Grid().FocusTile(adapter.CurrentDestination);
        }

        /// <summary>The menu has closed. The cursor comes back to the wielder it was teleporting, and
        /// a menu the player cancelled says so.</summary>
        public void ExitTeleportDestinationMode(TeleportMenu menu, bool cancelled)
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
            if (cancelled)
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Cancelled), interrupt: true));
            }

            Vector2Int position;
            if (Live != null && Live.TryGetSelectedWielderPosition(out position))
            {
                Grid().FocusTileSilently(position);
            }

            Navigator?.FocusNode(MapNodeId);
        }

        public bool MatchesTeleportMenu(TeleportMenu menu)
        {
            return _teleportMenuAdapter != null
                && (menu == null || ReferenceEquals(_teleportMenuAdapter.SourceKey, menu));
        }

        private TeleportMenuAdapter TeleportMenu
        {
            get
            {
                return _teleportMenuAdapter != null && _teleportMenuAdapter.IsPresent()
                    ? _teleportMenuAdapter
                    : null;
            }
        }

        private void SelectPreviousTeleportDestination()
        {
            SelectTeleportDestination(teleport => teleport.SelectPrevious());
        }

        private void SelectNextTeleportDestination()
        {
            SelectTeleportDestination(teleport => teleport.SelectNext());
        }

        /// <summary>Stepping the menu's destinations walks the tile cursor with them and reads where
        /// it landed, since the destination is a place on the map and nowhere else.</summary>
        private void SelectTeleportDestination(Func<TeleportMenuAdapter, bool> select)
        {
            TeleportMenuAdapter teleport = TeleportMenu;
            if (teleport == null || !select(teleport))
            {
                return;
            }

            Grid().FocusTile(teleport.CurrentDestination);
        }

        // ---- the map moving on its own ----

        private void HandleAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null)
            {
                if (_isTopScreen && !hudVisibility.IsVisible)
                {
                    MoveCursor(Grid().CursorTile, announce: false);
                }

                return;
            }

            MapCameraFocusEvent cameraFocus = accessibilityEvent as MapCameraFocusEvent;
            if (cameraFocus != null)
            {
                MoveCursor(cameraFocus.Tile, announce: true);
                return;
            }

            MapWielderTeleportedEvent teleport = accessibilityEvent as MapWielderTeleportedEvent;
            if (teleport != null)
            {
                MoveCursor(teleport.Tile, announce: true);
            }
        }

        /// <summary>The one way the game moves the cursor. It is read out only where the player is
        /// actually standing on the map and looking at it; anywhere else the cursor is moved without a
        /// word and focus is put back on the map without announcing that either.</summary>
        private void MoveCursor(Vector2Int tile, bool announce)
        {
            if (announce && _isTopScreen && IsMapFocused())
            {
                Grid().FocusTile(tile);
                return;
            }

            Grid().FocusTileSilently(tile);
            Navigator?.FocusNode(MapNodeId, announce: false);
        }

        // ---- the spoken resource summary (Ctrl+R, from the screen manager) ----

        public bool SummarizeResources()
        {
            AdventureHudAdapter hud = Live != null ? Live.Hud : null;
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

        // ---- the runtime probe (the reload resync) ----

        private static AdventureMapAdapter FindActiveAdventureMap()
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
                    return adapter;
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
            AdventureMapScannerState scannerState = SocAccessMod.Instance?.AdventureMapScannerState;
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
            SocAccessMod.Instance?.LogInfo(message);
        }
    }
}
