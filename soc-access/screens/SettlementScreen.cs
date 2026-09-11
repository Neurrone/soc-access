using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The landing page of a town or settlement a wielder has walked into. Four places to be, in the
    /// order the menu draws them: the tutorial button in the corner, the visiting wielder's band
    /// across the top, the page itself, and the close cross.
    ///
    /// THE PAGE is the drawn left column and then the defence panel on the right: the Draft and
    /// Upgrade buttons with the line the menu always writes under each of them read as their value
    /// (and, on Upgrade, the count the menu stamps on it while something can be upgraded); the
    /// defending wielder's band under the header the game draws over it; and the settlement's own
    /// troops, its garrison and its ballistae.
    ///
    /// THE TWO ARMIES ARE ONE CARRY: the visiting wielder's rows and the settlement's rows are the
    /// same troop rows (<c>ui/TroopHudRows.cs</c>), so Space picks a troop out of one and Enter drops
    /// it in the other through the game's own drag, which decides what a drop means - the split popup
    /// onto an empty slot, a swap onto a different troop. The two Move all buttons under the
    /// settlement's rows are the game's own mass moves; the game gives both the same tooltip ("Move as
    /// many as possible"), so the mod names them by the direction they move in and leaves the tooltip
    /// in the buffer.
    ///
    /// THE GARRISON AND THE BALLISTAE are read-only lines, not slots to be worked: the game wires no
    /// gesture at all to those entries.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu IS an
    /// <c>AdventureMenuBackground</c> with <c>_canClose</c> true, and <c>AnimateEntry</c> registers
    /// <c>UI.ExitMenu</c> on its own close outside any gamepad branch (measured 2026-09-07 in the
    /// decompiled source). The navigator claims the key only while something is being carried.
    ///
    /// The menu writes no title over the page other than the building's name and, on a banner over
    /// the wielder's portrait, whatever the place is called, so the screen is named after both.
    /// </summary>
    public sealed class SettlementScreen : LiveScreen<TownInteractionMenuAdapter>
    {
        private const string TutorialStop = "settlement-tutorial";
        private const string WielderStop = "settlement-wielder";
        private const string StoredWielderStop = "settlement-stored-wielder";
        private const string StoredWielderKey = "settlement/stored";
        private const string PageStop = "settlement-page";
        private const string CloseStop = "settlement-close";
        private const string KeyPrefix = "settlement";
        private const string WielderKey = "settlement:wielder";
        private const string SettlementArmyKey = "settlement:army";

        /// <summary>The one settlement window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<TownInteractionMenu> _source =
            ScreenSource<TownInteractionMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override TownInteractionMenuAdapter Adapt(object menu)
        {
            return new TownInteractionMenuAdapter((TownInteractionMenu)menu);
        }

        public override string Key
        {
            get { return "settlement"; }
        }

        /// <summary>Layer 20: the settlement landing page, under its own sub-pages.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The building the menu names at the top, and the name this particular place has
        /// where it has one of its own.</summary>
        public override string ScreenName
        {
            get
            {
                if (Live == null)
                {
                    return null;
                }

                string building = Live.Title;
                string custom = Live.IsCustomNameVisible ? Live.CustomName : null;
                if (string.IsNullOrWhiteSpace(custom) || SameText(building, custom))
                {
                    return string.IsNullOrWhiteSpace(building) ? null : building;
                }

                return string.IsNullOrWhiteSpace(building)
                    ? custom
                    : ModText.Get(ModStrings.Common.ListSeparator, building, custom);
            }
        }

        /// <summary>A dialog or a sub-page covers the settlement rather than closing it, and the cursor comes back to the control that opened it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildTutorial(builder);
            TroopHudRows.WielderStop(builder, WielderStop, WielderKey, Live.Wielder);
            BuildStoredWielder(builder);

            builder.BeginStop(PageStop);
            BuildDraft(builder);
            BuildUpgrade(builder);
            SettlementNodes.WielderBand(builder, KeyPrefix, Live.DefendingWielder);
            BuildSettlementTroops(builder);
            SettlementNodes.SlotBands(
                builder,
                KeyPrefix,
                Live.TroopsPanel,
                Live.GetGarrisonSlots(),
                Live.GetBallistaSlots());

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on whichever army's rows the cursor is on.
        /// </summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, VisitingTroops, WielderKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, StoredWielderTroops, StoredWielderKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, SettlementTroops, SettlementArmyKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, VisitingTroops, WielderKey)
                || TroopHudRows.OnAction(actionKey, Navigator, StoredWielderTroops, StoredWielderKey)
                || TroopHudRows.OnAction(actionKey, Navigator, SettlementTroops, SettlementArmyKey);
        }

        private TroopHudAdapter VisitingTroops
        {
            get
            {
                WielderInteract wielder = Live == null ? null : Live.Wielder;
                return wielder == null ? null : wielder.Troops;
            }
        }

        private TroopHudAdapter SettlementTroops
        {
            get { return Live == null ? null : Live.SettlementTroops; }
        }

        // ---- the tutorial ----

        /// <summary>The button the game draws in the corner until the player has seen the town
        /// tutorial, and never again.</summary>
        /// <summary>The wielder stored in the settlement, when one is: the panel draws their portrait
        /// and their army, and the game's drag reaches that army, so it is a Wielder stop of its own
        /// as it is on the defence menu.</summary>
        private void BuildStoredWielder(GraphBuilder builder)
        {
            DefencePanelWielderAdapter panel = Live.DefendingWielder;
            if (panel == null || !panel.IsStoredWielderVisible)
            {
                return;
            }

            TroopHudRows.WielderStop(
                builder,
                StoredWielderStop,
                StoredWielderKey,
                panel.Portrait,
                () => panel.StoredWielderName,
                panel.PortraitTooltip,
                panel.FocusPortrait,
                panel.Troops);
        }

        private TroopHudAdapter StoredWielderTroops
        {
            get
            {
                DefencePanelWielderAdapter panel = Live == null ? null : Live.DefendingWielder;
                return panel == null || !panel.IsStoredWielderVisible ? null : panel.Troops;
            }
        }

        private void BuildTutorial(GraphBuilder builder)
        {
            if (!Live.IsTutorialButtonVisible())
            {
                return;
            }

            builder.BeginStop(TutorialStop);
            SettlementNodes.Button(
                builder,
                Live.TutorialButton,
                "settlement:tutorial",
                () => Live.GetTutorialButtonLabel(),
                () => Live.ActivateTutorial(),
                null,
                null,
                null);
        }

        // ---- the two ways to get troops ----

        /// <summary>The Draft button, with the line the menu always writes under it as its value: what
        /// drafting here would do, or the game's own reason there is nothing to draft.</summary>
        private void BuildDraft(GraphBuilder builder)
        {
            Component button = Live.DraftButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.DraftLabel,
                () => Live.ActivateDraft(),
                Live.IsDraftEnabled,
                Live.DraftTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Live.DraftDescription));
            vtable.OnFocusVisual = () => Live.FocusDraft();
            builder.AddItem(new DrawnNode(ControlId.For(button, "settlement:draft"), vtable, button));
        }

        /// <summary>The Upgrade button, with its own line and the count the menu stamps on it while
        /// something can be upgraded. The game turns the button off when nothing can be.</summary>
        private void BuildUpgrade(GraphBuilder builder)
        {
            Component button = Live.UpgradeButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.UpgradeLabel,
                () => Live.ActivateUpgrade(),
                Live.IsUpgradeEnabled,
                Live.UpgradeTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Live.UpgradeDescription));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Live.UpgradesAvailableNumber));
            vtable.OnFocusVisual = () => Live.FocusUpgrade();
            builder.AddItem(new DrawnNode(ControlId.For(button, "settlement:upgrade"), vtable, button));
        }

        // ---- the settlement's own army ----

        /// <summary>The troops left behind in the settlement, and the two buttons that move a whole
        /// army in or out of it. Drawn only where the menu draws the rows at all, and named by the
        /// header the game writes over them, as the defence menu does.</summary>
        private void BuildSettlementTroops(GraphBuilder builder)
        {
            if (!Live.IsSettlementTroopsVisible())
            {
                return;
            }

            string caption = Live.DefendingTroopsLabel;
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(SettlementArmyKey);
            }

            TroopHudRows.Rows(builder, SettlementTroops, TroopHudRows.RowPrefix(SettlementArmyKey));
            SettlementNodes.Button(
                builder,
                Live.MoveToDefenceButton,
                "settlement:move-to-defence",
                () => ModText.Get(ModStrings.Screens.MoveAllToDefence),
                () => Live.ActivateMoveToDefence(),
                Live.IsMoveToDefenceEnabled,
                Live.MoveToDefenceTooltip,
                Live.FocusMoveToDefence);
            SettlementNodes.Button(
                builder,
                Live.MoveToWielderButton,
                "settlement:move-to-wielder",
                () => ModText.Get(ModStrings.Screens.MoveAllToWielder),
                () => Live.ActivateMoveToWielder(),
                Live.IsMoveToWielderEnabled,
                Live.MoveToWielderTooltip,
                Live.FocusMoveToWielder);

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        // ---- the close cross ----

        /// <summary>The cross on the wielder band, which is the one the menu itself listens to
        /// (<c>WielderInteractHeader.OnCloseButtonClicked</c>).</summary>
        private void BuildClose(GraphBuilder builder)
        {
            WielderInteract wielder = Live.Wielder;
            GraphNodes.DrawnClose(
                builder,
                "settlement:close",
                wielder == null ? null : wielder.CloseButton,
                () => wielder.IsCloseVisible,
                () => wielder.ActivateClose());
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                System.StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
