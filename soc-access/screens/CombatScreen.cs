using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.InputManagement;
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
using Zenject;

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
    /// cursor moves the navigator never announces a move; the grid says each landing itself, queued,
    /// and the review buffer refills because the node's readout changed.
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
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(BattleSceneInstaller), "Container");
        private static readonly EssenceType[] EssenceRowOrder =
        {
            EssenceType.Order,
            EssenceType.Creation,
            EssenceType.Chaos,
            EssenceType.Arcana,
            EssenceType.Destruction
        };

        private static string _lastProbeDiagnostic;

        private const string ReturnToGridSoundKey = "Common_ClosePauseMenu";
        private const string FocusWrapCueKey = "Common_ClickUnfold";

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
        private readonly CombatTroopCycle _localActingTroopCycle = new CombatTroopCycle();
        private readonly CombatTroopCycle _enemyActingTroopCycle = new CombatTroopCycle();
        private int _lastCycleCurrentTroopId = -1;
        private Action<TroopAbilityTargeting> _abilityTargetingBeginHandler;

        // The chat adapter this battle draws, kept while it is the one the game still draws: the
        // patch answers from the references its own hooks keep, so asking again costs a field read.
        private ChatAdapter _chat;

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
        private int _tooltipTurn;
        private Tooltip _tooltip;
        private bool _tooltipRead;

        // The targeting instruction, watched passively: baselined wherever it is spoken, so only an
        // instruction the game REPLACED under a still cursor is announced.
        private string _instruction;

        /// <summary>After a hot reload: point the slot at the battle already installed.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<CombatScreen>(FindActive());
        }

        /// <summary>The battle the game has installed and made ready, or null. The one scan.</summary>
        public static CombatAdapter FindActive()
        {
            return FindActiveCombatScreen();
        }

        /// <summary>The cursor is built over one battle: a new one gets a new grid.</summary>
        private CombatHexGrid Grid()
        {
            if (_grid != null && ReferenceEquals(_gridAdapter, Live))
            {
                return _grid;
            }

            _gridAdapter = Live;
            _grid = Live == null ? null : new CombatHexGrid(Live);
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

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
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
            Live?.AttachSpellTargetingNarration();
            _abilityTargetingBeginHandler = HandleAbilityTargetingBegin;
            Live?.AttachAbilityTargetingBegin(_abilityTargetingBeginHandler);
            Live?.AttachAbilityTargetingEnd(HandleAbilityTargetingEnd);
            Live?.AnnounceVisibleSpellTargetInstruction();
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
            Live?.DetachAbilityTargetingBegin(_abilityTargetingBeginHandler);
            Live?.DetachAbilityTargetingEnd();
            _abilityTargetingBeginHandler = null;
            Live?.Hud.ClearSpellTargetInstructionText();
            Live?.Hud.ClearAbilityTargetInstructionText();
            Live?.ClearNativeTooltip();
            Live?.ClearFocusedTileOverlay();
            base.OnPop();
        }

        /// <summary>A new battle is a new <see cref="Live"/>, and nothing read from the old one holds:
        /// the chat button is probed again for this one, and the tile tooltip's cache starts empty so
        /// the same coordinates in a new battle are not answered with the previous battle's tile.
        /// </summary>
        public override void OnLiveChanged(CombatAdapter previous)
        {
            _chat = null;
            _tooltip = null;
            _tooltipRead = false;
            // A new battle starts the troop cycles and the instruction baseline over: the first
            // acting troop of this battle may carry the last one's id.
            _lastCycleCurrentTroopId = -1;
            _localActingTroopCycle.Reset();
            _enemyActingTroopCycle.Reset();
            _instruction = null;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
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

            NodeVtable vtable = GraphNodes.Text(() => Grid().GetLabel(), null, TileTooltip());
            vtable.OnActivate = ConfirmTarget;
            vtable.OnContextual = ContextualTile;
            vtable.OnFocusVisual = () => Grid().ShowOverlay();
            vtable.OnBlurVisual = () => Grid().HideOverlay();
            TileInstructionHints.Add(vtable, TileTooltip);
            builder.AddItem(new SyntheticNode(BoardNodeId, vtable));
            builder.SetStart(BoardNodeId);

            builder.PopContext();
        }

        private string BoardContext()
        {
            string instruction = IsAiming ? InstructionText : null;
            return string.IsNullOrWhiteSpace(instruction) ? ModText.Get(ModStrings.UI.Battlefield) : instruction;
        }

        /// <summary>The cached tooltip, kept while everything it was composed from still reads the
        /// same: where the cursor stands, whether it is inspecting, what is being aimed, who stands on
        /// the tile and with how much health lost, and the battle's own turn counter - the generation
        /// under which reach and the moves left were true.</summary>
        private Tooltip TileTooltip()
        {
            Vector2Int tile = Grid().CursorTile;
            bool inspecting = Grid().IsInspecting;
            CombatTargetingMode targeting = Live.GetTargetingMode();
            int troopId;
            int troopHealthLost;
            Live.GetTileTroopState(tile, out troopId, out troopHealthLost);
            int turn = Live.GetCurrentTurn();
            if (_tooltipRead
                && tile == _tooltipTile
                && inspecting == _tooltipInspecting
                && targeting == _tooltipTargeting
                && troopId == _tooltipTroopId
                && troopHealthLost == _tooltipTroopHealthLost
                && turn == _tooltipTurn)
            {
                return _tooltip;
            }

            _tooltipTile = tile;
            _tooltipInspecting = inspecting;
            _tooltipTargeting = targeting;
            _tooltipTroopId = troopId;
            _tooltipTroopHealthLost = troopHealthLost;
            _tooltipTurn = turn;
            _tooltipRead = true;
            _tooltip = Grid().GetTooltip();
            return _tooltip;
        }

        /// <summary>Enter on the board. The game binds no confirm key in battle, so this only
        /// confirms the target of a spell or an ability being aimed and is otherwise silent.</summary>
        private void ConfirmTarget()
        {
            Grid().ConfirmTarget();
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
                    () => BuildQuickbarItemLabel(item),
                    () => ActivateQuickbarItem(item),
                    () => item.IsEnabled,
                    item.Tooltip);
                vtable.OnFocusVisual = item.Focus;
                vtable.OnBlurVisual = item.Unfocus;
                builder.AddItem(new SyntheticNode(ControlId.Structural(QuickbarKeyPrefix + item.Index), vtable));
            }

            builder.PopContext();
        }

        private string BuildQuickbarItemLabel(BattleHudAdapter.QuickbarItem item)
        {
            if (item == null || !item.HasSpell)
            {
                return string.Empty;
            }

            return item.SpellName + ", " + GameText.Get("Spells/Spellbook/SpellTierHeader", "tier " + item.SpellTier, item.SpellTier);
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

            AddButton(
                builder,
                key + "ai-control",
                aiDrawn,
                () => commanders.GetAiControlButtonLabel(side),
                () => commanders.ClickAiControlButton(side),
                () => commanders.IsAiControlButtonEnabled(side),
                aiDrawn ? commanders.GetAiControlButtonTooltip(side) : null,
                () => commanders.FocusAiControlButton(side));

            BuildEssences(builder, commanders, side, key, essencesDrawn);

            AddButton(
                builder,
                key + "spells",
                spellsDrawn,
                () => hud.SpellbookButtonLabel,
                () => hud.ClickSpellbookButton(),
                hud.IsSpellbookButtonEnabled,
                spellsDrawn ? hud.SpellbookButtonTooltip : null,
                hud.FocusSpellbookButton);
            AddButton(
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

            string caption = GameText.Get("Common/CommanderInventory/Essences", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
            }

            builder.SetRegion(key + "essences");
            for (int i = 0; i < EssenceRowOrder.Length; i++)
            {
                EssenceType essence = EssenceRowOrder[i];
                NodeVtable vtable = GraphNodes.Text(
                    () => commanders.GetEssenceLabel(side, essence),
                    null,
                    commanders.GetEssenceTooltip(side, essence));
                vtable.OnFocusVisual = () => commanders.FocusEssence(side, essence);
                builder.AddItem(new SyntheticNode(ControlId.Structural(key + "essence:" + essence), vtable));
            }

            builder.SetRegion(null);
            if (named)
            {
                builder.PopContext();
            }
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
                vtable.OnActivate = () => MoveCursorToTroop(hud.GetCurrentTroopId(), focusGrid: true, requireLocalCurrentTurn: false);
                builder.AddItem(new SyntheticNode(ControlId.Structural("combat:current-troop"), vtable));
            }

            AddButton(
                builder,
                "combat:ability",
                abilityDrawn,
                () => hud.AbilityButtonLabel,
                ActivateAbilityButton,
                hud.IsAbilityButtonEnabled,
                abilityDrawn ? hud.AbilityButtonTooltip : null,
                hud.FocusAbilityButton);
            AddButton(
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
                    vtable.OnActivate = () => MoveCursorToTroop(item.TroopId, focusGrid: true, requireLocalCurrentTurn: false);
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
                ? ModText.Get(ModStrings.Combat.TroopAt, label, CombatAdapter.FormatPoint(troop.Position))
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
            AddButton(
                builder,
                "combat:chat",
                chatDrawn,
                () => chat.ButtonLabel,
                () => chat.Open(),
                () => chat.IsButtonEnabled(),
                chatDrawn ? chat.ButtonTooltip : null,
                () => chat.FocusButton());
            AddButton(
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

        /// <summary>The chat adapter, asked for again whenever there is none or the button it wraps
        /// has gone - as the lobby's does. A battle without chat therefore costs a null field read a
        /// frame, and the chat window a later battle creates is picked up; the patch's own scene scan
        /// is behind ITS probed flags and runs once per mod load, not once per ask.</summary>
        private ChatAdapter Chat
        {
            get
            {
                if (_chat == null || _chat.Button == null)
                {
                    _chat = ChatPatches.CurrentAdapter;
                }

                return _chat;
            }
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
            AddButton(
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

        private static void AddButton(
            GraphBuilder builder,
            string key,
            bool drawn,
            Func<string> label,
            Action activate,
            Func<bool> enabled,
            Tooltip tooltip,
            Action focus)
        {
            if (!drawn)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(label, activate, enabled, tooltip);
            vtable.OnFocusVisual = focus;
            builder.AddItem(new SyntheticNode(ControlId.Structural(key), vtable));
        }

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
            get { return !IsBoardFocused() || Grid().IsInspecting || IsAiming; }
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

        private bool IsAiming
        {
            get { return Live != null && Live.GetTargetingMode() != CombatTargetingMode.None; }
        }

        private string InstructionText
        {
            get { return Live != null && Live.Hud != null ? Live.Hud.TargetingInstructionText : null; }
        }

        // ---- the cursor, driven from elsewhere ----

        public void MoveCursorToLocalActingTroop(int troopId)
        {
            MoveCursorToTroop(troopId, focusGrid: true, requireLocalCurrentTurn: true);
        }

        public bool MoveCursorToTroop(int troopId, bool focusGrid, bool requireLocalCurrentTurn)
        {
            Vector2Int position;
            if (Live == null
                || Grid() == null
                || !Live.TryGetTroopPosition(troopId, out position, requireLocalCurrentTurn))
            {
                return false;
            }

            bool moved = Grid().MoveToTroop(position);
            if (focusGrid && moved)
            {
                // Silent: the grid has just read the tile it landed on, which is the whole answer.
                Navigator?.FocusNode(BoardNodeId, announce: false);
            }

            return moved;
        }

        public bool CanFocusActingTroop()
        {
            return HasActingTroops(enemy: false);
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
            string summary = Live != null ? Live.BuildLocalEssenceSummary() : string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
            {
                return false;
            }

            SpeechPipeline.Output(new SpeechRequest(summary, interrupt: false));
            return true;
        }

        public bool SummarizeEnemyResources()
        {
            string summary = Live != null ? Live.BuildEnemyEssenceSummary() : string.Empty;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                SpeechPipeline.Output(new SpeechRequest(summary, interrupt: false));
            }

            return true;
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

            bool moved = MoveCursorToTroop(result.TroopId, focusGrid: true, requireLocalCurrentTurn: false);
            if (!moved)
            {
                cycle?.Reset();
                return true;
            }

            if (result.Wrapped)
            {
                NativeSoundUtility.PostEvent(FocusWrapCueKey);
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

            string instruction = Live != null ? Live.BuildAbilityTargetInstruction(targeting) : string.Empty;
            Live?.Hud.SetAbilityTargetInstructionText(instruction);
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                SpeechPipeline.Output(new SpeechRequest(instruction, interrupt: false));
            }

            _instruction = InstructionText;
        }

        private void HandleAbilityTargetingEnd(bool usedAbility)
        {
            Live?.Hud.ClearAbilityTargetInstructionText();
            if (!usedAbility)
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Combat.AbilityCancelled), interrupt: false));
            }

            _instruction = InstructionText;
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

        // ---- finding the battle ----

        private static CombatAdapter FindActiveCombatScreen()
        {
            BattleSceneInstaller[] installers = Resources.FindObjectsOfTypeAll<BattleSceneInstaller>();
            if (installers.Length == 0)
            {
                LogProbeDiagnostic("Combat probe found no BattleSceneInstaller instances");
                return null;
            }

            int liveInstallers = 0;
            for (int i = 0; i < installers.Length; i++)
            {
                BattleSceneInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                liveInstallers++;
                DiContainer container = GetContainer(installer);
                IClientBattleFacade facade = TryResolve<IClientBattleFacade>(container);
                IBattleCursorManager cursorManager = TryResolve<IBattleCursorManager>(container);
                IBattleGridManager gridManager = TryResolve<IBattleGridManager>(container);
                IBattlePathManager pathManager = TryResolve<IBattlePathManager>(container);
                IBattleHighlightManager highlightManager = TryResolve<IBattleHighlightManager>(container);
                IBattleViewManager battleViewManager = TryResolve<IBattleViewManager>(container);
                IBattleAttackPreviewHandler attackPreviewHandler = TryResolve<IBattleAttackPreviewHandler>(container);
                IBattleTooltipUtility tooltipUtility = TryResolve<IBattleTooltipUtility>(container);
                IInputManager inputManager = TryResolve<IInputManager>(container);
                ILocalizationHandler localization = TryResolve<ILocalizationHandler>(container);
                ICameraLookup cameraLookup = TryResolve<ICameraLookup>(container);
                IHumanBattleControllerFacade humanBattleController = TryResolve<IHumanBattleControllerFacade>(container);
                MouseKeyboardHumanBattleControllerModule mouseKeyboardInputModule = TryResolve<MouseKeyboardHumanBattleControllerModule>(container);
                IHumanBattleSpellController battleSpellController = TryResolve<IHumanBattleSpellController>(container);
                MouseKeyboardHumanBattleSpellModule mouseKeyboardSpellInputModule = TryResolve<MouseKeyboardHumanBattleSpellModule>(container);
                IBattleHudSignals battleHudSignals = TryResolve<IBattleHudSignals>(container);
                ISpellsLookup spellsLookup = TryResolve<ISpellsLookup>(container);
                ITroopAbilityUtility abilityUtility = TryResolve<ITroopAbilityUtility>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                CombatAdapter adapter = new CombatAdapter(
                    installer,
                    container,
                    facade,
                    cursorManager,
                    gridManager,
                    pathManager,
                    highlightManager,
                    battleViewManager,
                    attackPreviewHandler,
                    tooltipUtility,
                    inputManager,
                    localization,
                    cameraLookup,
                    cartographyConverter,
                    humanBattleController,
                    mouseKeyboardInputModule,
                    battleSpellController,
                    mouseKeyboardSpellInputModule,
                    battleHudSignals,
                    spellsLookup,
                    abilityUtility);
                if (adapter.IsPresent())
                {
                    CombatEventNarrator.SetActiveAdapter(adapter);
                    CombatEventNarrator.SyncCurrentTurnTroop(adapter);
                    LogProbeDiagnostic("Combat probe found ready battle");
                    return adapter;
                }

                LogProbeDiagnostic("Combat probe found installer but adapter is not ready: " + adapter.GetReadinessDiagnostic());
            }

            if (liveInstallers == 0)
            {
                LogProbeDiagnostic("Combat probe found " + installers.Length + " installer instances but none in a loaded scene");
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(BattleSceneInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(BattleSceneInstaller installer)
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
