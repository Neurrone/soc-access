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
    /// Up to four places to be: the tutorial button in the corner, the stored wielder's band where a
    /// wielder is stored, the menu itself, and the close cross.
    ///
    /// THE BAND HAS NO <c>WielderInteractHeader</c> here - the stored wielder's portrait and army
    /// hang off <c>DefencePanelWielder</c> instead - so the stop is built from those parts through
    /// the same contributor the other pages use (<c>ui/TroopHudRows.cs</c>), and there is no
    /// custom-name banner over it to read.
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
    public sealed class DefenceMenuScreen : GraphScreen
    {
        private const string TutorialStop = "defences-tutorial";
        private const string WielderStop = "defences-wielder";
        private const string PageStop = "defences-page";
        private const string CloseStop = "defences-close";
        private const string KeyPrefix = "defences";
        private const string WielderKey = "defences:wielder";
        private const string SettlementArmyKey = "defences:army";

        private readonly DefenceMenuAdapter _adapter;

        public DefenceMenuScreen(DefenceMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            DefenceMenu[] menus = Resources.FindObjectsOfTypeAll<DefenceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menus[i]);
                if (adapter.IsTopLevelPresent())
                {
                    return new DefenceMenuScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "defences"; }
        }

        /// <summary>The title and the subtitle the menu draws over the page, said once where the game
        /// has written the same text in both.</summary>
        public override string ScreenName
        {
            get
            {
                if (_adapter == null)
                {
                    return null;
                }

                string title = _adapter.Title;
                string subtitle = _adapter.Subtitle;
                if (string.IsNullOrWhiteSpace(subtitle) || SameText(title, subtitle))
                {
                    return string.IsNullOrWhiteSpace(title) ? null : title;
                }

                return string.IsNullOrWhiteSpace(title)
                    ? subtitle
                    : ModText.Get(ModStrings.Common.ListSeparator, title, subtitle);
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsTopLevelPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            BuildTutorial(builder);
            BuildStoredWielder(builder);

            builder.BeginStop(PageStop);
            BuildDraft(builder);
            BuildUpgrade(builder);
            SettlementNodes.WielderBand(builder, KeyPrefix, _adapter.DefendingWielder);
            BuildSettlementTroops(builder);
            BuildTowers(builder);
            SettlementNodes.SlotBands(
                builder,
                KeyPrefix,
                _adapter.TroopsPanel,
                _adapter.GetGarrisonSlots(),
                _adapter.GetBallistaSlots());

            builder.BeginStop(CloseStop);
            BuildClose(builder);
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
                DefencePanelWielderAdapter panel = _adapter == null ? null : _adapter.DefendingWielder;
                return panel == null || !panel.IsStoredWielderVisible ? null : panel.Troops;
            }
        }

        private TroopHudAdapter SettlementTroops
        {
            get { return _adapter == null ? null : _adapter.SettlementTroops; }
        }

        // ---- the tutorial ----

        private void BuildTutorial(GraphBuilder builder)
        {
            if (!_adapter.IsTutorialButtonVisible())
            {
                return;
            }

            builder.BeginStop(TutorialStop);
            SettlementNodes.Button(
                builder,
                _adapter.TutorialButton,
                "defences:tutorial",
                () => _adapter.GetTutorialButtonLabel(),
                () => _adapter.ActivateTutorial(),
                null,
                null,
                null);
        }

        // ---- the stored wielder's band ----

        /// <summary>The wielder stored in the settlement, as any other wielder band: the portrait row
        /// naming them, then their army as carry sources and targets.</summary>
        private void BuildStoredWielder(GraphBuilder builder)
        {
            DefencePanelWielderAdapter panel = _adapter.DefendingWielder;
            if (panel == null || !panel.IsStoredWielderVisible)
            {
                return;
            }

            TroopHudRows.WielderStop(
                builder,
                WielderStop,
                WielderKey,
                panel.Portrait,
                () => panel.StoredWielderName,
                null,
                panel.PortraitTooltip,
                panel.FocusPortrait,
                panel.Troops);
        }

        // ---- the two ways to get troops ----

        private void BuildDraft(GraphBuilder builder)
        {
            SettlementNodes.Button(
                builder,
                _adapter.DraftButton,
                "defences:draft",
                () => _adapter.DraftLabel,
                () => _adapter.ActivateDraft(),
                _adapter.IsDraftEnabled,
                _adapter.DraftTooltip,
                _adapter.FocusDraft);
        }

        /// <summary>The Upgrade button, which the game does not draw at all while nothing in the
        /// settlement can be upgraded (<c>DefencePanelTroops.HasUpgradableTroops</c>).</summary>
        private void BuildUpgrade(GraphBuilder builder)
        {
            SettlementNodes.Button(
                builder,
                _adapter.UpgradeButton,
                "defences:upgrade",
                () => _adapter.UpgradeLabel,
                () => _adapter.ActivateUpgrade(),
                _adapter.IsUpgradeEnabled,
                _adapter.UpgradeTooltip,
                _adapter.FocusUpgrade);
        }

        // ---- the settlement's own army ----

        /// <summary>The troops defending the settlement, under the game's own header for them, and the
        /// two buttons that move a whole army in or out of it.</summary>
        private void BuildSettlementTroops(GraphBuilder builder)
        {
            if (!_adapter.IsSettlementTroopsVisible())
            {
                return;
            }

            string caption = _adapter.DefendingTroopsLabel;
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(SettlementArmyKey);
            }

            TroopHudRows.Rows(builder, SettlementTroops, TroopHudRows.RowPrefix(SettlementArmyKey));
            SettlementNodes.Button(
                builder,
                _adapter.MoveToDefenceButton,
                "defences:move-to-defence",
                () => ModText.Get(ModStrings.Screens.MoveAllToDefence),
                () => _adapter.ActivateMoveToDefence(),
                _adapter.IsMoveToDefenceEnabled,
                _adapter.MoveToDefenceTooltip,
                _adapter.FocusMoveToDefence);
            SettlementNodes.Button(
                builder,
                _adapter.MoveToWielderButton,
                "defences:move-to-wielder",
                () => ModText.Get(ModStrings.Screens.MoveAllToWielder),
                () => _adapter.ActivateMoveToWielder(),
                _adapter.IsMoveToWielderEnabled,
                _adapter.MoveToWielderTooltip,
                _adapter.FocusMoveToWielder);

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

            IReadOnlyList<DefenceMenuAdapter.TowerItem> towers = _adapter.GetTowerItems();
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

            if (_adapter.HasVisibleTowerSummary())
            {
                AddLine(builder, "defences:towers/summary", () => _adapter.TowerSummary);
            }
            else if (_adapter.HasVisibleNoTowersHelp())
            {
                AddLine(builder, "defences:towers/help", () => _adapter.TowerInfoText);
            }

            builder.PopContext();
            builder.SetRegion(null);
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
            builder.AddItem(new DrawnNode(ControlId.For(close, "defences:close"), vtable, close));
        }

        // ---- shared ----

        private void AddLine(GraphBuilder builder, string key, System.Func<string> text)
        {
            Component panel = _adapter.TroopsPanel;
            if (panel == null)
            {
                return;
            }

            builder.AddItem(new DrawnNode(ControlId.Structural(key), GraphNodes.Text(text), panel));
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
