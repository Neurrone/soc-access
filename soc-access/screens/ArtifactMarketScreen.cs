using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Common.Details;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The artifact merchant a wielder walks into. Seven places to be, in the order the menu draws
    /// them: the wielder band across the top, the merchant's own words, the purchase, the sale (only
    /// while an artifact is up for sale), the wielder's equipment, their backpack, and the close
    /// cross.
    ///
    /// THE DESCRIPTION IS A STOP OF ITS OWN, so a player who came to trade tabs past the merchant's
    /// paragraph rather than arrowing through it to reach the offers. It holds the merchant's words
    /// and, while nothing is selected, the game's prompt ("Select an artifact to sell or purchase")
    /// as a second line (owner ruling 2026-09-08): the prompt is the state of the whole menu rather
    /// than of either half of the band, and saying it once in each half read as two things to do.
    ///
    /// PURCHASE holds everything buying takes, in the order the menu draws it: a region named
    /// "Filters" over the nine category filters as ONE radio row named by the game's own tooltips on
    /// the toggles (never choosing on arrival - switching category throws the offers away and clears
    /// the selection), a region named "Available" over the offers the grid really holds (the game
    /// pads the grid to 24 empty cells, which are not content; a category with nothing in it is one
    /// line saying so, so the region is never silent), and, while an offer is selected, the buy half
    /// of the SELECTION BAND at the end of the stop: the line naming what is being bought and the
    /// Buy button.
    ///
    /// SELL is that band's other half as a stop of its own, right after Purchase, declared only while
    /// the game paints it, so the sale a player sets up from the backpack is one Tab away and a
    /// market with nothing selected has no empty stop to tab through. It is named by the word the
    /// game itself writes over the button.
    ///
    /// THE GAME DRAWS ONE BAND with three containers - the prompt, the purchase, the sale - and
    /// paints one of them at a time. Each half's nodes are keyed structurally, the parts are watched
    /// live, the price rides on the button, and the button is gone for an important artifact, whose
    /// Sell button the game does not draw.
    ///
    /// THE CLICKS MEAN SOMETHING ELSE HERE than they do on the wielder sheet, and the difference is
    /// the game's, not the mod's - the same two native handlers branch on
    /// <c>InventoryHUD.IsArtifactShopInventory</c>. Enter is the artifact's left click, which in this
    /// menu SELECTS IT FOR SALE and fills the Sell band
    /// (<c>ArtifactMarketMenu.HandleInventoryArtifactClicked</c>); Ctrl and the right click SELL it
    /// rather than destroying it (<c>InventoryArtifactMovable.HandleRightClick</c>). The plain right
    /// click still equips, unequips or uses, and Ctrl and the left click still drop on the ground. An
    /// artifact the game treats as important has no Sell button drawn for it at all, which is its
    /// rule and not something the mod says anything extra about; likewise an unaffordable purchase is
    /// simply "unavailable", because the game itself is silent about why.
    ///
    /// The wielder band and the two artifact stops come from the shared contributors
    /// (<c>ui/TroopHudRows.cs</c> and <c>ui/ArtifactSlotNodes.cs</c>): the rows are identical to the
    /// sheet's, only the hints differ.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu IS an <c>AdventureMenuBackground</c>
    /// with a close cross, and <c>AnimateEntry</c> registers <c>UI.ExitMenu</c> on its own close
    /// outside any gamepad branch (measured 2026-09-07 in the decompiled source). The navigator claims
    /// the key only while something is being carried.
    /// </summary>
    public sealed class ArtifactMarketScreen : GraphScreen
    {
        private const string WielderStop = "artifact-market-wielder";
        private const string DescriptionStop = "artifact-market-description";
        private const string PurchaseStop = "artifact-market-purchase";
        private const string SellStop = "artifact-market-sell";
        private const string EquipmentStop = "artifact-market-equipment";
        private const string InventoryStop = "artifact-market-inventory";
        private const string CloseStop = "artifact-market-close";
        private const string KeyPrefix = "artifact-market";
        private const string WielderKey = "artifact-market:wielder";

        private readonly ArtifactMarketMenuAdapter _adapter;

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the description and the auto-arrange button are not drawn as
        // controls of their own.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public ArtifactMarketScreen(ArtifactMarketMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ArtifactMarketMenu[] menus = Resources.FindObjectsOfTypeAll<ArtifactMarketMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ArtifactMarketMenuAdapter adapter = new ArtifactMarketMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new ArtifactMarketScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "artifact-market"; }
        }

        /// <summary>The merchant, by the title the menu draws over it ("Raider's Market") and the
        /// banner the wielder's band draws over the portrait where the place has a name of its own.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                return _adapter == null
                    ? null
                    : TroopHudRows.NameWithPlace(_adapter.Title, _adapter.Wielder);
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it whenever the stock, an artifact or the army
        /// changes. The graph is declared afresh on every operation, so there is nothing to rebuild.
        /// </summary>
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

            TroopHudRows.WielderStop(builder, WielderStop, WielderKey, _adapter.Wielder);

            builder.BeginStop(DescriptionStop);
            BuildDescription(builder);

            builder.BeginStop(PurchaseStop);
            BuildPurchase(builder);

            builder.BeginStop(SellStop);
            BuildSell(builder);

            builder.BeginStop(EquipmentStop);
            ArtifactSlotNodes.Equipment(builder, _adapter, KeyPrefix, AddSlotHints);

            builder.BeginStop(InventoryStop);
            ArtifactSlotNodes.Inventory(builder, _adapter, KeyPrefix, AddSlotHints, Marker("auto-arrange"));

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on the band's troop rows.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, Troops, WielderKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, Troops, WielderKey);
        }

        private TroopHudAdapter Troops
        {
            get { return _adapter == null || _adapter.Wielder == null ? null : _adapter.Wielder.Troops; }
        }

        // ---- the merchant's words ----

        /// <summary>What the merchant says about itself, as one node of its paragraphs, and then the
        /// band's prompt while the game is painting it: nothing is selected to buy or sell.</summary>
        private void BuildDescription(GraphBuilder builder)
        {
            string description = _adapter.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("description"), "artifact-market:description"),
                    GraphNodes.Paragraphs(() => SpokenLines.Of(new[] { description }))));
            }

            Component prompt = _adapter.IsNoSelectionShown ? _adapter.NoSelectionContainer : null;
            if (prompt != null)
            {
                builder.AddItem(new DrawnNode(
                    ControlId.Structural("artifact-market:prompt"),
                    GraphNodes.Text(() => _adapter.NoSelectionText),
                    prompt));
            }
        }

        // ---- the purchase ----

        /// <summary>Everything a purchase takes, in the order the menu draws it: the filters, the
        /// offers they choose among, and the band's buy half at the end of the stop.</summary>
        private void BuildPurchase(GraphBuilder builder)
        {
            builder.PushContext(ModText.Get(ModStrings.Screens.Purchase));

            builder.PushContext(ModText.Get(ModStrings.UI.Filters));
            builder.SetRegion("artifact-market:filters");
            BuildCategories(builder);
            builder.PopContext();

            builder.PushContext(ModText.Get(ModStrings.Screens.Available));
            builder.SetRegion("artifact-market:offers");
            BuildOffers(builder);
            builder.PopContext();

            builder.SetRegion(null);
            BuildBuyBand(builder);
            builder.PopContext();
        }

        /// <summary>The nine filters as the ONE BAR the menu draws: Left and Right walk it, Enter is
        /// the toggle's own switch. Arriving must not switch - the switch throws the offers away and
        /// clears whatever is selected.</summary>
        private void BuildCategories(GraphBuilder builder)
        {
            IReadOnlyList<ArtifactMarketMenuAdapter.CategoryItem> categories = Items("categories", _adapter.GetCategories);
            if (categories.Count == 0)
            {
                return;
            }

            builder.StartRow("artifact-market:categories");
            for (int i = 0; i < categories.Count; i++)
            {
                ArtifactMarketMenuAdapter.CategoryItem it = categories[i];
                if (it.Toggle == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Radio(
                    () => it.Label,
                    () => _adapter.ActiveCategoryIndex == it.Index,
                    () => _adapter.SelectCategory(it.Index));
                vtable.OnFocusVisual = () => _adapter.FocusCategory(it.Index);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Toggle, "artifact-market:category/" + it.Index),
                    vtable,
                    it.Toggle));
            }

            builder.EndRow();
        }

        /// <summary>What the merchant has for sale, in the order the grid draws it. The grid is padded
        /// out to twenty-four cells with empties by design, and an empty cell is not an offer; a
        /// category with no offer at all is one line saying so.</summary>
        private void BuildOffers(GraphBuilder builder)
        {
            IReadOnlyList<ArtifactMarketMenuAdapter.MarketArtifactItem> offers = Items("offers", _adapter.GetMarketArtifacts);
            bool any = false;
            for (int i = 0; i < offers.Count; i++)
            {
                ArtifactMarketMenuAdapter.MarketArtifactItem it = offers[i];
                if (it.Entry == null)
                {
                    continue;
                }

                any = true;

                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => _adapter.SelectMarketEntryForPurchase(it.Entry),
                    null,
                    it.Tooltip);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.CostLabel, watch: false));
                // Selecting the game's own cell draws the artifact's tooltip and scrolls the grid.
                vtable.OnFocusVisual = () => _adapter.SelectMarketEntry(it.Entry);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Entry, "artifact-market:offer/" + i),
                    vtable,
                    it.Entry));
            }

            if (!any)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("no-offers"), "artifact-market:no-offers"),
                    GraphNodes.Text(() => ModText.Get(ModStrings.Screens.Empty))));
            }
        }

        /// <summary>
        /// The buy half of the band, at the end of the purchase stop while the game paints it: the
        /// line naming the artifact being bought and the Buy button under it. Both are keyed
        /// STRUCTURALLY and their parts watched, so a cursor standing here hears one purchase turn
        /// into the next.
        /// </summary>
        private void BuildBuyBand(GraphBuilder builder)
        {
            if (!_adapter.IsBuyShown)
            {
                return;
            }

            Component container = _adapter.BuyContainer;
            if (container != null)
            {
                NodeVtable line = GraphNodes.Text(() => _adapter.BuyItemName);
                line.Announcements[0].Live = true;
                builder.AddItem(new DrawnNode(
                    ControlId.Structural("artifact-market:purchase/line"),
                    line,
                    container));
            }

            Component button = _adapter.BuyButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.BuyButtonLabel,
                () => _adapter.BuySelectedMarketArtifact(),
                _adapter.CanBuySelectedArtifact);
            vtable.Announcements[0].Live = true;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => _adapter.BuyPriceLabel));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(
                ControlId.Structural("artifact-market:purchase/action"),
                vtable,
                button));
        }

        // ---- the sale ----

        /// <summary>
        /// The sell half of the same band, as a stop under the word the game writes over the button,
        /// declared only while the game paints it: the line naming the artifact being sold and the
        /// Sell button under it, which the game does not draw at all for an artifact it treats as
        /// important. While nothing is up for sale there is no stop here at all.
        /// </summary>
        private void BuildSell(GraphBuilder builder)
        {
            if (!_adapter.IsSellShown)
            {
                return;
            }

            builder.PushContext(_adapter.SellButtonLabel);

            Component container = _adapter.SellContainer;
            if (container != null)
            {
                NodeVtable line = GraphNodes.Text(() => _adapter.SellItemName);
                line.Announcements[0].Live = true;
                builder.AddItem(new DrawnNode(
                    ControlId.Structural("artifact-market:sell/line"),
                    line,
                    container));
            }

            Component button = _adapter.IsSellButtonShown ? _adapter.SellButton : null;
            if (button != null)
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => _adapter.SellButtonLabel,
                    () => _adapter.SellSelectedArtifact(),
                    _adapter.CanSellSelectedArtifact);
                vtable.Announcements[0].Live = true;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => _adapter.SellPriceLabel));
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
                builder.AddItem(new DrawnNode(
                    ControlId.Structural("artifact-market:sell/action"),
                    vtable,
                    button));
            }

            builder.PopContext();
        }

        // ---- the artifacts ----

        /// <summary>The four gestures an occupied slot has here, in the order they are said. Two of
        /// them are this menu's own: the left click picks the artifact out to sell, and Ctrl with the
        /// right click sells it where the sheet's would have destroyed it.</summary>
        private void AddSlotHints(NodeVtable vtable, InventorySlotInfo slot)
        {
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactSelectForSaleHint,
                AccessibilityActions.UiLeftClick.Key);
            ArtifactDetails.EquipInstruction instruction = _adapter.GetArtifactInstruction(slot);
            ModString contextual = instruction == ArtifactDetails.EquipInstruction.Use
                ? ModStrings.Screens.ArtifactUseHint
                : instruction == ArtifactDetails.EquipInstruction.Unequip
                    ? ModStrings.Screens.ArtifactUnequipHint
                    : ModStrings.Screens.ArtifactEquipHint;
            NodeHints.Add(vtable, contextual, AccessibilityActions.UiRightClick.Key);
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactSellHint,
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
            builder.AddItem(new DrawnNode(ControlId.For(close, "artifact-market:close"), vtable, close));
        }

        // ---- shared ----

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
                SocAccessMod.Instance?.LogWarning("ArtifactMarketScreen section " + section + " failed to build: " + exception);
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
    }
}
