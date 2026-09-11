using System;
using System.Collections.Generic;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The recruit cards of a <c>PurchaseTroopsSubMenu</c>, as the pages that draw one read them - a
    /// CONTRIBUTOR rather than a screen, called by the draft page of the dwelling, the town and the
    /// defence menu (<c>screens/DraftTroopsScreen.cs</c>) and by the rally point, which draws the
    /// same grid under its own list of towns.
    ///
    /// ONE EXPANDABLE GROUP PER CARD THE GAME DRAWS A CONTROL ON, in the order the grid draws them:
    /// the header is the troop and how many of them the pool holds, or the game's own line for a pool
    /// with nothing in it, and everything the card draws for that troop hangs underneath - the
    /// essence tabs where the troop has variants, the amount slider, the buy button with its price,
    /// and the button that upgrades what the pool already holds. The card itself has no click of any
    /// kind (the game wires none), so the header opens rather than acts. A card with NONE of those -
    /// a troop the place does not train yet - is a plain line instead: a group with nothing to open
    /// is a dead Right.
    ///
    /// EVERY GESTURE IS THE GAME'S OWN: the slider goes through the entry's own
    /// <c>HandleSliderChanged</c>, the buy button through <c>HandlePurchaseClicked</c>, the essence
    /// tabs through <c>HandleEssenceButtonClicked</c>, which is the same call the tab's mouse click
    /// makes and which rebuilds the card around the variant chosen. Resting on any of them selects
    /// the card the way the mouse's own hover does, which is what makes the game draw its details.
    /// </summary>
    public static class RecruitGroups
    {
        /// <summary>How far Shift and an arrow move the amount slider, which the game gives no coarse
        /// step of its own.</summary>
        public const int CoarseStep = 5;

        /// <summary>The whole grid, under the mod's own word for it, declared into whatever stop the
        /// caller has opened. <paramref name="lead"/> is whatever the page draws over the grid and
        /// inside the same band - the rally point's line saying which town it is recruiting from.
        /// </summary>
        public static void Region(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter subMenu,
            string keyPrefix,
            Action<GraphBuilder> lead = null)
        {
            if (builder == null || subMenu == null)
            {
                return;
            }

            IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries = subMenu.GetRecruitEntries();
            builder.PushContext(ModText.Get(ModStrings.Screens.Recruits));
            builder.SetRegion(keyPrefix + ":recruits");

            if (lead != null)
            {
                lead(builder);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                AddCard(builder, entries[i], CardPrefix(keyPrefix) + i);
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        /// <summary>Where a page's card keys start, so the same prefix names the cards and finds the
        /// one the cursor is on.</summary>
        public static string CardPrefix(string keyPrefix)
        {
            return keyPrefix + ":recruit/";
        }

        private static void AddCard(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key)
        {
            Component card = entry == null ? null : entry.Card;
            if (card == null)
            {
                return;
            }

            PurchaseTroopsSubMenuAdapter.RecruitEntry it = entry;
            // A card the game draws no control on - a troop this place does not train yet - is no
            // group: a group with nothing to open is a dead Right (owner ruling 2026-09-08), so the
            // header's own words stand as a line instead.
            bool opens = HasAnyControl(it);
            NodeVtable vtable = opens
                ? GraphNodes.Group(() => it.TroopName, null, null, it.Tooltip)
                : GraphNodes.Text(() => it.TroopName, null, it.Tooltip);
            // What the pool holds changes under a cursor standing right here: every purchase and
            // every upgrade of the pool empties or fills it.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Pool(it)));
            vtable.OnFocusVisual = () => it.Focus();
            DrawnNode node = new DrawnNode(ControlId.For(card, key), vtable, card);
            if (!opens)
            {
                builder.AddItem(node);
                return;
            }

            builder.BeginGroup(node);

            AddEssenceTabs(builder, it, key);
            AddSlider(builder, it, key);
            AddPurchase(builder, it, key);
            AddUpgradeInPool(builder, it, key);

            builder.EndGroup();
        }

        /// <summary>Whether the card draws anything the keyboard can reach: the same four questions
        /// the four contributors below ask themselves, asked before the card is declared.</summary>
        private static bool HasAnyControl(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return HasEssenceTabs(entry)
                || HasSlider(entry)
                || HasPurchase(entry)
                || HasUpgradeInPool(entry);
        }

        /// <summary>How many of this troop are waiting to be bought, or the game's own line where
        /// none are - it says both cases itself, one for a pool that has run dry and one for a troop
        /// this place does not produce at all.</summary>
        private static string Pool(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry.IsNoTroopsVisible
                ? entry.NoTroopsText
                : ModText.Plural(ModStrings.Draft.AvailableTroops, entry.AvailableTroops, entry.AvailableTroops);
        }

        /// <summary>The three essence variants, where the card draws them: one row of alternatives,
        /// named by the game's own words for the essences, of which the card is showing exactly one.
        /// Choosing rebuilds the card around that variant, which is what the tab's click does.
        /// </summary>
        private static void AddEssenceTabs(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key)
        {
            if (!HasEssenceTabs(entry))
            {
                return;
            }

            bool started = false;
            AddEssence(builder, entry, key, TroopUpgradeType.ArcanaUpgraded, "Units/Types/Arcana", "arcana", ref started);
            AddEssence(builder, entry, key, TroopUpgradeType.CreationUpgraded, "Units/Types/Creation", "creation", ref started);
            AddEssence(builder, entry, key, TroopUpgradeType.OrderUpgraded, "Units/Types/Order", "order", ref started);
            if (started)
            {
                builder.EndRow();
            }
        }

        private static bool HasEssenceTabs(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry.IsEssenceMenuVisible
                && (IsEssenceDrawn(entry, TroopUpgradeType.ArcanaUpgraded)
                    || IsEssenceDrawn(entry, TroopUpgradeType.CreationUpgraded)
                    || IsEssenceDrawn(entry, TroopUpgradeType.OrderUpgraded));
        }

        private static bool IsEssenceDrawn(
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            TroopUpgradeType upgradeType)
        {
            Component button = entry.EssenceButton(upgradeType);
            return GameObjects.IsLive(button);
        }

        private static void AddEssence(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key,
            TroopUpgradeType upgradeType,
            string nameKey,
            string id,
            ref bool started)
        {
            if (!IsEssenceDrawn(entry, upgradeType))
            {
                return;
            }

            Component button = entry.EssenceButton(upgradeType);
            if (!started)
            {
                builder.StartRow(key + "/essence");
                started = true;
            }

            PurchaseTroopsSubMenuAdapter.RecruitEntry it = entry;
            TroopUpgradeType type = upgradeType;
            string label = GameText.Get(nameKey, string.Empty);
            NodeVtable vtable = GraphNodes.Radio(
                () => label,
                () => it.CurrentEssenceVariant == type,
                () => it.SelectEssenceVariant(type),
                null,
                it.EssenceTooltip(type));
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(button, key + "/essence-" + id), vtable, button));
        }

        /// <summary>How many to buy, as the card draws it: the number it has settled on, of the pool
        /// it can take from. The arrows go through the game's own slider handler, so the price and
        /// the buy button follow every step.</summary>
        private static void AddSlider(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key)
        {
            if (!HasSlider(entry))
            {
                return;
            }

            Component slider = entry.Slider;
            PurchaseTroopsSubMenuAdapter.RecruitEntry it = entry;
            NodeVtable vtable = GraphNodes.Slider(
                () => ModText.Get(ModStrings.Common.Quantity),
                () => Amount(it),
                (sign, large) => it.SetSliderValue(it.SliderValue + sign * (large ? CoarseStep : 1)),
                () => it.IsSliderEnabled);
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(slider, key + "/slider"), vtable, slider));
        }

        private static bool HasSlider(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry.Slider != null && entry.IsSliderVisible;
        }

        private static string Amount(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            string amount = entry.AmountText;
            string total = entry.TotalAmountText;
            if (string.IsNullOrWhiteSpace(amount) || string.IsNullOrWhiteSpace(total))
            {
                return entry.SliderLabel;
            }

            return ModText.Get(ModStrings.Common.CountOf, amount, total);
        }

        /// <summary>The buy button, which draws its price and nothing else. The game turns it off
        /// while there is nothing to spend it on, nothing to spend, or no slot to put the troops in -
        /// and says which in the tooltip only for the last of those.</summary>
        private static void AddPurchase(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key)
        {
            if (!HasPurchase(entry))
            {
                return;
            }

            Component button = entry.PurchaseButton;
            PurchaseTroopsSubMenuAdapter.RecruitEntry it = entry;
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Draft.Purchase),
                () => it.Purchase(),
                () => it.IsPurchaseEnabled,
                it.PurchaseTooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ResourceCosts.Text(it.PurchaseCosts)));
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(button, key + "/purchase"), vtable, button));
        }

        private static bool HasPurchase(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry.PurchaseButton != null && entry.IsPurchaseVisible;
        }

        /// <summary>The button that upgrades what the pool is holding, which the game draws with no
        /// word on it and explains in its tooltip.</summary>
        private static void AddUpgradeInPool(
            GraphBuilder builder,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            string key)
        {
            if (!HasUpgradeInPool(entry))
            {
                return;
            }

            Component button = entry.UpgradeInPoolButton;
            PurchaseTroopsSubMenuAdapter.RecruitEntry it = entry;
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Draft.UpgradeAvailableTroops),
                () => it.UpgradeInPool(),
                () => it.IsUpgradeInPoolEnabled,
                it.UpgradeInPoolTooltip);
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(button, key + "/upgrade-pool"), vtable, button));
        }

        private static bool HasUpgradeInPool(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry.UpgradeInPoolButton != null && entry.IsUpgradeInPoolVisible;
        }
    }
}
