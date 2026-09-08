using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The trade between two wielders standing next to each other. Seven places to be.
    ///
    /// THE STOP ORDER IS DELIBERATE AND IS NOT THE DRAWN ONE (owner ruling 2026-09-07): left Wielder,
    /// right Wielder, left Equipment, left Inventory, right Equipment, right Inventory, then the two
    /// modifier bars, then Close. The menu draws each side as one column - portrait, stats,
    /// modifiers, artifacts, army - so the drawn order would put a wielder band between the two
    /// backpacks, and every transfer would then cross it. Putting the two wielder bands first and the
    /// four artifact stops together means a carry from one backpack to the other is one Tab away.
    ///
    /// A WIELDER STOP HERE IS THE SHEET'S SHAPE, because the menu draws the sheet's own parts: the
    /// portrait row, the stats band (through <c>ui/CommanderBands.cs</c>, shared with the sheet), the
    /// army rows (<c>ui/TroopHudRows.cs</c>), and then the side's Move all button. Each side's
    /// modifier bar, with the showing tab's lines under it, is a stop of its own after the artifact
    /// stops, as the sheet's is: a stop lands on the alternative in force, so a bar inside the wielder
    /// stop made Tab land on the showing tab instead of the wielder. Locked troop slots are not
    /// there to find: the menu builds both bars with <c>hideLockedSlots</c>, so only drawn slots are
    /// rows. Enter on a modifier tab switches BOTH sides, which is what the menu's own tab control
    /// (<c>TradingMenu.HandleSwitchTab</c>) does.
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
    ///
    /// ONE CONSEQUENCE OF THE ONE-STOP WIELDER BAND, measured 2026-09-07 and left as the model has it:
    /// a stop with no remembered position lands on whichever alternative of a set is in force, so Tab
    /// into a wielder stop the player has not stood in yet lands on the modifier tab that is showing
    /// rather than on the wielder. The sheet avoids this by giving its modifier bar a stop of its own;
    /// here the bar is inside the band, which is what makes the band one stop instead of two.
    /// </summary>
    public sealed class TradingScreen : GraphScreen
    {
        private const string LeftWielderStop = "trade-left-wielder";
        private const string RightWielderStop = "trade-right-wielder";
        private const string LeftEquipmentStop = "trade-left-equipment";
        private const string LeftInventoryStop = "trade-left-inventory";
        private const string RightEquipmentStop = "trade-right-equipment";
        private const string RightInventoryStop = "trade-right-inventory";
        private const string LeftModifiersStop = "trade-left-modifiers";
        private const string RightModifiersStop = "trade-right-modifiers";
        private const string CloseStop = "trade-close";
        private const string LeftKey = "trade:left";
        private const string RightKey = "trade:right";

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(TradingMenuInstaller), "Container");

        private readonly TradingMenuAdapter _adapter;

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the band lines and the auto-arrange buttons are not drawn as
        // controls of their own.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public TradingScreen(TradingMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TradingMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<TradingMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                TradingMenu menu = TryResolveTradingMenu(installers[i]);
                TradingMenuAdapter adapter = new TradingMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new TradingScreen(adapter);
                }
            }

            return null;
        }

        public TradingMenuAdapter Adapter
        {
            get { return _adapter; }
        }

        public override string Key
        {
            get { return "trading"; }
        }

        /// <summary>The two wielders the menu is about, in the order it draws them: the menu writes no
        /// title over them.</summary>
        public override string ScreenName
        {
            get
            {
                if (_adapter == null)
                {
                    return null;
                }

                string left = _adapter.Left.CommanderName;
                string right = _adapter.Right.CommanderName;
                if (string.IsNullOrWhiteSpace(left))
                {
                    return string.IsNullOrWhiteSpace(right) ? null : right;
                }

                return string.IsNullOrWhiteSpace(right)
                    ? left
                    : ModText.Get(ModStrings.Common.ListSeparator, left, right);
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it whenever the menu is reopened over itself.
        /// The graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            ArtifactSlotNodes.RegisterSounds();

            BuildWielder(builder, LeftWielderStop, LeftKey, _adapter.Left);
            BuildWielder(builder, RightWielderStop, RightKey, _adapter.Right);

            builder.BeginStop(LeftEquipmentStop);
            ArtifactSlotNodes.Equipment(builder, _adapter.Left, LeftKey, AddSlotHints);

            builder.BeginStop(LeftInventoryStop);
            ArtifactSlotNodes.Inventory(builder, _adapter.Left, LeftKey, AddSlotHints, Marker("left/auto-arrange"));

            builder.BeginStop(RightEquipmentStop);
            ArtifactSlotNodes.Equipment(builder, _adapter.Right, RightKey, AddSlotHints);

            builder.BeginStop(RightInventoryStop);
            ArtifactSlotNodes.Inventory(builder, _adapter.Right, RightKey, AddSlotHints, Marker("right/auto-arrange"));

            // The modifier bars last, each a stop of its own as the sheet's is: a stop lands on the
            // alternative in force, so a bar inside the wielder stop made Tab land on the showing tab
            // instead of the wielder. Read-only, so they sit after everything a transfer needs.
            builder.BeginStop(LeftModifiersStop);
            BuildModifiers(builder, LeftKey, _adapter.Left);

            builder.BeginStop(RightModifiersStop);
            BuildModifiers(builder, RightKey, _adapter.Right);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on whichever side's rows the cursor is on.
        /// </summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, Troops(_adapter?.Left), LeftKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, Troops(_adapter?.Right), RightKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, Troops(_adapter?.Left), LeftKey)
                || TroopHudRows.OnAction(actionKey, Navigator, Troops(_adapter?.Right), RightKey);
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
            BuildPortrait(builder, keyPrefix, side);
            BuildStats(builder, keyPrefix, side);
            BuildTroops(builder, keyPrefix, side);
            BuildMoveAll(builder, keyPrefix, side);
        }

        /// <summary>The wielder, as the menu draws them over their column: their name and the level on
        /// their portrait, with the stats the game draws on the portrait behind both in the buffer.
        /// </summary>
        private void BuildPortrait(GraphBuilder builder, string keyPrefix, TradingMenuAdapter.Side side)
        {
            Component portrait = side.Portrait;
            if (portrait == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(() => side.CommanderName, null, side.PortraitTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(
                () => ModText.Get(ModStrings.Screens.LevelValue, side.Level)));
            vtable.OnFocusVisual = () => side.FocusPortrait();
            builder.AddItem(new DrawnNode(
                ControlId.For(portrait, keyPrefix + "/portrait"),
                vtable,
                portrait));
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
        /// under the title the game writes over them. Enter switches BOTH sides, as the menu's own tab
        /// control does; arriving only selects, since Up from the first line lands on the bar.
        /// </summary>
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

            CommanderBands.Tabs(
                builder,
                keyPrefix,
                tabs,
                side.GetActiveModifierCategoryIndex,
                index => _adapter.ActivateModifierCategory(index),
                index => side.SelectModifierCategory(index));

            CommanderBands.Band(
                builder,
                keyPrefix,
                "modifiers",
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
            Component close = _adapter.CloseButton;
            if (close == null || !_adapter.IsCloseVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "trade:close"), vtable, close));
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

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }

        private static TradingMenu TryResolveTradingMenu(TradingMenuInstaller installer)
        {
            if (installer == null || installer.gameObject == null || !installer.gameObject.scene.IsValid() || !installer.gameObject.scene.isLoaded)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty != null
                ? InstallerContainerProperty.GetValue(installer, null) as DiContainer
                : null;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<TradingMenu>();
            }
            catch
            {
                return null;
            }
        }
    }
}
