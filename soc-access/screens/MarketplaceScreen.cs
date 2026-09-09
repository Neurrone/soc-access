using System;
using System.Collections.Generic;
using System.Globalization;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Economy;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The court of trade, made navigable as a graph. Four places to be, in the order the menu draws
    /// them: how many marketplaces the team owns, the trade grid, the tip, and the close.
    ///
    /// The grid is a TABLE (owner ruling) whose rows are the resources and whose cells are the game's
    /// own trade buttons - there is nothing to adjust here, because every trade is for a fixed amount
    /// the menu draws its own column for. Measured 2026-09-07: the heading "Court of Trade", the
    /// "Owning: N" line under it, then a Sell band over two columns captioned "-1" and "-5" and a
    /// Purchase band over two captioned "+1" and "+5"; five rows (Stone, Wood, Glimmerweave, Ancient
    /// Amber, Celestial Ore - gold is the currency the grid prices everything in and has no row); a tip
    /// line at the bottom; and NO close control at all, only the full-screen blocker behind the menu.
    ///
    /// A crossing names its column with BOTH captions the menu draws over it ("Sell -1"), because "-1"
    /// on its own says nothing about which way the trade goes. The row's own cell is a line rather than
    /// a control: it names the resource and says what the team holds of it, which is what a vertical
    /// crossing wants to hear. Each trade cell is the drawn button, labelled with the gold price the
    /// menu writes onto it (<c>MarketplaceMenu.ValidateButtons</c>) and unavailable while the game
    /// refuses the trade.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE, running the game's own <c>MarketplaceMenu.Hide</c>, and ESCAPE
    /// IS THE MOD'S too and runs the same path: the menu registers no input callback of any kind, so
    /// the keyboard has no way out of it otherwise.
    /// </summary>
    public sealed class MarketplaceScreen : LiveScreen<MarketplaceMenuAdapter>
    {
        private const string SummaryStop = "marketplace-summary";
        private const string TradesStop = "marketplace-trades";
        private const string TipStop = "marketplace-tip";
        private const string CloseStop = "marketplace-close";
        private const string SheetKey = "marketplace:";

        // Subjects of their own for the lines the menu gives no component the screen can key on, kept
        // across rebuilds so the reconciler seats the cursor back on the same node.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        /// <summary>The kingdom HUD's settings hold this menu (<see cref="HudSources"/>).</summary>
        private readonly ScreenSource<MarketplaceMenu> _source =
            ScreenSource<MarketplaceMenu>.FromOwner(HudSources.Kingdom, HudSources.Marketplace);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override MarketplaceMenuAdapter Adapt(object menu)
        {
            return new MarketplaceMenuAdapter((MarketplaceMenu)menu);
        }

        public override string Key
        {
            get { return "marketplace"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The menu's own drawn heading ("Court of Trade").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            return Live != null && Live.Close();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(SummaryStop);
            BuildSummary(builder);

            builder.BeginStop(TradesStop);
            BuildTrades(builder);

            builder.BeginStop(TipStop);
            BuildTip(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the marketplace count ----

        private void BuildSummary(GraphBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(Live.OwningSummary))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("summary"), "marketplace:summary"),
                GraphNodes.Text(() => Live.OwningSummary)));
        }

        // ---- the trade grid ----

        private void BuildTrades(GraphBuilder builder)
        {
            IReadOnlyList<MarketplaceMenuAdapter.TradeColumn> columns = Live.GetTradeColumns();
            // No heading band: the sheet names the column on every crossing ("Sell -1, 50 Gold"),
            // so a row of the captions would say them a second time (owner ruling 2026-09-07).
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(Live.Title, SheetCaptions(columns));
            IReadOnlyList<MarketplaceMenuAdapter.ResourceItem> resources = Live.GetResources();
            for (int i = 0; i < resources.Count; i++)
            {
                MarketplaceMenuAdapter.ResourceItem resource = resources[i];
                if (resource == null)
                {
                    continue;
                }

                object rowWidget = null;
                List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
                for (int c = 0; c < columns.Count; c++)
                {
                    MarketplaceMenuAdapter.TradeColumn column = columns[c];
                    MarketplaceMenuAdapter.TradeButtonItem button =
                        Live.GetTradeButton(resource.ResourceType, column.IsBuyButton, column.Amount);
                    if (button == null || !button.IsVisible || button.Component == null)
                    {
                        continue;
                    }

                    if (rowWidget == null)
                    {
                        rowWidget = button.Component;
                    }

                    cells.Add(new GraphSheet.SheetCell(c + 1, 0, TradeCell(button, resource)));
                }

                sheet.RowAt(Primary(resource), RowKey(resource), cells, rowWidget);
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on a RESOURCE, never on the heading band above it.
                builder.LandStopOn(sheet.FirstRow);
            }
        }

        /// <summary>The row's own cell: the resource's name and what the team holds of it, which is
        /// what names the row on a vertical crossing.</summary>
        private static NodeVtable Primary(MarketplaceMenuAdapter.ResourceItem resource)
        {
            string label = ModText.Get(
                ModStrings.UI.LabelValue,
                resource.ResourceName,
                resource.Amount.ToString("N0", CultureInfo.InvariantCulture));
            return GraphNodes.Text(() => label);
        }

        /// <summary>One crossing of the grid, as the button the menu draws it as: the gold price it
        /// carries (the game draws the figure beside a gold icon, so the label names the unit), the
        /// game's own click, and "unavailable" while the game refuses the trade.</summary>
        private NodeVtable TradeCell(
            MarketplaceMenuAdapter.TradeButtonItem button,
            MarketplaceMenuAdapter.ResourceItem resource)
        {
            MarketplaceMenuAdapter.TradeButtonItem it = button;
            string rowName = resource.ResourceName;
            string gold = Live.GetResourceName(ResourceType.Gold);
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Common.ResourceAmount, it.Price, gold),
                () => it.Activate(),
                () => it.IsEnabled);
            vtable.OnFocusVisual = it.Focus;
            vtable.SearchText = () => rowName;
            return vtable;
        }

        /// <summary>The captions the sheet labels its crossings with: nothing over the resource, and
        /// both drawn captions over each trade column, because "-1" on its own does not say which way
        /// the trade goes.</summary>
        private static string[] SheetCaptions(IReadOnlyList<MarketplaceMenuAdapter.TradeColumn> columns)
        {
            string[] captions = new string[columns.Count + 1];
            for (int i = 0; i < columns.Count; i++)
            {
                captions[i + 1] = ColumnCaption(columns[i]);
            }

            return captions;
        }

        private static string ColumnCaption(MarketplaceMenuAdapter.TradeColumn column)
        {
            return string.IsNullOrWhiteSpace(column.BandCaption)
                ? column.Caption
                : ModText.Get(ModStrings.Screens.MarketplaceTradeColumn, column.BandCaption, column.Caption);
        }

        private static string RowKey(MarketplaceMenuAdapter.ResourceItem resource)
        {
            return "marketplace-resource-" + resource.ResourceType;
        }

        // ---- the tip ----

        private void BuildTip(GraphBuilder builder)
        {
            if (Live.TipLines.Count == 0)
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("tip"), "marketplace:tip"),
                GraphNodes.Paragraphs(() => Live.TipLines)));
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("close"), "marketplace:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => Live.Close())));
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
