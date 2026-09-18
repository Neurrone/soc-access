using System;
using System.Collections.Generic;
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

        // The tile cursor, built over the adapter it walks. Rebuilt when the slot is pointed at a
        // DIFFERENT adventure and kept otherwise, so a dialog covering the map does not move the
        // cursor: the map is only deactivated by the story gap and the loading screen, and it keeps
        // its state across both.
        private AdventureMapGrid _grid;
        private AdventureMapAdapter _gridAdapter;

        // Whether the map is the screen the player is on rather than one under a menu. The baseline
        // for the HUD announcements: a container the game hides while a menu covers the map is not
        // news, and saying so would talk over the menu the player opened.
        private bool _isTopScreen;

        // THE TELEPORT MODE IS DERIVED, NOT REMEMBERED: the map reads the teleport menu from the
        // adventure scene's container every frame, and the menu being drawn IS the mode
        // (AGENTS.md, "Screen Resolution"). The only thing kept is whether the mod has already said
        // the mode was entered, so arrival and departure are each announced once.
        private readonly AdaptedSource<TeleportMenu, TeleportMenuAdapter> _teleportSource =
            new AdaptedSource<TeleportMenu, TeleportMenuAdapter>(
                ScreenSource<TeleportMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new TeleportMenuAdapter(menu));

        // Whether the mod has already said the mode was entered, so arrival and departure are each
        // announced once. The mode itself is read off the menu above, never remembered.
        private bool _inTeleportMode;

        // The tile tooltip is expensive to compose (the game's whole details capture) and the graph
        // is rebuilt every frame, so it is composed once per tile, which is exactly as often as the
        // widget engine's focus commit composed it.
        private Vector2Int _tooltipTile;
        private Tooltip _tooltip;
        private bool _tooltipRead;
        private ILanguageDefinition _tooltipLanguage;

        // The tile itself is the same story: reading one still runs the game's shortest-path query
        // for an interactable entity standing on it, on top of everything the adapter reads about
        // the tile, and the focused node's label resolves it about twice a frame. It is read once
        // per tile and kept until the cursor moves or the map changes under it, which is what the
        // event listener's hook below reports.
        private Vector2Int _tileTile;
        private AdventureMapTile _tile;
        private bool _tileRead;
        private ILanguageDefinition _tileLanguage;

        // THE ADVENTURE FINDS ITSELF: the view installer is a component on the adventure scene's
        // SceneContext object, and its container is where everything the map reads is bound, so the
        // installer IS the adventure as far as this screen is concerned. A battle, a save load and a
        // quit to the menu all end with this scene unloaded (SceneLoader unloads every scene but the
        // one it just brought in), so each of them answers with a NEW installer and gets a new
        // adapter and a new cursor; the bookmarks are on disk under the game's own identity and
        // outlive all three.
        private readonly ScreenSource<AdventureViewInstaller> _installer =
            ScreenSource<AdventureViewInstaller>.FromSceneRoot(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _installer.Current;
        }

        protected override AdventureMapAdapter Adapt(object menu)
        {
            // The adapter is built as soon as the scene's installer exists, which is while the
            // loading screen is still up; its events are attached in OnPush, once the map is up.
            AdventureViewInstaller installer = (AdventureViewInstaller)menu;
            return new AdventureMapAdapter(installer, GetAdventureMapRevealedRegistry(installer));
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
            _grid = Live == null ? null : new AdventureMapGrid(Live, ReadMapTile);
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

        /// <summary>The adventure view installed and ready, and none of the four things that take
        /// the map AWAY: a story sequence running (the camera and the keyboard are the story's), a
        /// claimed battle, the game's UI blocker fading in over a session being abandoned, and the
        /// loading screen. A popup does NOT deactivate the map - it covers it by layer, so the HUD
        /// stays drawn underneath and the event listener and its audio are not torn down and
        /// rebuilt for every dialog.
        ///
        /// The battle gate is what keeps the map quiet from the attack until the battle is over. The
        /// game hides the troop placement page the moment Quick Battle or Manual Battle is pressed
        /// and the result page or the combat screen only arrives some frames later; without this the
        /// map is the top screen in between and is announced. The blocker gate is the same story on
        /// the way out: Quit to Main Menu closes the pause menu first and the scene loader only goes
        /// busy once the blocker has faded in, and the map was announced in between.</summary>
        public override bool IsActive()
        {
            if (!base.IsActive())
            {
                return false;
            }

            if (Live.IsStoryTriggerRunning() || Live.IsBattleClaimed() || Live.IsProjectUiBlockerShowing())
            {
                return false;
            }

            ScreenManager screens = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ScreenManager;
            LoadingCompleteScreen loading = screens == null ? null : screens.Registered<LoadingCompleteScreen>();
            // Whether the loading screen is SHOWING, not whether its menu exists: the loading scene's
            // menu is now resolved for as long as that scene is loaded, and the map comes back when
            // the prompt is answered rather than when the scene finally unloads.
            return loading == null || !loading.IsActive();
        }

        /// <summary>The cursor survives the story gap, a battle, the way out of a session and the
        /// loading screen, which are the only four things that take the map off the stack.</summary>
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

        /// <summary>The map's events are listened to for exactly as long as the map is up: attached
        /// here, where the loading screen is gone and the map is filled in, so the listener's discovery
        /// baseline is the loaded map and not the empty one the adapter was built over; detached in
        /// <see cref="OnPop"/>. The listener itself lives on the adapter, which releases it in its
        /// Dispose when the slot moves to another adventure.
        ///
        /// The kept tile and tooltip are dropped here because nothing reported what the game changed
        /// while the map was off the stack: a quick battle removes the defender with the listener
        /// detached, and a cursor left standing on that tile would otherwise still read the defeated
        /// stack and its attack instruction. A manual battle unloads the scene and gets a new adapter,
        /// which drops them in <see cref="Grid"/>; a quick battle does not.</summary>
        public override void OnPush()
        {
            Live?.AttachEvents(InvalidateTile);
            InvalidateTile();
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
            Live?.DetachEvents();
            _isTopScreen = false;
            Grid()?.DisposeAudio();
            Grid()?.HideOverlay();
            base.OnPop();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            Live?.UpdateEvents();
            WatchTeleportMode();
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
            if (Teleport != null)
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
            TileInstructionHints.Add(vtable, TileTooltip, () => Teleport == null);
            builder.AddItem(new SyntheticNode(MapNodeId, vtable));

            builder.PopContext();
        }

        private string MapContext()
        {
            TeleportMenuAdapter teleport = Teleport;
            string instruction = teleport != null ? teleport.InstructionText : null;
            return string.IsNullOrWhiteSpace(instruction) ? ModText.Get(ModStrings.Screens.Map) : instruction;
        }

        /// <summary>The tile under the cursor, read once and kept until the cursor moves or the map
        /// changes, the same shape as <see cref="TileTooltip"/> below. Both hold localized text the
        /// pause menu's options page can change under a still cursor - no map event fires for it -
        /// so both are keyed on the language as well.</summary>
        private AdventureMapTile CursorTile()
        {
            Vector2Int tile = Grid().CursorTile;
            ILanguageDefinition language = MapLanguage();
            if (_tileRead && tile == _tileTile && ReferenceEquals(language, _tileLanguage))
            {
                return _tile;
            }

            _tileTile = tile;
            _tileLanguage = language;
            _tileRead = true;
            _tile = Live.GetTile(tile);
            return _tile;
        }

        /// <summary>Every change the map listens to - a selection, a move, a teleport, a command, a
        /// spawned entity, the fog - drops the kept tile AND the kept tooltip, so nothing the game
        /// changes under a still cursor is read from either cache. The tooltip carries the tile's
        /// click instructions, which a selection changes without moving the cursor.</summary>
        private void InvalidateTile()
        {
            _tileRead = false;
            _tile = null;
            _tooltipRead = false;
            _tooltip = null;
        }

        private Tooltip TileTooltip()
        {
            Vector2Int tile = Grid().CursorTile;
            ILanguageDefinition language = MapLanguage();
            if (_tooltipRead && tile == _tooltipTile && ReferenceEquals(language, _tooltipLanguage))
            {
                return _tooltip;
            }

            _tooltipTile = tile;
            _tooltipLanguage = language;
            _tooltipRead = true;
            _tooltip = Live.GetTooltip(tile);
            return _tooltip;
        }

        /// <summary>The language the map's text is being read in, off the adapter's own handler.</summary>
        private ILanguageDefinition MapLanguage()
        {
            ILocalizationHandler localization = Live != null ? Live.LocalizationHandler : null;
            return localization != null ? localization.CurrentLanguage : null;
        }

        /// <summary>Enter on the map. While the teleport menu is up it confirms the destination and
        /// only while the cursor is standing on it; anywhere else it is refused in silence, as it is
        /// today.</summary>
        private void ActivateTile()
        {
            TeleportMenuAdapter teleport = Teleport;
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
            if (Teleport != null)
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
                NodeVtable vtable = GraphNodes.Text(() => ExperienceLabel(hud), null, hud.ExperienceTooltip);
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

            GraphNodes.SyntheticButton(
                builder,
                "adventure-map:wielder-sheet",
                hud.IsInventoryButtonVisible(),
                () => hud.InventoryButtonLabel,
                () => hud.ClickInventoryButton(),
                hud.IsInventoryButtonEnabled,
                hud.InventoryButtonTooltip,
                hud.FocusInventoryButton);
            GraphNodes.SyntheticButton(
                builder,
                "adventure-map:movement",
                hud.IsMoveToDestinationButtonVisible(),
                () => hud.MoveToDestinationButtonLabel,
                () => hud.ClickMoveToDestinationButton(),
                hud.IsMoveToDestinationButtonEnabled,
                hud.MoveToDestinationButtonTooltip,
                hud.FocusMoveToDestinationButton);
            GraphNodes.SyntheticButton(
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
                VisualTooltipMetadata.ForComponent(target),
                isLong: () => NativeTooltipUtility.IsLongForComponent(target, portrait.RefreshTooltip));
        }

        /// <summary>The experience bar as one line: the game's caption for it, the level reached and
        /// the experience earned against what the next level asks for. Where no wielder is selected
        /// there is nothing to count, so the caption stands alone.</summary>
        private static string ExperienceLabel(AdventureHudAdapter hud)
        {
            int level;
            int current;
            int nextLevelExperience;
            if (!hud.TryGetExperience(out level, out current, out nextLevelExperience))
            {
                return hud.ExperienceCaption;
            }

            return ModText.Get(
                ModStrings.Screens.WielderExperience,
                hud.ExperienceCaption,
                hud.LevelCaption,
                level,
                current,
                nextLevelExperience);
        }

        private static void BuildEssences(GraphBuilder builder, AdventureHudAdapter hud, bool drawn)
        {
            if (!drawn)
            {
                return;
            }

            EssenceRows.Build(
                builder,
                "adventure-map:",
                essence => hud.GetEssenceLabel(essence),
                essence => hud.GetEssenceTooltip(essence),
                essence => hud.FocusEssence(essence));
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
                        GraphNodes.Text(() => ResourceLabel(hud, resource), null, hud.GetResourceTooltip(resource)),
                        () => hud.FocusResource(resource))));
            }

            builder.PopContext();
        }

        /// <summary>One entry of the treasury strip: the resource's name, what the strip draws beside
        /// it and the income it draws under it.</summary>
        private static string ResourceLabel(AdventureHudAdapter hud, ResourceType resource)
        {
            return ResourceStrip.Label(
                hud.GetResourceName(resource),
                hud.GetResourceAmountText(resource),
                hud.GetResourceIncomeText(resource));
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
            ChatAdapter chat = ChatSource.Current;
            bool chatDrawn = chat != null && chat.IsButtonVisible();
            bool bugReportDrawn = hud.IsBugReportButtonVisible();
            if (!optionsDrawn && !overviewDrawn && !chatDrawn && !bugReportDrawn)
            {
                return;
            }

            builder.BeginStop(KingdomStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Kingdom));

            GraphNodes.SyntheticButton(
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
                // Checked here rather than handed to SyntheticButton, because the tooltip beside it
                // is an argument: an undrawn slot would compose one for a node nothing draws. The
                // wielder and town lists guard theirs the same way.
                if (!hud.IsKingdomOverviewItemVisible(index))
                {
                    continue;
                }

                GraphNodes.SyntheticButton(
                    builder,
                    KingdomKeyPrefix + index,
                    true,
                    () => hud.GetKingdomOverviewLabel(index),
                    () => hud.ClickKingdomOverviewItem(index),
                    () => hud.IsKingdomOverviewItemEnabled(index),
                    hud.GetKingdomOverviewTooltip(index),
                    () => hud.FocusKingdomOverviewItem(index));
            }

            GraphNodes.SyntheticButton(
                builder,
                "adventure-map:chat",
                chatDrawn,
                () => ChatButtonText.Label(chat),
                () => chat.Open(),
                () => chat.IsButtonEnabled(),
                chatDrawn ? chat.ButtonTooltip : null,
                () => chat.FocusButton());
            GraphNodes.SyntheticButton(
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
                // Checked here rather than handed to AddHudButton, because the tooltip beside it is
                // an argument: an undrawn slot would compose one for a node nothing draws.
                if (!hud.IsWielderListEntryVisible(index))
                {
                    continue;
                }

                GraphNodes.SyntheticButton(
                    builder,
                    WielderKeyPrefix + index,
                    true,
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
                // Checked here rather than handed to AddHudButton, because the tooltip beside it is
                // an argument: an undrawn slot would compose one for a node nothing draws.
                if (!hud.IsTownListEntryVisible(index))
                {
                    continue;
                }

                GraphNodes.SyntheticButton(
                    builder,
                    TownKeyPrefix + index,
                    true,
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
                    () => ObjectiveLabel(hud, index),
                    null,
                    hud.GetObjectiveTooltip(index));
                vtable.OnFocusVisual = () => hud.FocusObjective(index);
                vtable.OnBlurVisual = hud.UnfocusObjective;
                builder.AddItem(new SyntheticNode(ControlId.Structural(ObjectiveKeyPrefix + index), vtable));
            }

            builder.PopContext();
        }

        /// <summary>
        /// One objective row: what the panel drew, prefixed by where the objective stands - a lose
        /// condition, or complete or incomplete and whether it can still be reached - and followed,
        /// where the entry has a single unfinished marker, by how far off it is and which way.
        /// </summary>
        private static string ObjectiveLabel(AdventureHudAdapter hud, int index)
        {
            string text = hud.GetObjectiveText(index);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (hud.IsObjectiveLoseCondition(index))
            {
                parts.Add(ModText.Get(ModStrings.Screens.LoseCondition));
            }
            else
            {
                parts.Add(ModText.Get(hud.IsObjectiveComplete(index)
                    ? ModStrings.Screens.ObjectiveCompleted
                    : ModStrings.Screens.ObjectiveIncomplete));
                if (!hud.CanObjectiveBeCompleted(index))
                {
                    parts.Add(ModText.Get(ModStrings.Screens.ObjectiveCannotBeCompleted));
                }
            }

            parts.Add(text);

            Vector2Int offset;
            Vector2Int mapSize;
            if (hud.TryGetObjectiveMarkerOffset(index, out offset, out mapSize))
            {
                parts.Add(ObjectiveMarkerDistance(offset, mapSize));
                parts.Add(ObjectiveMarkerDirection(offset));
            }

            return ModText.JoinListWithCommas(parts);
        }

        /// <summary>How far the marker is, as a share of the map rather than in tiles, so the wording
        /// means the same thing on a small map and a large one.</summary>
        private static string ObjectiveMarkerDistance(Vector2Int offset, Vector2Int mapSize)
        {
            float normalizedX = offset.x / (float)mapSize.x;
            float normalizedY = offset.y / (float)mapSize.y;
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            if (distance <= 0.10f)
            {
                return ModText.Get(ModStrings.Screens.ObjectiveMarkerNearby);
            }

            if (distance <= 0.25f)
            {
                return ModText.Get(ModStrings.Screens.ObjectiveMarkerSomeDistance);
            }

            return ModText.Get(ModStrings.Screens.ObjectiveMarkerFarAway);
        }

        private static string ObjectiveMarkerDirection(Vector2Int offset)
        {
            if (offset.x == 0 && offset.y == 0)
            {
                return ModText.Get(ModStrings.Spatial.Here);
            }

            if (offset.y > 0)
            {
                if (offset.x > 0)
                {
                    return ModText.Get(ModStrings.Scanner.Northeast);
                }

                if (offset.x < 0)
                {
                    return ModText.Get(ModStrings.Scanner.Northwest);
                }

                return ModText.Get(ModStrings.Scanner.North);
            }

            if (offset.y < 0)
            {
                if (offset.x > 0)
                {
                    return ModText.Get(ModStrings.Scanner.Southeast);
                }

                if (offset.x < 0)
                {
                    return ModText.Get(ModStrings.Scanner.Southwest);
                }

                return ModText.Get(ModStrings.Scanner.South);
            }

            return offset.x > 0
                ? ModText.Get(ModStrings.Scanner.East)
                : ModText.Get(ModStrings.Scanner.West);
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
            TeleportMenuAdapter teleport = Teleport;
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
            get { return Teleport == null && !IsMapFocused(); }
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
            if (hud == null || Teleport != null)
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

        /// <summary>
        /// The map's tile cursor has landed on another tile, and the readout is deliberately the
        /// NAVIGATOR's rather than the grid's. The whole map is one node, so a step inside it moves
        /// no focus and the engine would say nothing on its own; a tile said in the grid instead
        /// would be the bare label, without the node's tooltip and without its usage hints.
        /// </summary>
        private void ReadMapTile()
        {
            GraphNavigator navigator = Navigator;
            if (navigator == null || !ReferenceEquals(navigator.Screen, this))
            {
                // The map is not the page the player is on; a tile read at them would not be either.
                return;
            }

            if (IsMapFocused())
            {
                navigator.ReannounceFocused();
                return;
            }

            // The cursor was walked while focus sat on a HUD stop (the teleport menu picking a
            // destination): the landing on the map is what reads the tile, once - an announcing
            // one, because a silent landing plus a re-announce would read nothing.
            navigator.FocusNode(MapNodeId);
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

        /// <summary>The mode the teleport menu puts the map into, watched rather than waited for: the
        /// menu draws itself and the map reads it, so entering and leaving are edges of what the game
        /// is doing and no hook has to report either.</summary>
        private void WatchTeleportMode()
        {
            TeleportMenuAdapter teleport = Teleport;
            if ((teleport != null) == _inTeleportMode)
            {
                return;
            }

            _inTeleportMode = teleport != null;
            if (teleport != null)
            {
                EnterTeleportDestinationMode(teleport);
            }
            else
            {
                ExitTeleportDestinationMode();
            }
        }

        private void EnterTeleportDestinationMode(TeleportMenuAdapter adapter)
        {
            string instruction = adapter.InstructionText;
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                SpeechPipeline.Output(new SpeechRequest(instruction, interrupt: true));
            }

            // The landing on the map node is what reads the destination now (ReadMapTile), so the
            // focus request is left to it: a silent one here would swallow that readout.
            Grid().FocusTile(adapter.CurrentDestination);
        }

        /// <summary>The menu has closed. The cursor comes back to the wielder it was teleporting, and
        /// a menu the player cancelled says so - read off the answer the menu completed its own async
        /// with rather than off a hook on Cancel.</summary>
        private void ExitTeleportDestinationMode()
        {
            TeleportMenuAdapter adapter = _teleportSource.Current;
            bool cancelled = adapter != null && adapter.WasCancelled;
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

        /// <summary>The teleport menu while it is drawn, which is the whole of "the map is picking a
        /// destination"; null the rest of the time.</summary>
        private TeleportMenuAdapter Teleport
        {
            get
            {
                TeleportMenuAdapter adapter = _teleportSource.Current;
                return adapter != null && adapter.IsPresent() ? adapter : null;
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
            TeleportMenuAdapter teleport = Teleport;
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

        /// <summary>The one way the game moves the cursor. It is read out only where the player is on
        /// the map: on the map node it is the tile's own landing, and from a HUD control (a town in the
        /// towns list, a wielder in the wielders list) it is a landing on the map node, whose readout
        /// is the tile. Under another screen the cursor is moved without a word and focus is put back
        /// on the map without announcing that either.</summary>
        private void MoveCursor(Vector2Int tile, bool announce)
        {
            // The events that land here (a camera focus published from a HUD click) arrive while the
            // adventure is still being built as well as while it is up, and the cursor exists only
            // once the map does.
            AdventureMapGrid grid = Grid();
            if (grid == null)
            {
                return;
            }

            if (announce && _isTopScreen && IsMapFocused())
            {
                grid.FocusTile(tile);
                return;
            }

            grid.FocusTileSilently(tile);
            Navigator?.FocusNode(MapNodeId, announce: announce && _isTopScreen);
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

        /// <summary>The revealed registry this adventure records its discoveries in. A NEW ADAPTER
        /// IS NOT A NEW GAME: a manual battle unloads the adventure scene and loads it again
        /// afterwards, and the registry must survive that, so the mod keys it on the object the
        /// running adventure is rather than on the adapter or the menu scene passed through
        /// (<see cref="AdventureMapAdapter.AdventureGame"/>).</summary>
        private static AdventureMapRevealedRegistry GetAdventureMapRevealedRegistry(AdventureViewInstaller installer)
        {
            AdventureMapRevealedRegistry registry = SocAccessMod.Instance
                ?.AdventureRevealedRegistry(AdventureMapAdapter.AdventureGame(installer));
            return registry ?? new AdventureMapRevealedRegistry();
        }

    }
}
