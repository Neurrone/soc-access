using System;
using System.Collections.Generic;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Spells;
using SongsOfConquest.Utilities;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The battle: a MODE whose cursor is not the focus cursor, plus the HUD panels the game draws
    /// around it. The largest of phase E's three modes and the last of them.
    ///
    /// THE BATTLEFIELD IS ONE NODE with a fixed identity, first and alone in its stop. Its label is
    /// the tile the cursor stands on (with the inspection's context and the spell-target selection),
    /// its review buffer that tile's inspect tooltip, Enter the confirmation of a spell or ability
    /// target while one is being aimed, and Backslash the right click a troop acts with.
    /// <see cref="CombatHexGrid"/> survives as the cursor and owns every key that walks it - the six
    /// hex moves and their skips, Ctrl+Space for the centre tile, I to inspect, comma and period for
    /// the friendly and enemy troop cycles, Space for the acting troop, W for the relevant tiles, T
    /// for the turn order, S for the threat, and the scanner - answered through
    /// <see cref="ModeClaims"/>, which the navigator asks BEFORE its own set while the board's node is
    /// focused. Home, End and Backspace are TRANSLATED onto the scanner's jump, distance and return
    /// and Space onto the acting-troop focus, as the map and the pre-battle page translate theirs, so
    /// an injected key behaves exactly as the physical one. Because the node's id never changes as the
    /// cursor moves, a step is not a move as far as the navigator is concerned; the grid therefore asks
    /// it to read the node again as a landing (<see cref="GraphNavigator.ReannounceFocused"/>), which
    /// is what gets the tile's tooltip and hints read on a step and refills the review buffer.
    ///
    /// THE BOARD'S STOP IS NAMED by the game's combat-grid word, or by the targeting instruction
    /// while a spell or an ability is being aimed, so entering it says what the game is asking for.
    /// The instruction is also watched passively: the begin handlers and the adapter's own narration
    /// speak the FIRST instruction, so the watcher speaks only an instruction the game REPLACED with
    /// another while the player was aiming.
    ///
    /// THE HUD STOPS follow it in drawn order and each exists only while the game draws its panel, so
    /// Tab walks exactly what is on the screen: the quickbar down the left edge, the attacker's
    /// column, the defender's, the current troop with its ability, the turn order, the two buttons in
    /// the top right, the battle log, and End Turn alone in the bottom right.
    ///
    /// ESCAPE: on the board it belongs to the game - which opens the pause menu - EXCEPT while a
    /// sub-mode is on, where it gives up the spell being aimed, the ability being aimed, or the
    /// inspection. On a HUD stop the screen takes it and lands the cursor back on the board with the
    /// game's own close-menu noise. Type-ahead is off on the board's node alone (its letters are the
    /// hex moves, the inspect key, the relevant-tile walk and the threat readout) and on everywhere
    /// else.
    ///
    /// Measured on the 1280x800 fixture 2026-09-08 (Cecilia Stoutheart with Footmen, Rangers,
    /// Minstrels and Militia against three Oathbound stacks): the attacker's column LEFT - a portrait
    /// <c>Button</c> [45,43,65,65] carrying the wielder's stat tooltip, the five essence icons under
    /// it (y 66-114), <c>AiAutoBattleButton</c> [129,15,32,32] "Auto Battle" and <c>SpellbookButton</c>
    /// [11,120,49,49] "Spells (V)" with the quickbar's <c>SpellcastingContainer</c> [0,71,71,447]
    /// under it; the defender's column RIGHT [1039..1280] drawing only the army's emblem, so no
    /// defender stop is built at all. Top centre-right <c>ChatButton</c> [971,5,35,35] and
    /// <c>GameMenuButton</c> [1009,5,35,35]; bottom centre the <c>QueueHUD</c> with
    /// <c>SelectedTroop</c> [333,722,61,78] and the queue's entries [397..774] with a round separator
    /// at [508,763]; bottom right <c>EndTurnButton</c> [1206,724,59,59]. The acting troop's ability
    /// button and its Cancel live on the <c>BattleTroopStatusPanel</c> drawn over the troop itself.
    ///
    /// UNVERIFIED on this fixture: the quickbar (no spell was in it), the defender's column (a
    /// neutral army draws no wielder there), the battle log (the log window was empty), the player
    /// name lines and the turn timer (multiplayer only), and the spell and ability targeting states.
    /// </summary>
    public sealed class CombatScreen : LiveScreen<CombatAdapter>
    {
        private const string ReturnToGridSoundKey = "Common_ClosePauseMenu";

        private const string BoardStop = "combat:board";
        private const string QuickbarStop = "combat:quickbar";
        private const string AttackerStop = "combat:attacker";
        private const string DefenderStop = "combat:defender";
        private const string CurrentTroopStop = "combat:current-troop";
        private const string TurnOrderStop = "combat:turn-order";
        private const string MenuStop = "combat:menu";
        private const string BattleLogStop = "combat:battle-log";
        private const string EndTurnStop = "combat:end-turn";

        private const string QueueKeyPrefix = "combat:queue:";
        private const string QuickbarKeyPrefix = "combat:quickbar:";
        private const string BattleLogKeyPrefix = "combat:battle-log:";

        /// <summary>The one node the whole battlefield is. Fixed, so walking the cursor is never a
        /// move as far as the navigator is concerned.</summary>
        public static readonly ControlId BoardNodeId = ControlId.Structural("combat:tile");

        // The hex cursor, built over the battle it walks. Rebuilt when the slot is pointed at a
        // DIFFERENT battle and kept otherwise, so a dialog covering the battlefield does not move it.
        private CombatHexGrid _grid;
        private CombatAdapter _gridAdapter;
        private CombatAdapter _battleStateAdapter;
        private readonly CombatTroopCycle _localActingTroopCycle = new CombatTroopCycle();
        private readonly CombatTroopCycle _enemyActingTroopCycle = new CombatTroopCycle();
        private int _lastCycleCurrentTroopId = -1;

        // The tile's inspect tooltip is the game's whole details capture and the graph is rebuilt for
        // every navigation operation, so it is composed once per tile - which is exactly as often as
        // the widget engine's focus commit composed it. The key is the cursor's state AND the board's,
        // so a troop that steps onto the tile, is hurt on it or dies on it under a still cursor is
        // read afresh.
        private Vector2Int _tooltipTile;
        private bool _tooltipInspecting;
        private CombatTargetingMode _tooltipTargeting;
        private int _tooltipTroopId = -1;
        private int _tooltipTroopHealthLost;
        private int _tooltipTroopStatusCount;
        private int _tooltipTurn;
        private Vector2Int _tooltipCurrentTroopTile;
        private Tooltip _tooltip;
        private bool _tooltipRead;

        // The targeting instruction, watched passively: baselined wherever it is spoken, so only an
        // instruction the game REPLACED under a still cursor is announced.
        private string _instruction;

        // THE BATTLE FINDS ITSELF: the scene installer is the battle (see BattleSources). One
        // installer instance per fight, so a second battle gets a second adapter and everything the
        // screen keeps about the first is dropped with it; a hot reload finds the same installer and
        // rebuilds the adapter over it, which is all the old probe ever did.
        protected override object ResolveMenu()
        {
            return BattleSources.Scene.Current;
        }

        /// <summary>The adapter over one battle, built once per installer. Building it is what
        /// starting a battle means to the narration: it is told which battle it is reading, and the
        /// combat-event buffer of the previous fight is emptied.</summary>
        protected override CombatAdapter Adapt(object menu)
        {
            CombatAdapter adapter = new CombatAdapter((BattleSceneInstaller)menu);
            adapter.AttachActingTroopFocus(HandleActingTroopFocusRequest);
            CombatEventNarrator.SetActiveAdapter(adapter);
            SocAccessMod.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.CombatEvents);
            return adapter;
        }

        /// <summary>The cursor is built over one battle: a new one gets a new grid.</summary>
        private CombatHexGrid Grid()
        {
            if (_grid != null && ReferenceEquals(_gridAdapter, Live))
            {
                return _grid;
            }

            _gridAdapter = Live;
            _grid = Live == null ? null : new CombatHexGrid(Live, this, ReadBoardTile);
            return _grid;
        }

        public override string Key
        {
            get { return "combat"; }
        }

        /// <summary>Layer 10: the battlefield; registered after the map, so it covers it.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.Combat); }
        }

        /// <summary>Off for the whole screen (owner ruling 2026-09-08): the game keeps its battle
        /// hotkeys (V the spellbook, E end turn) from every stop, as on the adventure map, and on
        /// the board A, D, Q, E, Z, C, I, W and S are the cursor's keys.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        /// <summary>Spelled out rather than layered over the base: the battle state is synced between
        /// the slot and the presence check, and "not present" is where the fight ENDS, not merely a
        /// false answer.</summary>
        public override bool IsActive()
        {
            SyncLive();
            SyncBattleState();
            if (Live == null)
            {
                return false;
            }

            if (!Live.IsPresent())
            {
                // The game ends the battle (BattleGameOverCommand clears IsBattleActive) while the
                // scene, and so the source, is still there for the result page. The narration held
                // back for the last blows is spoken here, once, by the adapter it belongs to.
                Live.EndCombat();
                return false;
            }

            return true;
        }

        public CombatAdapter Adapter
        {
            get { return Live; }
        }

        /// <summary>The cursor survives the story gap, which is the one thing that takes the
        /// battlefield off the stack while the battle is still being fought.</summary>
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

                yield return ReviewBufferKind.CombatEvents;
            }
        }

        public override void OnPush()
        {
            AccessibilityEventBus.Subscribe(HandleAccessibilityEvent);
            Live?.AttachSpellCastBegin(HandleSpellCastBegin);
            Live?.AttachSpellTargetingNarration(HandleSpellTargetInstruction);
            Live?.AttachAbilityTargetingBegin(HandleAbilityTargetingBegin);
            Live?.AttachAbilityTargetingEnd(HandleAbilityTargetingEnd);
            AnnounceVisibleSpellTargetInstruction();
            _instruction = InstructionText;
        }

        public override void OnUnfocus()
        {
            Live?.ClearNativeTooltip();
            Live?.ClearFocusedTileOverlay();
            base.OnUnfocus();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            Live?.DetachSpellCastBegin();
            Live?.DetachSpellTargetingNarration();
            Live?.DetachAbilityTargetingBegin();
            Live?.DetachAbilityTargetingEnd();
            Live?.Hud.ClearSpellTargetInstructionText();
            Live?.Hud.ClearAbilityTargetInstructionText();
            Live?.ClearNativeTooltip();
            Live?.ClearFocusedTileOverlay();
            base.OnPop();
        }

        /// <summary>Everything the screen remembers ABOUT ONE BATTLE, dropped when the battle
        /// changes. These are the mod's own wording state rather than anything the game holds - the
        /// two troop cycles' anchors, the acting-troop baseline, the last spoken targeting
        /// instruction and the tile tooltip's cache - so they stay on the screen; they are keyed on
        /// the identity of the adapter, which <see cref="LiveScreen{TAdapter}.SyncLive"/> reads from
        /// the game every frame, so a new battle and a hot reload start them fresh and no hook has to
        /// reset them. Without this the same coordinates, troop id and turn number in a NEW battle
        /// would be answered with the previous battle's tile.</summary>
        private void SyncBattleState()
        {
            if (ReferenceEquals(_battleStateAdapter, Live))
            {
                return;
            }

            _battleStateAdapter = Live;
            _tooltip = null;
            _tooltipRead = false;
            _lastCycleCurrentTroopId = -1;
            _localActingTroopCycle.Reset();
            _enemyActingTroopCycle.Reset();
            _instruction = null;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            // Read from the game every frame: the mouse takes the game's hover back from the
            // keyboard cursor the moment the physical pointer moves.
            Live?.WatchHoverOwnership();
            WatchInstruction();
        }

        // ---- the graph ----

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildBoard(builder);

            BattleHudAdapter hud = Live.Hud;
            if (hud == null)
            {
                return;
            }

            BuildQuickbar(builder, hud);
            BuildSide(builder, hud, CombatHudSide.Attacker);
            BuildSide(builder, hud, CombatHudSide.Defender);
            BuildCurrentTroop(builder, hud);
            BuildTurnOrder(builder, hud);
            BuildMenu(builder, hud);
            BuildBattleLog(builder, hud);
            BuildEndTurn(builder, hud);
        }

        // ---- the battlefield ----

        /// <summary>The board itself: one node, named by the tile under the cursor, in a stop the
        /// targeting instruction renames to itself while a spell or an ability is being aimed.
        /// </summary>
        private void BuildBoard(GraphBuilder builder)
        {
            builder.BeginStop(BoardStop);
            builder.PushContext(BoardContext());

            Tooltip tooltip = TileTooltip();
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(() => Grid().GetLabel()) },
                Sections = BoardSections(tooltip),
            };
            GraphNodes.Aim(vtable, tooltip);
            vtable.OnActivate = ConfirmTarget;
            vtable.OnContextual = ContextualTile;
            vtable.OnFocusVisual = () => Grid()?.ShowOverlay();
            vtable.OnBlurVisual = () => Grid()?.HideOverlay();
            TileInstructionHints.Add(vtable, TileTooltip);
            builder.AddItem(new SyntheticNode(BoardNodeId, vtable));
            builder.SetStart(BoardNodeId);

            builder.PopContext();
        }

        /// <summary>What the board node reads, in the order it reads: the damage preview the mod
        /// composed out of the game's own numbers, the tile's dossier, and then what the buff and
        /// nerf indicators over the stack standing there say.
        ///
        /// The preview is a COMPOSED section rather than part of the dossier tooltip, which is what
        /// lets it be spoken on arrival while the dossier - a long tooltip - is only reviewed: a
        /// player who has not asked to hear long tooltips still hears what the attack would do,
        /// which is the one thing a mouse player sees without asking.
        ///
        /// The indicators' own lines are a BUFFER section: the readout already names them
        /// ("Momentum"), and what each one does is a stat block that belongs beside the dossier
        /// rather than in front of the player on every step. A section of its own rather than lines
        /// appended to the dossier, because they are a different hover surface and the dossier's
        /// tooltip is the node's one tooltip.</summary>
        private IList<NodeSection> BoardSections(Tooltip tooltip)
        {
            List<NodeSection> sections = new List<NodeSection>(3);
            sections.Add(NodeSection.Composed(() => Grid().GetAttackPreviewLines()));
            NodeSection dossier = GraphNodes.TooltipSection(tooltip);
            if (dossier != null)
            {
                sections.Add(dossier);
            }

            sections.Add(NodeSection.Buffer(() => Grid().GetTroopEffectDetailLines()));
            return sections;
        }

        private string BoardContext()
        {
            string instruction = IsAiming ? InstructionText : null;
            return string.IsNullOrWhiteSpace(instruction) ? ModText.Get(ModStrings.UI.Battlefield) : instruction;
        }

        /// <summary>The cached tooltip, kept while everything it was composed from still reads the
        /// same: where the cursor stands, whether it is inspecting, what is being aimed, who stands on
        /// the tile with how much health lost and how many statuses - a wielder's spell changes the
        /// dossier's bacterias and restrictions without taking any health - the battle's own turn
        /// counter, and where the acting troop stands, which is what the dossier's reach, its travel
        /// cost and its "click to attack" row are measured from and which a troop with movement left
        /// changes without the turn moving on.</summary>
        private Tooltip TileTooltip()
        {
            Vector2Int tile = Grid().CursorTile;
            bool inspecting = Grid().IsInspecting;
            CombatTargetingMode targeting = Live.GetTargetingMode();
            int troopId;
            int troopHealthLost;
            int troopStatusCount;
            Live.GetTileTroopState(tile, out troopId, out troopHealthLost, out troopStatusCount);
            int turn = Live.GetCurrentTurn();
            Vector2Int currentTroopTile = Live.GetCurrentTroopPosition();
            if (_tooltipRead
                && tile == _tooltipTile
                && inspecting == _tooltipInspecting
                && targeting == _tooltipTargeting
                && troopId == _tooltipTroopId
                && troopHealthLost == _tooltipTroopHealthLost
                && troopStatusCount == _tooltipTroopStatusCount
                && turn == _tooltipTurn
                && currentTroopTile == _tooltipCurrentTroopTile)
            {
                return _tooltip;
            }

            _tooltipTile = tile;
            _tooltipInspecting = inspecting;
            _tooltipTargeting = targeting;
            _tooltipTroopId = troopId;
            _tooltipTroopHealthLost = troopHealthLost;
            _tooltipTroopStatusCount = troopStatusCount;
            _tooltipTurn = turn;
            _tooltipCurrentTroopTile = currentTroopTile;
            _tooltipRead = true;
            _tooltip = Grid().GetTooltip();
            return _tooltip;
        }

        /// <summary>Enter on the board. The game binds no confirm key in battle, so this only
        /// confirms the target of a spell or an ability being aimed and is otherwise silent.</summary>
        private void ConfirmTarget()
        {
            SpeakSpellTargetSelection(Grid().ConfirmTarget());
        }

        /// <summary>What confirming a spell target did, in words: a target taken says how many times
        /// this tile is now aimed at, a target given back says so, and a click that changed nothing
        /// is silent.</summary>
        private static void SpeakSpellTargetSelection(CombatSpellTargetSelection selection)
        {
            if (!selection.Clicked)
            {
                return;
            }

            if (selection.Count > selection.PreviousCount)
            {
                Speak(selection.Count > 1
                    ? ModText.Get(ModStrings.UI.SelectedCount, selection.Count)
                    : ModText.Get(ModStrings.UI.Selected));
            }
            else if (selection.Count < selection.PreviousCount && selection.StillTargeting)
            {
                Speak(selection.Count > 0
                    ? ModText.Get(ModStrings.UI.SelectedCount, selection.Count)
                    : ModText.Get(ModStrings.UI.Unselected));
            }
        }

        /// <summary>Backslash on the board: the game's own right click, which is how a troop moves
        /// and attacks.</summary>
        private void ContextualTile()
        {
            Live.HandleSecondaryAction(Grid().CursorTile);
        }

        // ---- the quickbar ----

        /// <summary>The spell slots the game draws down the left edge while a wielder can cast.
        /// </summary>
        private void BuildQuickbar(GraphBuilder builder, BattleHudAdapter hud)
        {
            if (!hud.IsQuickbarMenuVisible())
            {
                return;
            }

            IReadOnlyList<BattleHudAdapter.QuickbarItem> items = hud.GetQuickbarItems();
            if (items.Count == 0)
            {
                return;
            }

            builder.BeginStop(QuickbarStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Quickbar));
            for (int i = 0; i < items.Count; i++)
            {
                BattleHudAdapter.QuickbarItem item = items[i];
                if (item == null || !item.HasSpell)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Button(
                    () => BuildQuickbarItemLabel(hud, item),
                    () => ActivateQuickbarItem(item),
                    () => item.IsEnabled,
                    // The spell's details are read when the tooltip is read, never when the build
                    // asks whether there is one.
                    new Tooltip(() => SpellTooltipText.Lines(item.ReadTooltipFacts()), null));
                vtable.OnFocusVisual = item.Focus;
                vtable.OnBlurVisual = item.Unfocus;
                builder.AddItem(new SyntheticNode(ControlId.Structural(QuickbarKeyPrefix + item.Index), vtable));
            }

            builder.PopContext();
        }

        /// <summary>A quickbar slot: the spell's name with the tier the wielder can cast it at, as
        /// the spellbook titles it.</summary>
        private static string BuildQuickbarItemLabel(BattleHudAdapter hud, BattleHudAdapter.QuickbarItem item)
        {
            if (item == null || !item.HasSpell)
            {
                return string.Empty;
            }

            return ModText.JoinListWithCommas(new[]
            {
                SpellTooltipText.Name(item.SpellName),
                hud.GetTierLabel(item.SpellTier)
            });
        }

        /// <summary>Casting from the quickbar puts the game into targeting, and the target is a place
        /// on the board, so the cursor goes there.</summary>
        private void ActivateQuickbarItem(BattleHudAdapter.QuickbarItem item)
        {
            if (item == null || !item.Activate())
            {
                return;
            }

            LandOnBoardWhileAiming();
        }

        // ---- one side's column ----

        /// <summary>One side of the battle, in the order the game draws its column: the player's name
        /// where a game between people draws one, the wielder's portrait with the stat tooltip, the
        /// Auto Battle button, the essences, and the Spells button - which the game replaces with
        /// Cancel spell, in the same spot, while a spell is being aimed.</summary>
        private void BuildSide(GraphBuilder builder, BattleHudAdapter hud, CombatHudSide side)
        {
            BattleCommanderHudAdapter commanders = hud.Commanders;
            bool nameDrawn = commanders.IsPlayerNameVisible(side);
            bool portraitDrawn = commanders.IsPortraitVisible(side);
            bool aiDrawn = commanders.IsAiControlButtonVisible(side);
            bool essencesDrawn = commanders.IsEssenceMenuVisible(side);
            bool spellsDrawn = hud.IsSpellbookButtonVisible(side);
            bool cancelSpellDrawn = hud.IsCancelSpellButtonVisible(side);
            if (!nameDrawn && !portraitDrawn && !aiDrawn && !essencesDrawn && !spellsDrawn && !cancelSpellDrawn)
            {
                return;
            }

            bool attacker = side == CombatHudSide.Attacker;
            string key = attacker ? "combat:attacker:" : "combat:defender:";
            builder.BeginStop(attacker ? AttackerStop : DefenderStop);
            builder.PushContext(ModText.Get(attacker ? ModStrings.Screens.Attacker : ModStrings.Screens.Defender));

            if (nameDrawn)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural(key + "player"),
                    GraphNodes.Text(() => commanders.GetPlayerName(side))));
            }

            if (portraitDrawn)
            {
                // The stats behind the portrait are captured when the tooltip is READ, never when the
                // build asks whether there is one.
                NodeVtable vtable = GraphNodes.Text(
                    () => commanders.GetPortraitLabel(side),
                    null,
                    Portrait.BuildNativeTooltip(() => commanders.GetPortraitButton(side), commanders.Localization));
                vtable.OnFocusVisual = () => Portrait.FocusNative(() => commanders.GetPortraitButton(side));
                builder.AddItem(new SyntheticNode(ControlId.Structural(key + "portrait"), vtable));
            }

            GraphNodes.SyntheticButton(
                builder,
                key + "ai-control",
                aiDrawn,
                () => commanders.GetAiControlButtonLabel(side),
                () => commanders.ClickAiControlButton(side),
                () => commanders.IsAiControlButtonEnabled(side),
                aiDrawn ? commanders.GetAiControlButtonTooltip(side) : null,
                () => commanders.FocusAiControlButton(side));

            BuildEssences(builder, commanders, side, key, essencesDrawn);

            GraphNodes.SyntheticButton(
                builder,
                key + "spells",
                spellsDrawn,
                () => hud.SpellbookButtonLabel,
                () => hud.ClickSpellbookButton(),
                hud.IsSpellbookButtonEnabled,
                spellsDrawn ? hud.SpellbookButtonTooltip : null,
                hud.FocusSpellbookButton);
            GraphNodes.SyntheticButton(
                builder,
                key + "cancel-spell",
                cancelSpellDrawn,
                () => ModText.Get(ModStrings.Screens.CancelSpell),
                () => hud.ClickCancelSpellButton(),
                hud.IsCancelSpellButtonEnabled,
                cancelSpellDrawn ? hud.CancelSpellButtonTooltip : null,
                hud.FocusCancelSpellButton);

            builder.PopContext();
        }

        /// <summary>The five essence counters under the portrait, a region named by the game's own
        /// Essences caption.</summary>
        private static void BuildEssences(
            GraphBuilder builder,
            BattleCommanderHudAdapter commanders,
            CombatHudSide side,
            string key,
            bool drawn)
        {
            if (!drawn)
            {
                return;
            }

            EssenceRows.Build(
                builder,
                key,
                essence => BuildEssenceLabel(commanders, side, essence),
                essence => commanders.GetEssenceTooltip(side, essence),
                essence => commanders.FocusEssence(side, essence));
        }

        /// <summary>One essence counter: the game's word for the essence and what the side holds of
        /// it.</summary>
        private static string BuildEssenceLabel(
            BattleCommanderHudAdapter commanders,
            CombatHudSide side,
            EssenceType essence)
        {
            return ModText.Get(
                ModStrings.Common.ListSeparator,
                EssenceText.Name(commanders.Localization, essence),
                commanders.GetEssenceAmount(side, essence));
        }

        // ---- the current troop ----

        /// <summary>The troop whose turn it is, as the queue draws it at the bottom of the screen,
        /// and the ability it can use - which the game replaces with Cancel ability, in the same spot
        /// on the troop's own status panel, while the ability is being aimed.</summary>
        private void BuildCurrentTroop(GraphBuilder builder, BattleHudAdapter hud)
        {
            bool troopDrawn = hud.IsCurrentTroopIndicatorVisible();
            bool abilityDrawn = hud.IsAbilityButtonVisible();
            bool cancelAbilityDrawn = hud.IsCancelAbilityButtonVisible();
            if (!troopDrawn && !abilityDrawn && !cancelAbilityDrawn)
            {
                return;
            }

            builder.BeginStop(CurrentTroopStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.CurrentTroopSection));

            if (troopDrawn)
            {
                NodeVtable vtable = GraphNodes.Text(
                    () => ModText.Get(ModStrings.Screens.CurrentTroop, BuildTroopLabel(hud.GetCurrentTroopInfo())));
                vtable.OnActivate = () => MoveCursorToTroop(hud.GetCurrentTroopId(), requireLocalCurrentTurn: false);
                builder.AddItem(new SyntheticNode(ControlId.Structural("combat:current-troop"), vtable));
            }

            GraphNodes.SyntheticButton(
                builder,
                "combat:ability",
                abilityDrawn,
                () => hud.AbilityButtonLabel,
                ActivateAbilityButton,
                hud.IsAbilityButtonEnabled,
                abilityDrawn ? hud.AbilityButtonTooltip : null,
                hud.FocusAbilityButton);
            GraphNodes.SyntheticButton(
                builder,
                "combat:cancel-ability",
                cancelAbilityDrawn,
                () => ModText.Get(ModStrings.Screens.CancelAbility),
                ActivateCancelAbilityButton,
                hud.IsCancelAbilityButtonEnabled,
                cancelAbilityDrawn ? hud.CancelAbilityButtonTooltip : null,
                hud.FocusCancelAbilityButton);

            builder.PopContext();
        }

        /// <summary>Using an ability puts the game into targeting, and the target is a place on the
        /// board, so the cursor goes there.</summary>
        private void ActivateAbilityButton()
        {
            if (Live == null || !Live.Hud.ClickAbilityButton())
            {
                return;
            }

            LandOnBoardWhileAiming();
        }

        private void ActivateCancelAbilityButton()
        {
            if (Live == null || !Live.Hud.ClickCancelAbilityButton())
            {
                return;
            }

            Navigator?.FocusNode(BoardNodeId);
        }

        // ---- the turn order ----

        /// <summary>The queue the game draws along the bottom, in its drawn order, with the round
        /// separators as lines of their own. Enter on a troop walks the cursor to it.</summary>
        private void BuildTurnOrder(GraphBuilder builder, BattleHudAdapter hud)
        {
            IReadOnlyList<BattleHudAdapter.QueueItem> items = hud.GetQueueItems();
            if (items.Count == 0)
            {
                return;
            }

            builder.BeginStop(TurnOrderStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.TurnOrder));
            for (int i = 0; i < items.Count; i++)
            {
                BattleHudAdapter.QueueItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                ControlId id = QueueNodeId(i);
                NodeVtable vtable = GraphNodes.Text(
                    () => BuildQueueItemLabel(item),
                    null,
                    item.IsRoundMarker ? null : item.Tooltip);
                if (!item.IsRoundMarker)
                {
                    vtable.OnActivate = () => MoveCursorToTroop(item.TroopId, requireLocalCurrentTurn: false);
                    vtable.OnFocusVisual = item.Focus;
                    vtable.OnBlurVisual = item.Unfocus;
                }

                builder.AddItem(new SyntheticNode(id, vtable));
                if (i == 0)
                {
                    builder.LandStopOn(id);
                }
            }

            builder.PopContext();
        }

        private static ControlId QueueNodeId(int index)
        {
            return ControlId.Structural(QueueKeyPrefix + index);
        }

        private static string BuildQueueItemLabel(BattleHudAdapter.QueueItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return item.IsRoundMarker ? ModText.Get(ModStrings.Screens.Round, item.RoundNumber) : BuildTroopLabel(item.Troop);
        }

        private static string BuildTroopLabel(BattleHudAdapter.TroopInfo troop)
        {
            if (troop == null || !troop.IsKnown || string.IsNullOrWhiteSpace(troop.Name))
            {
                return string.Empty;
            }

            string label;
            if (troop.HasSize)
            {
                label = troop.IsEnemy
                    ? ModText.Get(ModStrings.Combat.EnemyTroop, troop.Size, troop.Name)
                    : ModText.Get(ModStrings.Combat.TroopQuantity, troop.Size, troop.Name);
            }
            else
            {
                label = troop.Name;
            }

            return troop.HasPosition
                ? ModText.Get(ModStrings.Combat.TroopAt, label, CombatText.FormatPoint(troop.Position))
                : label;
        }

        // ---- the two buttons in the top right ----

        private void BuildMenu(GraphBuilder builder, BattleHudAdapter hud)
        {
            ChatAdapter chat = Chat;
            bool chatDrawn = chat != null && chat.IsButtonVisible();
            bool menuDrawn = hud.IsOptionsButtonVisible();
            if (!chatDrawn && !menuDrawn)
            {
                return;
            }

            builder.BeginStop(MenuStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Menu));
            GraphNodes.SyntheticButton(
                builder,
                "combat:chat",
                chatDrawn,
                () => ChatButtonText.Label(chat),
                () => chat.Open(),
                () => chat.IsButtonEnabled(),
                chatDrawn ? chat.ButtonTooltip : null,
                () => chat.FocusButton());
            GraphNodes.SyntheticButton(
                builder,
                "combat:game-menu",
                menuDrawn,
                () => hud.OptionsButtonLabel,
                () => hud.ClickOptionsButton(),
                hud.IsOptionsButtonEnabled,
                menuDrawn ? hud.OptionsButtonTooltip : null,
                hud.FocusOptionsButton);
            builder.PopContext();
        }

        /// <summary>The chat the game has up, found by <see cref="ChatSource"/> and memoised there
        /// on the set of loaded scenes, so a battle without chat costs a comparison a frame.</summary>
        private ChatAdapter Chat
        {
            get { return ChatSource.Current; }
        }

        // ---- the battle log ----

        private static void BuildBattleLog(GraphBuilder builder, BattleHudAdapter hud)
        {
            IReadOnlyList<string> entries = hud.GetBattleLogEntries();
            if (entries.Count == 0)
            {
                return;
            }

            builder.BeginStop(BattleLogStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.BattleLog));
            for (int i = 0; i < entries.Count; i++)
            {
                string entry = entries[i];
                NodeVtable vtable = GraphNodes.Text(() => entry);
                vtable.OnFocusVisual = hud.FocusBattleLog;
                vtable.OnBlurVisual = hud.UnfocusBattleLog;
                builder.AddItem(new SyntheticNode(ControlId.Structural(BattleLogKeyPrefix + i), vtable));
            }

            builder.PopContext();
        }

        // ---- End Turn ----

        /// <summary>The one button in the bottom right corner, a stop of its own with no name: the
        /// button says what it is.</summary>
        private static void BuildEndTurn(GraphBuilder builder, BattleHudAdapter hud)
        {
            if (!hud.IsEndTurnButtonVisible())
            {
                return;
            }

            builder.BeginStop(EndTurnStop);
            GraphNodes.SyntheticButton(
                builder,
                "combat:end-turn",
                true,
                () => hud.EndTurnButtonLabel,
                () => hud.ClickEndTurnButton(),
                hud.IsEndTurnButtonEnabled,
                hud.EndTurnButtonTooltip,
                hud.FocusEndTurnButton);
        }

        // ---- shared node plumbing ----

        // ---- keys ----

        /// <summary>
        /// The tile cursor's whole key set, while the board's node is the one the cursor is on. Asked
        /// BEFORE the navigator's own set, which is what makes the hex letters walk the board rather
        /// than start a search; on any other stop none of it is claimed.
        /// </summary>
        public override bool ModeClaims(string actionKey)
        {
            return IsBoardFocused() && Grid() != null && ModeAction(actionKey) != null;
        }

        /// <summary>The board action a key means here, or null where the key is not the cursor's. The
        /// mod's own combat and scanner keys answer for themselves; Space, Home, End and Backspace are
        /// TRANSLATED onto the acting-troop focus and the scanner's jump, distance and return, so the
        /// dev server's injections behave exactly as the physical keys the router resolves to those
        /// actions first. The arrows are NOT translated: a hex board has no north or south, and its
        /// six directions are the letters.</summary>
        private string ModeAction(string actionKey)
        {
            string translated = Translate(actionKey);
            return Grid().ClaimsAction(translated) ? translated : null;
        }

        private static string Translate(string actionKey)
        {
            if (actionKey == AccessibilityActions.UiCarry.Key)
            {
                return AccessibilityActions.CombatFocusActingTroop.Key;
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

            return actionKey;
        }

        public override bool OnAction(string actionKey)
        {
            if (!IsBoardFocused() || Grid() == null)
            {
                return false;
            }

            string action = ModeAction(actionKey);
            return action != null && Grid().HandleAction(AccessibilityActions.FindByKey(action));
        }

        /// <summary>Escape is the screen's wherever the cursor has left the board - it lands back on
        /// it - and on the board only while a sub-mode is on. Otherwise it is the game's, which is
        /// what opens the pause menu.</summary>
        public override bool ConsumesBack
        {
            get { return !IsBoardFocused() || (Grid() != null && Grid().IsInspecting) || IsAiming; }
        }

        public override bool Back()
        {
            if (IsBoardFocused())
            {
                return Grid() != null && Grid().HandleBack();
            }

            Navigator?.FocusNode(BoardNodeId);
            NativeSoundUtility.PostEvent(ReturnToGridSoundKey);
            return true;
        }

        private bool IsBoardFocused()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null
                && ReferenceEquals(navigator.Screen, this)
                && BoardNodeId.Equals(navigator.FocusedKey);
        }

        /// <summary>Whether this is the page the player is on: the navigator is handed the screen the
        /// manager has made current, so a menu over the battlefield reads as not it.</summary>
        private bool IsCurrentScreen
        {
            get
            {
                GraphNavigator navigator = Navigator;
                return navigator != null && ReferenceEquals(navigator.Screen, this);
            }
        }

        private bool IsAiming
        {
            get { return Live != null && Live.GetTargetingMode() != CombatTargetingMode.None; }
        }

        private string InstructionText
        {
            get
            {
                return Live != null && Live.Hud != null
                    ? Live.Hud.GetTargetingInstructionText(Live.GetTargetingMode())
                    : null;
            }
        }

        // ---- the cursor, driven from elsewhere ----

        /// <summary>The narration's "a new turn has begun": the cursor goes to the troop, but only
        /// while the battlefield is the page the player is on - a pause menu or a popup over it keeps
        /// its own cursor, as it did when the narrator looked the screen up itself.</summary>
        private void HandleActingTroopFocusRequest(int troopId)
        {
            if (IsCurrentScreen)
            {
                MoveCursorToLocalActingTroop(troopId);
            }
        }

        public void MoveCursorToLocalActingTroop(int troopId)
        {
            MoveCursorToTroop(troopId, requireLocalCurrentTurn: true);
        }

        public bool MoveCursorToTroop(int troopId, bool requireLocalCurrentTurn)
        {
            Vector2Int position;
            if (Live == null
                || Grid() == null
                || !Live.TryGetTroopPosition(troopId, out position, requireLocalCurrentTurn))
            {
                return false;
            }

            // The landing puts the cursor on the board and reads the tile, both through
            // ReadBoardTile: nothing is focused or said from here.
            return Grid().MoveToTroop(position);
        }

        /// <summary>
        /// The board's tile cursor has landed on another tile, and the readout is deliberately the
        /// NAVIGATOR's rather than the grid's. The whole board is one node, so a step inside it
        /// moves no focus and the engine would say nothing on its own; a tile said in the grid
        /// instead would be the bare label, without the dossier the "read long tooltips" setting
        /// asks for and without the tile's usage hints - which is why a step onto a troop used to
        /// say less than pressing Escape back onto that very tile.
        /// </summary>
        private void ReadBoardTile()
        {
            GraphNavigator navigator = Navigator;
            if (navigator == null || !ReferenceEquals(navigator.Screen, this))
            {
                return;
            }

            if (IsBoardFocused())
            {
                navigator.ReannounceFocused();
                return;
            }

            // The cursor was walked from a HUD stop (the queue's Enter, a troop cycle, the
            // turn-start narration): the landing on the board is what reads the tile, once - an
            // announcing one, because a silent landing plus a re-announce would read nothing.
            navigator.FocusNode(BoardNodeId);
        }

        public bool CanNavigateLocalActingTroops()
        {
            return HasActingTroops(enemy: false);
        }

        public bool CanNavigateEnemyActingTroops()
        {
            return HasActingTroops(enemy: true);
        }

        public bool FocusActingTroop()
        {
            SyncTroopCyclesWithCurrent();
            IReadOnlyList<int> troopIds = GetActingTroopIds(enemy: false);
            CombatTroopCycleResult result = _localActingTroopCycle.AnchorFirst(troopIds);
            return ApplyCycleResult(_localActingTroopCycle, result);
        }

        public bool NavigateLocalActingTroop(int delta)
        {
            return NavigateActingTroop(_localActingTroopCycle, enemy: false, delta: delta);
        }

        public bool NavigateEnemyActingTroop(int delta)
        {
            return NavigateActingTroop(_enemyActingTroopCycle, enemy: true, delta: delta);
        }

        /// <summary>T from the board: the turn order's first line, which is the next troop to act.
        /// </summary>
        public bool FocusTimeline()
        {
            BattleHudAdapter hud = Live != null ? Live.Hud : null;
            if (hud == null || hud.GetQueueItems().Count == 0)
            {
                return true;
            }

            Navigator?.FocusNode(QueueNodeId(0));
            return true;
        }

        public bool SummarizeResources()
        {
            string summary = BuildEssenceSummary(
                Live != null ? Live.GetLocalCombatHudSide() : null,
                requireVisible: false);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return false;
            }

            Speak(summary);
            return true;
        }

        public bool SummarizeEnemyResources()
        {
            Speak(BuildEssenceSummary(
                Live != null ? Live.GetEnemyCombatHudSide() : null,
                requireVisible: true));
            return true;
        }

        /// <summary>What a side's wielder is holding, essence by essence and only where there is any
        /// of it. The enemy's is answered only while the game is drawing their counters.</summary>
        private string BuildEssenceSummary(CombatHudSide? side, bool requireVisible)
        {
            BattleCommanderHudAdapter commanders = Live != null && Live.Hud != null ? Live.Hud.Commanders : null;
            if (commanders == null || !side.HasValue)
            {
                return string.Empty;
            }

            if (requireVisible && !commanders.IsEssenceMenuVisible(side.Value))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < EssenceRows.RowOrder.Length; i++)
            {
                EssenceType essence = EssenceRows.RowOrder[i];
                int amount = commanders.GetEssenceAmount(side.Value, essence);
                if (amount > 0)
                {
                    parts.Add(ModText.Get(
                        ModStrings.Common.PhraseSeparator,
                        EssenceText.Name(commanders.Localization, essence),
                        amount));
                }
            }

            return ModText.JoinListWithCommas(parts);
        }

        /// <summary>Put the cursor on the board without a word: the instruction the aiming state
        /// speaks is the readout that matters, not the tile it started on.</summary>
        private void LandOnBoardWhileAiming()
        {
            if (IsAiming)
            {
                Navigator?.FocusNode(BoardNodeId, announce: false);
            }
        }

        private bool HasActingTroops(bool enemy)
        {
            if (Live == null || !Live.IsLocalTurn())
            {
                return false;
            }

            IReadOnlyList<int> troopIds = GetActingTroopIds(enemy);
            return troopIds != null && troopIds.Count > 0;
        }

        private bool NavigateActingTroop(CombatTroopCycle cycle, bool enemy, int delta)
        {
            if (cycle == null)
            {
                return false;
            }

            SyncTroopCyclesWithCurrent();
            CombatTroopCycleResult result = cycle.Move(GetActingTroopIds(enemy), delta);
            return ApplyCycleResult(cycle, result);
        }

        private bool ApplyCycleResult(CombatTroopCycle cycle, CombatTroopCycleResult result)
        {
            if (!result.Moved)
            {
                return true;
            }

            bool moved = MoveCursorToTroop(result.TroopId, requireLocalCurrentTurn: false);
            if (!moved)
            {
                cycle?.Reset();
                return true;
            }

            if (result.Wrapped)
            {
                NativeSoundUtility.PostEvent(WrapCue.Key);
            }

            return true;
        }

        private IReadOnlyList<int> GetActingTroopIds(bool enemy)
        {
            if (Live == null || !Live.IsLocalTurn())
            {
                return new int[0];
            }

            return enemy ? Live.GetEnemyActingTroopIds() : Live.GetLocalActingTroopIds();
        }

        private void SyncTroopCyclesWithCurrent()
        {
            int currentTroopId = Live != null ? Live.GetCurrentTroopId() : -1;
            if (currentTroopId == _lastCycleCurrentTroopId)
            {
                return;
            }

            _lastCycleCurrentTroopId = currentTroopId;
            _localActingTroopCycle.Reset();
            _enemyActingTroopCycle.Reset();
        }

        // ---- the game's own signals ----

        /// <summary>A spell is being aimed. The cursor goes to the board without a word, because the
        /// adapter's narration has just read the instruction the board's stop is now named by.
        /// </summary>
        private void HandleSpellCastBegin()
        {
            Navigator?.FocusNode(BoardNodeId, announce: false);
            Grid()?.HandleTargetingBegin();
            _instruction = InstructionText;
        }

        private void HandleAbilityTargetingBegin(TroopAbilityTargeting targeting)
        {
            Navigator?.FocusNode(BoardNodeId, announce: false);
            Grid()?.HandleTargetingBegin();

            string instruction = Live != null
                ? NameAndInstruction(Live.GetCurrentAbilityName(), Live.GetAbilityTargetInstruction(targeting))
                : string.Empty;
            Live?.Hud.SetAbilityTargetInstructionText(instruction);
            Speak(instruction);

            _instruction = InstructionText;
        }

        /// <summary>The game asked for a spell's target: the spell's name and what it wants aimed at,
        /// as one line, kept for the board's stop name and said once.</summary>
        private void HandleSpellTargetInstruction(string spellName, string instruction)
        {
            string text = NameAndInstruction(spellName, instruction);
            Live?.Hud.SetSpellTargetInstructionText(text);
            Speak(text);
        }

        /// <summary>An aim that was already up when the battlefield took the cursor - a spell begun
        /// from a menu the page replaced - is read on arrival.</summary>
        private void AnnounceVisibleSpellTargetInstruction()
        {
            if (Live == null || Live.GetTargetingMode() != CombatTargetingMode.Spell)
            {
                return;
            }

            Speak(InstructionText);
        }

        /// <summary>What is being aimed and what the game wants aimed at, as one line; either alone
        /// where the other is missing.</summary>
        private static string NameAndInstruction(string name, string instruction)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(instruction))
            {
                return ModText.Get(ModStrings.UI.LabelValue, name, instruction);
            }

            return !string.IsNullOrWhiteSpace(name) ? name : instruction;
        }

        private void HandleAbilityTargetingEnd(bool usedAbility)
        {
            Live?.Hud.ClearAbilityTargetInstructionText();
            if (!usedAbility)
            {
                Speak(ModText.Get(ModStrings.Combat.AbilityCancelled));
            }

            _instruction = InstructionText;
        }

        /// <summary>Queued behind whatever the same keypress has already said, as everything the
        /// battlefield says is.</summary>
        private static void Speak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        /// <summary>The instruction the board's stop is named by, watched passively. The first
        /// instruction of an aim is spoken by whoever began it, so only a REPLACEMENT - a spell that
        /// asks for a second target - is announced here.</summary>
        private void WatchInstruction()
        {
            string text = InstructionText;
            if (string.Equals(text, _instruction, StringComparison.Ordinal))
            {
                return;
            }

            bool replaced = !string.IsNullOrWhiteSpace(_instruction) && !string.IsNullOrWhiteSpace(text);
            _instruction = text;
            if (replaced)
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        private void HandleAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            // A queue change needs no rebuild: the graph is declared afresh every frame.
            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null && !hudVisibility.IsVisible)
            {
                Navigator?.FocusNode(BoardNodeId);
            }
        }
    }
}
