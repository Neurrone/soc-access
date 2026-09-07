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
    /// The artifact merchant a wielder walks into. Five places to be, in the order the menu draws
    /// them: the wielder band across the top, the market itself, the wielder's equipment, their
    /// backpack, and the close cross.
    ///
    /// THE MARKET stop is the drawn left half: the merchant's description, the nine category filters
    /// as ONE radio row named by the game's own tooltips on the toggles (never choosing on arrival -
    /// switching category throws the offers away and clears the selection), the offers the grid
    /// really holds (the game pads the grid to 24 empty cells, which are not content), and then the
    /// SELECTION BAND at the foot, which is one line and one button whose parts are all watched live.
    /// The band's two nodes are keyed structurally and gated on whichever of the game's three
    /// containers is painted, so a cursor standing on the band stays there and hears it turn from the
    /// prompt into Buy and from Buy into Sell.
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
        private const string MarketStop = "artifact-market";
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

        /// <summary>The merchant, by the title the menu draws over it ("Raider's Market").</summary>
        public override string ScreenName
        {
            get { return _adapter == null ? null : _adapter.Title; }
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

            builder.BeginStop(MarketStop);
            BuildDescription(builder);
            BuildCategories(builder);
            BuildOffers(builder);
            BuildSelectionBand(builder);

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

        // ---- the market ----

        /// <summary>What the merchant says about itself, as one node of its paragraphs.</summary>
        private void BuildDescription(GraphBuilder builder)
        {
            string description = _adapter.Description;
            if (string.IsNullOrWhiteSpace(description))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("description"), "artifact-market:description"),
                GraphNodes.Paragraphs(() => SpokenLines.Of(new[] { description }))));
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
        /// out to twenty-four cells with empties by design, and an empty cell is not an offer.
        /// </summary>
        private void BuildOffers(GraphBuilder builder)
        {
            IReadOnlyList<ArtifactMarketMenuAdapter.MarketArtifactItem> offers = Items("offers", _adapter.GetMarketArtifacts);
            for (int i = 0; i < offers.Count; i++)
            {
                ArtifactMarketMenuAdapter.MarketArtifactItem it = offers[i];
                if (it.Entry == null)
                {
                    continue;
                }

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
        }

        /// <summary>
        /// The band at the foot of the menu, as the two things it ever holds: the line naming what is
        /// selected - the game's prompt while nothing is - and the button that completes the deal.
        ///
        /// Both are keyed STRUCTURALLY and vouched for by whichever container the game is painting, so
        /// the cursor does not move when the band turns from the prompt into Buy or from Buy into
        /// Sell; the parts are watched, so it hears the change instead. The button is gone entirely
        /// while nothing is selected, and for an important artifact, whose Sell button the game does
        /// not draw.
        /// </summary>
        private void BuildSelectionBand(GraphBuilder builder)
        {
            Component container = _adapter.SelectionBandContainer;
            if (container != null)
            {
                NodeVtable line = GraphNodes.Text(BandLine);
                line.Announcements[0].Live = true;
                builder.AddItem(new DrawnNode(
                    ControlId.Structural("artifact-market:band/line"),
                    line,
                    container));
            }

            Component button = BandButton();
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(BandButtonLabel, BandActivate, BandEnabled);
            vtable.Announcements[0].Live = true;
            vtable.Announcements.Add(GraphNodes.ValuePart(BandPrice));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(
                ControlId.Structural("artifact-market:band/action"),
                vtable,
                button));
        }

        private string BandLine()
        {
            if (_adapter.IsBuyShown)
            {
                return _adapter.BuyItemName;
            }

            return _adapter.IsSellShown ? _adapter.SellItemName : _adapter.NoSelectionText;
        }

        private Component BandButton()
        {
            if (_adapter.IsBuyShown)
            {
                return _adapter.BuyButton;
            }

            return _adapter.IsSellShown && _adapter.IsSellButtonShown ? _adapter.SellButton : null;
        }

        private string BandButtonLabel()
        {
            return _adapter.IsBuyShown ? _adapter.BuyButtonLabel : _adapter.SellButtonLabel;
        }

        private string BandPrice()
        {
            return _adapter.IsBuyShown ? _adapter.BuyPriceLabel : _adapter.SellPriceLabel;
        }

        private bool BandEnabled()
        {
            return _adapter.IsBuyShown ? _adapter.CanBuySelectedArtifact() : _adapter.CanSellSelectedArtifact();
        }

        private void BandActivate()
        {
            if (_adapter.IsBuyShown)
            {
                _adapter.BuySelectedMarketArtifact();
                return;
            }

            _adapter.SellSelectedArtifact();
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
