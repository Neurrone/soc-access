using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The community maps browser's Browse page, made navigable as a graph. Four places to be, and
    /// Tab moves between them: the Browse/Collection tab pair, the page's two commands, the bands of
    /// maps and mods, and the way out.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the browser draws in its own canvas
    /// ("Canvas - (MUST BE DISABLED WHEN SAVING THE PREFAB)"). The nav bar across the top holds BROWSE
    /// (x 479) and COLLECTION (x 634) at y 29, "Search &amp; filter" at [1064,27,107,27] and the
    /// "Back / Exit" prompt at [53,24,50,27]. The page itself (<c>Home</c>) is 1791 px of content in a
    /// 800 px window: the featured band at [0,109,1280,457] drawing its caption "Featured maps &amp;
    /// mods" at y 109 over a carousel of ten items at y 133, then ONE <c>Details</c> block at
    /// [323,513,635,27] carrying the highlighted item's name with "Subscribe" (x 748) and "More
    /// options" (x 859) beside it; then <c>ModRow_1</c> to <c>ModRow_4</c> at y 609, 891, 1173 and
    /// 1455, each drawing its own caption ("Highest rated", "Trending", "Most popular", "Recently
    /// added") over twenty items 244 px apart.
    ///
    /// A drawn caption is the REGION its items belong to, so Alt+Up and Alt+Down jump between the five
    /// bands and the caption is spoken on the way in. THE FEATURED ROW'S SUBSCRIBE AND MORE OPTIONS
    /// ARE DRAWN ONCE, below the carousel, acting on whichever featured item is highlighted; each
    /// featured item is nonetheless a group whose children highlight that item and then press the
    /// block, so the keyboard reaches them from the item as it does in the bands (owner, 2026-09-07).
    ///
    /// A BAND'S OWN ITEMS ARE DIFFERENT: mod.io draws their Subscribe and More options as an OVERLAY
    /// over the selected item (<c>HomeModListItem_Overlay</c>, moved onto the item by its
    /// <c>OnSelect</c>), so each item is an expandable group carrying the two as its children - Right
    /// opens it, Enter on the item still opens its details page.
    ///
    /// THE TABS SWITCH ON ENTER, NOT ON FOCUS. The nav bar draws two text labels with no clickable
    /// control under them at all (<c>NavBar</c> only tints them, decompiled), so there is no native
    /// selection to arrive on: the only way to switch is to OPEN the other panel, which re-fetches the
    /// page from mod.io. Arriving on a tab must not do that.
    ///
    /// Escape is CLAIMED: this page is mod.io's, not the game's, and nothing registers the key for it -
    /// the finding the community maps modal recorded. It runs the browser's own close, which is what
    /// mod.io's <c>Navigating.Cancel</c> reaches on this panel (decompiled: the last branch, once no
    /// context menu, dropdown, search panel, authentication, download queue, details or other panel is
    /// up, is <c>Browser.Close()</c>).
    /// </summary>
    public sealed class CommunityMapsHomeScreen : LiveScreen<CommunityMapsHomeAdapter>
    {
        private const string TabsStop = "community-maps-tabs";
        private const string CommandsStop = "community-maps-commands";
        private const string RowsStop = "community-maps-rows";
        private const string FooterStop = "community-maps-footer";

        /// <summary>The Browse page, read off mod.io's own singleton every frame
        /// (<see cref="CommunityMapsSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return CommunityMapsSources.Home;
        }

        protected override CommunityMapsHomeAdapter Adapt(object menu)
        {
            return new CommunityMapsHomeAdapter((ModIOBrowser.Implementation.Home)menu);
        }

        public override string Key
        {
            get { return "community-maps-home"; }
        }

        /// <summary>Layer 1: the community maps browser, over the main menu.</summary>
        public override int Layer
        {
            get { return 1; }
        }

        /// <summary>The page's own name in mod.io's words ("Browse"), which is also its tab.</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        /// <summary>The tab pair, so arrival reads which page is showing before its first band.</summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        /// <summary>Showing means the browser is up, the home object is there, AND the BROWSE panel
        /// is the one drawn: the adapter answers present for either tab, because it owns the tab pair
        /// both pages draw, so the panel's own active state is read here every frame.</summary>
        public override bool IsActive()
        {
            return base.IsActive() && Live.IsBrowseSelected;
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
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

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(CommandsStop);
            BuildCommands(builder);

            builder.BeginStop(RowsStop);
            BuildFeatured(builder);
            BuildRows(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the tab pair ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsHomeAdapter.TabItem> tabs = Live.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                CommunityMapsHomeAdapter.TabItem tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                CommunityMapsHomeAdapter.TabItem captured = tab;
                NodeVtable vtable = GraphNodes.Tab(
                    () => captured.Label,
                    () => captured.IsSelected);
                vtable.OnActivate = () => captured.Select();
                builder.AddItem(Synthetic("tab/" + captured.Key, vtable));
            }
        }

        // ---- the page's commands ----

        private void BuildCommands(GraphBuilder builder)
        {
            NodeVtable searchFilter = GraphNodes.Button(
                () => Live.SearchFilterLabel,
                () => Live.OpenSearchFilter(),
                () => Live.HasSearchFilter);
            builder.AddItem(Synthetic("search-filter", searchFilter));

            NodeVtable downloads = GraphNodes.Button(
                () => Live.DownloadsLabel,
                () => Live.OpenDownloadsMenu(),
                () => Live.HasDownloadsMenu);
            builder.AddItem(Synthetic("downloads", downloads));
        }

        // ---- the featured band ----

        private void BuildFeatured(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsHomeAdapter.FeaturedItem> items = Live.GetFeaturedItems();
            if (items.Count == 0)
            {
                return;
            }

            builder.PushContext(Live.FeaturedLabel);
            builder.SetRegion("community-maps:featured");
            for (int i = 0; i < items.Count; i++)
            {
                CommunityMapsHomeAdapter.FeaturedItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                CommunityMapsHomeAdapter.FeaturedItem captured = item;
                string key = "featured/" + captured.Index;
                NodeVtable vtable = GraphNodes.Button(
                    () => captured.Label,
                    () => Live.ActivateFeaturedItem(captured));
                vtable.OnFocusVisual = () => Live.FocusFeaturedItem(captured);

                // mod.io draws ONE Subscribe / More options block under the carousel, acting on
                // whichever featured item is highlighted. The owner wants it reachable from each
                // item, as the bands' own per-item overlay is (2026-09-07), so every featured item
                // is a group whose children highlight the item first and then press that block.
                SyntheticNode header = Synthetic(key, vtable);
                builder.BeginGroup(header);
                // A shut group's children are swallowed by the builder anyway, and there are two of
                // them under each of the ninety items this page draws: composing them costs a vtable,
                // its closures and a keyed node apiece, every frame, for nothing anyone can reach.
                if (!builder.IsExpanded(header.Id))
                {
                    builder.EndGroup();
                    continue;
                }

                NodeVtable subscribe = GraphNodes.Button(
                    () => Live.FeaturedSubscribeLabel,
                    () =>
                    {
                        Live.FocusFeaturedItem(captured);
                        Live.SubscribeFeatured();
                    },
                    () => Live.HasFeatured);
                subscribe.OnFocusVisual = () => Live.FocusFeaturedItem(captured);
                builder.AddItem(Synthetic(key + "/subscribe", subscribe));

                NodeVtable options = GraphNodes.Button(
                    () => Live.MoreOptionsLabel,
                    () =>
                    {
                        Live.FocusFeaturedItem(captured);
                        Live.OpenFeaturedOptions();
                    },
                    () => Live.HasFeatured);
                options.OnFocusVisual = () => Live.FocusFeaturedItem(captured);
                builder.AddItem(Synthetic(key + "/options", options));

                builder.EndGroup();
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        // ---- the captioned bands ----

        private void BuildRows(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsHomeAdapter.RowItem> rows = Live.GetRows();
            for (int r = 0; r < rows.Count; r++)
            {
                CommunityMapsHomeAdapter.RowItem row = rows[r];
                if (row == null || row.Items == null || row.Items.Count == 0)
                {
                    continue;
                }

                builder.PushContext(string.IsNullOrWhiteSpace(row.Label)
                    ? ModText.Get(ModStrings.Screens.Group, (row.Index + 1).ToString())
                    : row.Label);
                builder.SetRegion("community-maps:row/" + row.Index);
                for (int i = 0; i < row.Items.Count; i++)
                {
                    CommunityMapsHomeAdapter.ModItem item = row.Items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    AddItem(builder, item);
                }

                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>
        /// One map or mod of a band, with the two commands mod.io hangs on it as children: Enter opens
        /// its details page, Right opens the group and reaches Subscribe and More options.
        ///
        /// They are the item's own, not the band's. mod.io keeps ONE overlay object
        /// (<c>SelectionOverlayHandler.homeModListItemOverlay</c>) and moves it onto whichever list item
        /// is selected - the item's <c>OnSelect</c> calls <c>MoveSelection(this)</c>, which sets the
        /// overlay's <c>listItemToReplicate</c> - so what the two buttons act on is whatever is
        /// selected, and the adapter selects the child's own item before pressing either. The featured
        /// carousel is the exception and keeps its pair as rows of the band, because there the block is
        /// drawn once under the whole carousel rather than over an item.
        /// </summary>
        private void AddItem(GraphBuilder builder, CommunityMapsHomeAdapter.ModItem item)
        {
            CommunityMapsHomeAdapter.ModItem captured = item;
            string key = "row/" + captured.RowIndex + "/item/" + captured.Index;
            NodeVtable vtable = GraphNodes.Button(
                () => captured.Label,
                () => Live.ActivateItem(captured));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => captured.Status));
            vtable.OnFocusVisual = () => Live.FocusItem(captured);

            SyntheticNode header = Synthetic(key, vtable);
            builder.BeginGroup(header);
            if (!builder.IsExpanded(header.Id))
            {
                builder.EndGroup();
                return;
            }

            NodeVtable subscribe = GraphNodes.Button(
                () => Live.GetItemSubscribeLabel(captured),
                () => Live.SubscribeItem(captured));
            // The overlay follows mod.io's selection, so standing on a child keeps the item selected.
            subscribe.OnFocusVisual = () => Live.FocusItem(captured);
            builder.AddItem(Synthetic(key + "/subscribe", subscribe));

            NodeVtable options = GraphNodes.Button(
                () => Live.MoreOptionsLabel,
                () => Live.OpenItemOptions(captured));
            options.OnFocusVisual = () => Live.FocusItem(captured);
            builder.AddItem(Synthetic(key + "/options", options));

            builder.EndGroup();
        }

        // ---- the way out ----

        private void BuildFooter(GraphBuilder builder)
        {
            NodeVtable close = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => Live.Close());
            builder.AddItem(Synthetic("close", close));
        }

        private SyntheticNode Synthetic(string key, NodeVtable vtable)
        {
            return new SyntheticNode(ControlId.For(Marker(key), "community-maps:" + key), vtable);
        }
    }
}
