using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The community maps browser's search results page, made navigable as a graph. Three places to
    /// be, and Tab moves between them: the band above the grid, the results themselves, and the way
    /// back.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the panel scrolls 4264 px of content
    /// through a 720 px viewport. A <c>Top</c> band at [0,133,1280,71] holds "Refine filter" at
    /// [972,133,100,27] and the sort dropdown at [1085,133,141,27] ("Sort by:" over its value); the
    /// results are <c>LavapotionSearchResultListItem_Regular(Clone)</c>s from y 251, five across
    /// 243 px apart and 193 px down; a <c>Bottom</c> band at [0,4077,1280,267] carries the
    /// end-of-results or no-results line under them.
    ///
    /// The sort control is a real dropdown, so it is a combo box opening the mod's own list over
    /// mod.io's popup, as every other page's dropdown is.
    ///
    /// Escape is CLAIMED and runs mod.io's own cancel, which from this panel opens the browse page
    /// again (decompiled <c>Navigating.Cancel</c>) - the same thing the drawn Back prompt does.
    /// </summary>
    public sealed class CommunityMapsSearchResultsScreen : LiveScreen<CommunityMapsSearchResultsAdapter>
    {
        private const string TopStop = "community-maps-search-results-top";
        private const string ResultsStop = "community-maps-search-results-list";
        private const string FooterStop = "community-maps-search-results-footer";

        /// <summary>The results page, read off mod.io's own singleton every frame
        /// (<see cref="CommunityMapsSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return CommunityMapsSources.SearchResults;
        }

        protected override CommunityMapsSearchResultsAdapter Adapt(object menu)
        {
            return new CommunityMapsSearchResultsAdapter((ModIOBrowser.Implementation.SearchResults)menu);
        }

        public override string Key
        {
            get { return "community-maps-search-results"; }
        }

        /// <summary>Layer 4: over the browser pages it was searched from.</summary>
        public override int Layer
        {
            get { return 4; }
        }

        /// <summary>The panel's own drawn heading ("Search results").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        /// <summary>The band above the grid, NOT the results: the page is pushed the moment mod.io
        /// switches the panel on, before it has fetched a single result, so a landing declared in the
        /// results stop finds nothing there and falls back anyway (measured). The summary is the line
        /// that says what the search found, and Tab is one key away from the grid.</summary>
        public override object InitialFocusStop
        {
            get { return TopStop; }
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Live != null && Live.Back();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TopStop);
            BuildTop(builder);

            builder.BeginStop(ResultsStop);
            BuildResults(builder);
            BuildEndOfResults(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the band above the grid ----

        private void BuildTop(GraphBuilder builder)
        {
            string summary = Live.SummaryText;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.AddItem(Synthetic("summary", GraphNodes.Text(() => summary)));
            }

            if (Live.HasRefineFilter)
            {
                NodeVtable refine = GraphNodes.Button(
                    () => Live.RefineFilterLabel,
                    () => Live.OpenRefineFilter());
                builder.AddItem(Synthetic("refine-filter", refine));
            }

            CommunityMapsSearchResultsAdapter.SortDropdown sort = Live.Sort;
            if (sort != null && sort.IsVisible() && sort.Subject != null)
            {
                NodeVtable vtable = GraphNodes.ComboBox(
                    () => sort.Label,
                    () => sort.CurrentLabel,
                    () => DropListScreen.Open(sort, sort.Label, index => sort.SetValue(index)),
                    sort.IsEnabled);
                vtable.OnFocusVisual = () => sort.Focus();
                builder.AddItem(new DrawnNode(
                    ControlId.For(sort.Subject, "community-maps-search-results:sort"),
                    vtable,
                    sort.Subject));
            }
        }

        // ---- the results ----

        private void BuildResults(GraphBuilder builder)
        {
            // The adapter's snapshot, retaken whenever the grid has fetched: mod.io appends to it as
            // the page scrolls, and the row count it appends to is one of the three things the
            // snapshot is stamped with. A row's own words are still read when they are read.
            IReadOnlyList<CommunityMapsSearchResultsAdapter.ResultItem> results = Live.BuildResults();
            for (int i = 0; i < results.Count; i++)
            {
                CommunityMapsSearchResultsAdapter.ResultItem result = results[i];
                if (result == null)
                {
                    continue;
                }

                CommunityMapsSearchResultsAdapter.ResultItem captured = result;
                NodeVtable vtable = GraphNodes.Button(
                    () => captured.Label,
                    () => Live.ActivateResult(captured));
                vtable.OnFocusVisual = () => Live.FocusResult(captured);
                // Keyed by PLACE in the grid, not by mod id: mod.io's grid can hold the same mod
                // twice (measured - the engine refused a build with "Duplicate control id:
                // community-maps-search-results:result/3561654"), while a page only ever appends, so
                // a row's position is both unique and stable.
                builder.AddItem(Synthetic("result/" + captured.DisplayIndex, vtable));
            }
        }

        /// <summary>The line mod.io draws under the grid - "no results" or the end-of-results block -
        /// read where it is drawn, after the results rather than before them.</summary>
        private void BuildEndOfResults(GraphBuilder builder)
        {
            string footer = Live.FooterText;
            if (string.IsNullOrWhiteSpace(footer))
            {
                return;
            }

            builder.AddItem(Synthetic("end-of-results", GraphNodes.Text(() => footer)));
        }

        // ---- the way back ----

        private void BuildFooter(GraphBuilder builder)
        {
            NodeVtable back = GraphNodes.Button(
                () => Live.BackLabel,
                () => Live.Back());
            builder.AddItem(Synthetic("back", back));
        }

        private SyntheticNode Synthetic(string key, NodeVtable vtable)
        {
            return new SyntheticNode(
                ControlId.For(Marker(key), "community-maps-search-results:" + key),
                vtable);
        }
    }
}
