using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The defence menu of a settlement the player owns, opened without walking a wielder into it.
    /// Up to five places to be: the tutorial button in the corner, the stored wielder's portrait and
    /// their army where a wielder is stored, the menu itself, and the close cross.
    ///
    /// THE BAND HAS NO <c>WielderInteractHeader</c> here - the stored wielder's portrait and army
    /// hang off <c>DefencePanelWielder</c> instead - so the stops are built from those parts through
    /// the same contributor the other pages use (<c>ui/TroopHudRows.cs</c>), and there is no
    /// custom-name banner over it to read. The stored wielder's ARMY is a stop of its own, and Right
    /// from one of its rows crosses to the settlement's own army, Left comes back: the two are the
    /// halves of a troop exchange, and the captions over them say which side is which.
    ///
    /// THE MENU is the two ways to get troops, the defending wielder's band under the header the game
    /// draws over it, the settlement's own troops under the game's own header for them, the towers,
    /// and then the garrison and the ballistae. The game hides rather than disables what does not
    /// apply here: no Store and no Trade (there is no visiting wielder to store or trade with), no
    /// Upgrade button while nothing can be upgraded, and no Move all buttons while no wielder is
    /// stored to move an army to.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): <c>DefenceMenu.ShowTopLevel</c> registers
    /// <c>UI.ExitMenu</c> on <c>Hide</c> outside its gamepad branch (measured 2026-09-07 in the
    /// decompiled source). The navigator claims the key only while something is being carried.
    ///
    /// The menu writes its own title and subtitle over the page - the building and whatever the place
    /// is called - and writes the same text in both where the place has no name of its own, so the
    /// screen says it once.
    /// </summary>
    public sealed class DefenceMenuScreen : LiveScreen<DefenceMenuAdapter>
    {
        private const string TutorialStop = "defences-tutorial";
        private const string WielderStop = "defences-wielder";
        private const string WielderArmyStop = "defences-wielder-army";
        private const string PageStop = "defences-page";
        private const string CloseStop = "defences-close";
        private const string KeyPrefix = "defences";
        private const string WielderKey = "defences:wielder";
        private const string SettlementArmyKey = "defences:army";

        /// <summary>The one defence window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<DefenceMenu> _source =
            ScreenSource<DefenceMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override DefenceMenuAdapter Adapt(object menu)
        {
            return new DefenceMenuAdapter((DefenceMenu)menu);
        }

        public override string Key
        {
            get { return "defences"; }
        }

        /// <summary>Layer 20: the defence landing page, under its own sub-pages.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The title and the subtitle the menu draws over the page, said once where the game
        /// has written the same text in both.</summary>
        public override string ScreenName
        {
            get { return PlaceName; }
        }

        /// <summary>What this place is called - the page's own name, and what the settlement's army is
        /// named after.</summary>
        private string PlaceName
        {
            get
            {
                if (Live == null)
                {
                    return null;
                }

                string title = Live.Title;
                string subtitle = Live.Subtitle;
                return TroopHudRows.NameWithPlace(title, subtitle);
            }
        }

        /// <summary>A sub-page covers the defence menu rather than closing it, and the cursor comes
        /// back to the control that opened it - but only then. Read from the game at the pop, which is
        /// the same tick the defence panel went away: the two places that deactivate it
        /// (<c>DefenceMenu.HandlePurchaseTroopsClicked</c>, <c>HandleUpgradeTroopsClicked</c>) show
        /// the sub-page in the same call, so the menu still standing with one of its own pages drawn
        /// is what tells a cover apart from a close. The menu closing takes the cursor with it, so the
        /// next settlement starts at the top rather than on the slot the last one was left on.
        /// </summary>
        public override bool KeepStateOnPop
        {
            get { return Live != null && (Live.IsDraftPresent() || Live.IsUpgradePresent()); }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildTutorial(builder);
            BuildStoredWielder(builder);

            builder.BeginStop(PageStop);
            BuildDraft(builder);
            BuildUpgrade(builder);
            SettlementNodes.WielderBand(builder, KeyPrefix, Live.DefendingWielder);
            BuildSettlementTroops(builder);
            BuildTowers(builder);
            SettlementNodes.SlotBands(
                builder,
                KeyPrefix,
                Live.TroopsPanel,
                Live.GetGarrisonSlots(),
                Live.GetBallistaSlots());

            builder.BeginStop(CloseStop);
            BuildClose(builder);

            // Left and Right between the stored wielder's army and the settlement's own.
            TroopHudRows.ConnectArmies(
                builder,
                Navigator,
                StoredWielderTroops,
                WielderKey,
                SettlementTroops,
                SettlementArmyKey);
        }

        /// <summary>The game's Ctrl+digit quick splits, on whichever army's rows the cursor is on.
        /// </summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, StoredWielderTroops, WielderKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, SettlementTroops, SettlementArmyKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, StoredWielderTroops, WielderKey)
                || TroopHudRows.OnAction(actionKey, Navigator, SettlementTroops, SettlementArmyKey);
        }

        private TroopHudAdapter StoredWielderTroops
        {
            get
            {
                DefencePanelWielderAdapter panel = Live == null ? null : Live.DefendingWielder;
                return panel == null || !panel.IsStoredWielderVisible ? null : panel.Troops;
            }
        }

        private TroopHudAdapter SettlementTroops
        {
            get { return Live == null ? null : Live.SettlementTroops; }
        }

        // ---- the tutorial ----

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
                "defences:tutorial",
                () => Live.GetTutorialButtonLabel(),
                () => Live.ActivateTutorial(),
                null,
                null,
                null);
        }

        // ---- the stored wielder's band ----

        /// <summary>The wielder stored in the settlement, as any other wielder band: the portrait row
        /// naming them, then their army as carry sources and targets.</summary>
        private void BuildStoredWielder(GraphBuilder builder)
        {
            DefencePanelWielderAdapter panel = Live.DefendingWielder;
            if (panel == null || !panel.IsStoredWielderVisible)
            {
                return;
            }

            TroopHudRows.PortraitStop(
                builder,
                WielderStop,
                WielderKey,
                panel.Portrait,
                () => panel.StoredWielderName,
                panel.PortraitTooltip,
                panel.FocusPortrait);
            TroopHudRows.ArmyStop(
                builder,
                WielderArmyStop,
                WielderKey,
                panel.Troops,
                TroopHudRows.ArmySide(ModStrings.Screens.ArmyLeft, panel.StoredWielderName));
        }

        // ---- the two ways to get troops ----

        private void BuildDraft(GraphBuilder builder)
        {
            SettlementNodes.Button(
                builder,
                Live.DraftButton,
                "defences:draft",
                () => Live.DraftLabel,
                () => Live.ActivateDraft(),
                Live.IsDraftEnabled,
                Live.DraftTooltip,
                Live.FocusDraft);
        }

        /// <summary>The Upgrade button, which the game does not draw at all while nothing in the
        /// settlement can be upgraded (<c>DefencePanelTroops.HasUpgradableTroops</c>).</summary>
        private void BuildUpgrade(GraphBuilder builder)
        {
            SettlementNodes.Button(
                builder,
                Live.UpgradeButton,
                "defences:upgrade",
                () => Live.UpgradeLabel,
                () => Live.ActivateUpgrade(),
                Live.IsUpgradeEnabled,
                Live.UpgradeTooltip,
                Live.FocusUpgrade);
        }

        // ---- the settlement's own army ----

        /// <summary>The troops defending the settlement, under the game's own header for them, and the
        /// two buttons that move a whole army in or out of it. The rows only where the menu draws them
        /// (a settlement without troop storage has none); the buttons whenever the game draws them,
        /// which it decides on its own.</summary>
        private void BuildSettlementTroops(GraphBuilder builder)
        {
            bool rowsDrawn = Live.IsSettlementTroopsVisible();
            string caption = rowsDrawn
                ? ModText.Get(ModStrings.Screens.SettlementArmyRight, PlaceName)
                : null;
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(SettlementArmyKey);
            }

            if (rowsDrawn)
            {
                TroopHudRows.Rows(builder, SettlementTroops, TroopHudRows.RowPrefix(SettlementArmyKey));
            }

            SettlementNodes.Button(
                builder,
                Live.MoveToDefenceButton,
                "defences:move-to-defence",
                () => ModText.Get(ModStrings.Screens.MoveAllToDefence),
                () => Live.ActivateMoveToDefence(),
                Live.IsMoveToDefenceEnabled,
                Live.MoveToDefenceTooltip,
                Live.FocusMoveToDefence);
            SettlementNodes.Button(
                builder,
                Live.MoveToWielderButton,
                "defences:move-to-wielder",
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

        // ---- the towers ----

        /// <summary>One line per tower the settlement has built, named by the header of the tooltip
        /// the game draws for it, and then the line summing the tiers up - or, where there are no
        /// towers at all, the game's own line saying what towers would do.</summary>
        private void BuildTowers(GraphBuilder builder)
        {
            builder.PushContext(ModText.Get(ModStrings.Screens.Towers));
            builder.SetRegion("defences:towers");

            IReadOnlyList<DefenceMenuAdapter.TowerItem> towers = Live.GetTowerItems();
            for (int i = 0; i < towers.Count; i++)
            {
                DefenceMenuAdapter.TowerItem it = towers[i];
                Component source = it.Source;
                if (source == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Text(() => it.Label, null, it.Tooltip);
                vtable.OnFocusVisual = () => it.Focus();
                builder.AddItem(new DrawnNode(
                    ControlId.For(source, "defences:tower/" + i),
                    vtable,
                    source));
            }

            if (Live.HasVisibleTowerSummary())
            {
                AddLine(builder, "defences:towers/summary", () => Live.TowerSummary);
            }
            else if (Live.HasVisibleNoTowersHelp())
            {
                AddLine(builder, "defences:towers/help", () => Live.TowerInfoText);
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.DrawnClose(
                builder,
                "defences:close",
                Live.CloseButton,
                Live.IsCloseVisible,
                () => Live.ActivateClose());
        }

        // ---- shared ----

        private void AddLine(GraphBuilder builder, string key, System.Func<string> text)
        {
            Component panel = Live.TroopsPanel;
            if (panel == null)
            {
                return;
            }

            builder.AddItem(new DrawnNode(ControlId.Structural(key), GraphNodes.Text(text), panel));
        }
    }
}
