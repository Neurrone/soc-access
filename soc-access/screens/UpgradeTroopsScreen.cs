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
    /// The page a wielder's own troops are upgraded on, over the landing page of the dwelling, the
    /// town or the defence menu.
    ///
    /// THE PAGE IS ONE EXPANDABLE GROUP PER UPGRADE - "Militia to Halberdiers" and how many of them
    /// there are - or, where there is nothing to upgrade, the one line the game writes instead.
    /// Under each group is everything the card draws for that trade: the two portraits, which the
    /// game wires as the shortcuts to none and to as many as can be afforded, the slider between
    /// them, and the button that pays for it.
    ///
    /// THE TWO SHORTCUTS MOVE THE SLIDER WITHOUT TELLING THE CARD (the game sets
    /// <c>UISlider.SliderValue</c>, which does not notify), so the amounts the card draws only catch
    /// up on its next recalculation. The amounts are read live and watched, so the cursor standing
    /// on them hears them arrive rather than reading a number the mod worked out itself.
    ///
    /// Escape, the tutorial button, the wielder's band, the back button and the close cross are the
    /// base's (<c>screens/TroopManagementScreenBase.cs</c>).
    ///
    /// Against the widget page it replaced (walked on the dwelling, the town and the defence menu
    /// 2026-09-08), nothing spoken was lost: the page's title is now the screen's name, the target
    /// troop's line ("Archers. 14") is the target portrait button with its amount as its value, the
    /// slider says "4 of 9" where it said "Amount to upgrade, Archers. 14", and the pay button says
    /// "Upgrade" with its price as the value instead of "Upgrade for 2100 Gold". The wielder's rows
    /// lost "slot 5": the graph says where a row sits.
    /// </summary>
    public sealed class UpgradeTroopsScreen : TroopManagementScreenBase
    {

        /// <summary>Where the upgrade cards' keys start, so the same prefix names them and finds the
        /// one the cursor is on.</summary>
        private string CardPrefix
        {
            get { return Key + ":upgrade/"; }
        }

        /// <summary>Layer 22: a sub-page over the settlement, dwelling or defence page.</summary>
        public override int Layer
        {
            get { return 22; }
        }

        protected override string ScreenSuffix { get { return "upgrade-troops"; } }

        protected override bool IsContentPresent(ITroopManagementHostAdapter host)
        {
            return host.IsUpgradePresent();
        }

        protected override void BuildContent(GraphBuilder builder)
        {
            UpgradeTroopsSubMenuAdapter subMenu = Host.UpgradeTroops;
            if (subMenu == null)
            {
                return;
            }

            if (subMenu.IsNoUpgradableTroopsVisible)
            {
                BuildNoUpgrades(builder, subMenu);
            }

            IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries = subMenu.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                BuildUpgrade(builder, entries[i], CardPrefix + i);
            }
        }

        /// <summary>The game's own line for a page with nothing on it: every troop already upgraded,
        /// or the buildings that would allow it not built yet.</summary>
        private void BuildNoUpgrades(GraphBuilder builder, UpgradeTroopsSubMenuAdapter subMenu)
        {
            Component container = subMenu.NoUpgradableTroopsContainer;
            if (container == null)
            {
                return;
            }

            builder.AddItem(new DrawnNode(
                ControlId.For(container, Key + ":no-upgrades"),
                GraphNodes.Text(() => subMenu.NoUpgradableTroopsText),
                container));
        }

        /// <summary>One trade the page offers, and everything the card draws for it.</summary>
        private void BuildUpgrade(
            GraphBuilder builder,
            UpgradeTroopsSubMenuAdapter.UpgradeEntry entry,
            string key)
        {
            Component card = entry == null ? null : entry.Card;
            if (card == null)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter.UpgradeEntry it = entry;
            NodeVtable vtable = GraphNodes.Group(() => Choice(it), null, null, it.CurrentTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(
                () => ModText.Plural(ModStrings.Draft.AvailableTroops, it.AvailableTroops, it.AvailableTroops)));
            vtable.OnFocusVisual = () => it.Focus();
            builder.BeginGroup(new DrawnNode(ControlId.For(card, key), vtable, card));

            BuildPortrait(builder, it, key + "/current", it.CurrentTroopButton, it.IsCurrentTroopVisible, () => it.CurrentTroopName, () => it.CurrentAmountText, () => it.ClickCurrentTroop(), it.CurrentTooltip);
            BuildPortrait(builder, it, key + "/target", it.TargetTroopButton, it.IsTargetTroopVisible, () => it.TargetTroopName, () => it.TargetAmountText, () => it.ClickTargetTroop(), it.TargetTooltip);
            BuildSlider(builder, it, key + "/slider");
            BuildUpgradeButton(builder, it, key + "/upgrade");

            builder.EndGroup();
        }

        /// <summary>What this card would turn what into.</summary>
        private static string Choice(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            string current = entry.CurrentTroopName;
            string target = entry.TargetTroopName;
            if (string.IsNullOrWhiteSpace(target) || target == current)
            {
                return current;
            }

            return ModText.Get(ModStrings.Draft.UpgradeChoice, current, target);
        }

        /// <summary>One of the two portraits: the troop it is about and how many of them the card is
        /// drawing under it, watched, since the game's own shortcut on the other portrait rewrites
        /// both. Pressing it is that shortcut - none of them on the current troop, as many as can be
        /// afforded on the target.</summary>
        private void BuildPortrait(
            GraphBuilder builder,
            UpgradeTroopsSubMenuAdapter.UpgradeEntry entry,
            string key,
            Component button,
            bool visible,
            System.Func<string> name,
            System.Func<string> amount,
            System.Action activate,
            Tooltip tooltip)
        {
            if (button == null || !visible)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter.UpgradeEntry it = entry;
            NodeVtable vtable = GraphNodes.Button(name, activate, null, tooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(amount));
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(button, key), vtable, button));
        }

        /// <summary>How many of the stack to upgrade, of the number that could be: the two amounts
        /// the card itself draws.</summary>
        private void BuildSlider(GraphBuilder builder, UpgradeTroopsSubMenuAdapter.UpgradeEntry entry, string key)
        {
            Component slider = entry.Slider;
            if (slider == null || !entry.IsSliderVisible)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter.UpgradeEntry it = entry;
            NodeVtable vtable = GraphNodes.Slider(
                () => ModText.Get(ModStrings.Common.Quantity),
                () => Amount(it),
                (sign, large) => it.SetSliderValue(it.SliderValue + sign * (large ? RecruitGroups.CoarseStep : 1)),
                () => it.IsSliderEnabled);
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(slider, key), vtable, slider));
        }

        /// <summary>How many of the stack the card is set to upgrade, of the whole of it - both
        /// numbers as the card draws them.</summary>
        private static string Amount(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            string target = entry.TargetAmountText;
            int upgrading;
            int keeping;
            if (string.IsNullOrWhiteSpace(target)
                || !int.TryParse(target, out upgrading)
                || !int.TryParse(entry.CurrentAmountText, out keeping))
            {
                return target;
            }

            return ModText.Get(ModStrings.Common.CountOf, upgrading, upgrading + keeping);
        }

        /// <summary>The button that pays for the upgrade, which draws its price and nothing else -
        /// or, where the troops would have nowhere to go, the game's own line saying so instead of
        /// the price.</summary>
        private void BuildUpgradeButton(GraphBuilder builder, UpgradeTroopsSubMenuAdapter.UpgradeEntry entry, string key)
        {
            Component button = entry.UpgradeButton;
            if (button == null || !entry.IsUpgradeVisible)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter.UpgradeEntry it = entry;
            NodeVtable vtable = GraphNodes.Button(
                () => it.IsRefusalVisible ? it.RefusalText : ModText.Get(ModStrings.Draft.Upgrade),
                () => it.Upgrade(),
                () => it.IsUpgradeEnabled,
                it.UpgradeTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(
                () => it.IsRefusalVisible ? string.Empty : ResourceCosts.Text(it.UpgradeCosts)));
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(button, key), vtable, button));
        }
    }
}
