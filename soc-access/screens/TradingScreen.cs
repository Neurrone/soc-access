using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The trade between two wielders standing next to each other. Seven places to be.
    ///
    /// THE STOP ORDER IS DELIBERATE AND IS NOT THE DRAWN ONE (owner ruling 2026-09-07): left Wielder,
    /// right Wielder, left Equipment, left Inventory, right Equipment, right Inventory, then Close.
    /// The menu draws each side as one column - portrait, stats, modifiers, artifacts, army - so the
    /// drawn order would put a wielder band between the two backpacks, and every transfer would then
    /// cross it. Putting the two wielder bands first and the four artifact stops together means a
    /// carry from one backpack to the other is one Tab away.
    ///
    /// A WIELDER STOP HERE IS THE SHEET'S SHAPE, because the menu draws the sheet's own parts, in the
    /// column's own order: the portrait row, the stats band (through <c>ui/CommanderBands.cs</c>,
    /// shared with the sheet), the modifiers, the army rows (<c>ui/TroopHudRows.cs</c>), and then the
    /// side's Move all button. THE STOP IS NAMED AFTER THE WIELDER (owner ruling 2026-09-08): the
    /// name is the stop's context, so Tab into it says who this column belongs to, and the portrait
    /// line under it says the level. The portrait is a region of its own, unnamed, so the region jump
    /// works from it as from anywhere else in the stop. The stop names the portrait as its landing
    /// (<c>GraphBuilder.LandStopOn</c>), because a stop that names none opens on the alternative in
    /// force and the showing modifier tab is one; the bar and that tab's lines are a region named
    /// Modifiers after the stats, which is what the region jump reaches them by. The four artifact
    /// stops are named after their owner too ("Cecilia's Equipment"), since a page with two
    /// backpacks on it cannot call either of them just Inventory. Locked troop slots
    /// are not there to find: the menu builds both bars with <c>hideLockedSlots</c>, so only drawn
    /// slots are rows. Enter on a modifier tab switches BOTH sides, which is what the menu's own tab
    /// control (<c>TradingMenu.HandleSwitchTab</c>) does.
    ///
    /// THE CLICKS ON AN ARTIFACT MEAN SOMETHING ELSE HERE than they do on the sheet, and the
    /// difference is the game's: <c>InventoryHUD.EquipArtifact</c> answers the right click with
    /// <c>MoveItemToOtherBackpack</c> when the other inventory is set. Enter is the left click (inert,
    /// and with Ctrl held the game's drop on the ground); Backslash moves the artifact across, and
    /// with Ctrl held destroys it. A carry ACROSS the sides goes down the game's give path, which
    /// <c>ArtifactDropUtility</c> already branches on the owner for, and a troop carried across is
    /// answered by the game's own <c>CanDropHere</c>, which applies its faction-mixing and partner
    /// rules.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the window IS an
    /// <c>AdventureMenuBackground</c> with a close cross, and <c>AnimateEntry</c> registers
    /// <c>UI.ExitMenu</c> on its own close outside any gamepad branch (measured 2026-09-07 in the
    /// decompiled source). The navigator claims the key only while something is being carried.
    ///
    /// The menu draws no title of its own, so the screen is named after the two wielders in it.
    /// </summary>
    public sealed class TradingScreen : LiveScreen<TradingMenuAdapter>
    {
        private const string LeftWielderStop = "trade-left-wielder";
        private const string RightWielderStop = "trade-right-wielder";
        private const string LeftEquipmentStop = "trade-left-equipment";
        private const string LeftInventoryStop = "trade-left-inventory";
        private const string RightEquipmentStop = "trade-right-equipment";
        private const string RightInventoryStop = "trade-right-inventory";
        private const string CloseStop = "trade-close";
        private const string LeftKey = "trade:left";
        private const string RightKey = "trade:right";

        // Each side's slot nodes, kept for as long as the adapter hands back the same slot list.
        // The nodes are closures that read the game when they are READ, so rebuilding them every
        // frame bought nothing but the allocation; the contributor owns the key and the rule
        // (ui/ArtifactSlotNodes.cs, Column). It is a field on the SCREEN because an adapter may hold
        // no graph concepts.
        private readonly ArtifactSlotNodes.Column _leftColumn = new ArtifactSlotNodes.Column();

        private readonly ArtifactSlotNodes.Column _rightColumn = new ArtifactSlotNodes.Column();

        /// <summary>The one trading window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<ITradingMenu> _source =
            ScreenSource<ITradingMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override TradingMenuAdapter Adapt(object menu)
        {
            return new TradingMenuAdapter((TradingMenu)menu);
        }

        public TradingMenuAdapter Adapter
        {
            get { return Live; }
        }

        public override string Key
        {
            get { return "trading"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The two wielders the menu is about, in the order it draws them: the menu writes no
        /// title over them.</summary>
        public override string ScreenName
        {
            get
            {
                if (Live == null)
                {
                    return null;
                }

                string left = Live.Left.CommanderName;
                string right = Live.Right.CommanderName;
                if (string.IsNullOrWhiteSpace(left))
                {
                    return string.IsNullOrWhiteSpace(right) ? null : right;
                }

                return string.IsNullOrWhiteSpace(right)
                    ? left
                    : ModText.Get(ModStrings.Common.ListSeparator, left, right);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            ArtifactSlotNodes.RegisterSounds();

            BuildWielder(builder, LeftWielderStop, LeftKey, Live.Left);
            BuildWielder(builder, RightWielderStop, RightKey, Live.Right);

            builder.BeginStop(LeftEquipmentStop);
            ArtifactSlotNodes.Equipment(builder, Live.Left, LeftKey, AddSlotHints, _leftColumn, Section(Live.Left, Live.Left.EquipmentLabel));

            builder.BeginStop(LeftInventoryStop);
            ArtifactSlotNodes.Inventory(builder, Live.Left, LeftKey, AddSlotHints, Marker("left/auto-arrange"), _leftColumn, Section(Live.Left, Live.Left.InventoryLabel));

            builder.BeginStop(RightEquipmentStop);
            ArtifactSlotNodes.Equipment(builder, Live.Right, RightKey, AddSlotHints, _rightColumn, Section(Live.Right, Live.Right.EquipmentLabel));

            builder.BeginStop(RightInventoryStop);
            ArtifactSlotNodes.Inventory(builder, Live.Right, RightKey, AddSlotHints, Marker("right/auto-arrange"), _rightColumn, Section(Live.Right, Live.Right.InventoryLabel));

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on whichever side's rows the cursor is on.
        /// </summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, Troops(Live?.Left), LeftKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, Troops(Live?.Right), RightKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, Troops(Live?.Left), LeftKey)
                || TroopHudRows.OnAction(actionKey, Navigator, Troops(Live?.Right), RightKey);
        }

        private static TroopHudAdapter Troops(TradingMenuAdapter.Side side)
        {
            return side == null ? null : side.Troops;
        }

        // ---- one side's wielder band ----

        /// <summary>One side, in the order the menu draws that side's column: who they are, their
        /// stats, their modifiers, their army, and the button that hands the whole army over.
        /// </summary>
        private void BuildWielder(GraphBuilder builder, string stop, string keyPrefix, TradingMenuAdapter.Side side)
        {
            builder.BeginStop(stop);
            builder.PushContext(side.CommanderName);
            ControlId portrait = BuildPortrait(builder, keyPrefix, side);
            BuildStats(builder, keyPrefix, side);
            BuildModifiers(builder, keyPrefix, side);
            BuildTroops(builder, keyPrefix, side);
            BuildMoveAll(builder, keyPrefix, side);
            builder.PopContext();

            if (portrait != null)
            {
                // The wielder, not the showing modifier tab: a stop that names no landing opens on
                // the alternative in force.
                builder.LandStopOn(portrait);
            }
        }

        /// <summary>The wielder's portrait, as its own unnamed region under the stop that carries
        /// their name: the level the menu draws on it, with the stats the game draws on the portrait
        /// behind it in the buffer.</summary>
        private ControlId BuildPortrait(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            Component portrait = side.Portrait;
            if (portrait == null)
            {
                return null;
            }

            builder.SetRegion(keyPrefix + ":portrait");
            NodeVtable vtable = GraphNodes.Text(
                () => ModText.Get(ModStrings.Screens.LevelValue, side.Level),
                null,
                side.PortraitTooltip);
            vtable.OnFocusVisual = () => side.FocusPortrait();
            ControlId id = ControlId.For(portrait, keyPrefix + "/portrait");
            builder.AddItem(new DrawnNode(id, vtable, portrait));
            builder.SetRegion(null);
            return id;
        }

        /// <summary>A section of the page named after the wielder it belongs to, in the game's own
        /// word for the section: "Cecilia's Equipment".</summary>
        private static string Section(TradingMenuAdapter.Side side, string caption)
        {
            return ModText.Get(
                ModStrings.Screens.WielderSection,
                ModText.FormatPossessiveName(side.CommanderName, ModStrings.Spatial.CommanderPossessive),
                caption);
        }

        private void BuildStats(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            CommanderBands.Band(
                builder,
                keyPrefix,
                "stats",
                GameText.Get("Common/CommanderInventory/Stats", string.Empty),
                Lines("stats", side.GetStats),
                Marker);
        }

        /// <summary>The three modifier tabs as the one bar the menu draws, then the showing tab's lines
        /// under the title the game writes over them - one region of the side's wielder stop, named
        /// Modifiers (<see cref="CommanderBands.Modifiers"/>). Enter switches BOTH sides, as the
        /// menu's own tab control does; arriving only selects, since Up from the first line lands on
        /// the bar.</summary>
        private void BuildModifiers(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            IReadOnlyList<TradingMenuAdapter.ModifierCategory> categories = Items(
                "modifier categories",
                side.GetModifierCategories);
            List<CommanderBands.TabItem> tabs = new List<CommanderBands.TabItem>();
            for (int i = 0; i < categories.Count; i++)
            {
                TradingMenuAdapter.ModifierCategory it = categories[i];
                tabs.Add(new CommanderBands.TabItem(it.Label, it.Index, it.Button, it.Tooltip));
            }

            CommanderBands.Modifiers(
                builder,
                keyPrefix,
                tabs,
                side.GetActiveModifierCategoryIndex,
                index => Live.ActivateModifierCategory(index),
                index => side.SelectModifierCategory(index),
                side.GetActiveModifierListLabel(),
                Lines("modifiers", side.GetActiveModifiers),
                Marker);
        }

        /// <summary>The side's army, under the game's own word for it.</summary>
        private void BuildTroops(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            string caption = GameText.Get("Commanders/Tooltip/Troops", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(keyPrefix + ":troops");
            }

            TroopHudRows.Rows(builder, side.Troops, TroopHudRows.RowPrefix(keyPrefix));

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>The button under the army that hands the whole of it over. The game turns it off
        /// when it would refuse the move (<c>CanMassMoveTroops</c>), so it is watched under a cursor
        /// waiting here.</summary>
        private void BuildMoveAll(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            Component button = side.MoveAllButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => side.MoveAllLabel,
                () => side.ActivateMoveAll(),
                side.IsMoveAllEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(
                ControlId.For(button, keyPrefix + ":move-all"),
                vtable,
                button));
        }

        // ---- the artifacts ----

        /// <summary>The three gestures an occupied slot has in a trade, in the order they are said.
        /// The left click is inert here, as it is on the sheet, so it says nothing.</summary>
        private void AddSlotHints(NodeVtable vtable, InventorySlotInfo slot)
        {
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactTradeHint,
                AccessibilityActions.UiRightClick.Key);
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactDestroyHint,
                AccessibilityActions.UiRightClick.Key,
                AccessibilityActions.UiRightClickCtrlBindingIndex);
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactDropHint,
                AccessibilityActions.UiLeftClick.Key,
                AccessibilityActions.UiLeftClickCtrlBindingIndex);
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.DrawnClose(
                builder,
                "trade:close",
                Live.CloseButton,
                Live.IsCloseVisible,
                () => Live.ActivateClose());
        }

        // ---- shared ----

        private static IReadOnlyList<CommanderBands.Line> Lines(
            string section,
            Func<IReadOnlyList<TradingMenuAdapter.LabeledItem>> getter)
        {
            IReadOnlyList<TradingMenuAdapter.LabeledItem> items = Items(section, getter);
            List<CommanderBands.Line> lines = new List<CommanderBands.Line>();
            for (int i = 0; i < items.Count; i++)
            {
                TradingMenuAdapter.LabeledItem it = items[i];
                lines.Add(new CommanderBands.Line(it.Label, it.Value, it.Tooltip));
            }

            return lines;
        }

        /// <summary>One section's items, or none where reading them threw: a part of the menu the game
        /// has stopped answering for costs its own rows and never the rest of the page.</summary>
        private static IReadOnlyList<T> Items<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("TradingScreen section " + section + " failed to build: " + exception);
                return new T[0];
            }
        }

    }
}
