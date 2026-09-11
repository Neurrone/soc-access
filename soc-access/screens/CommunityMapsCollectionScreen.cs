using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using TMPro;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The community maps browser's Collection page, made navigable as a graph. Five places to be, and
    /// Tab moves between them: the Browse/Collection tab pair, the page's two commands, the filtering
    /// band, the subscribed mods, and the way out.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the nav bar as on the browse page -
    /// BROWSE (x 479) and COLLECTION (x 634) at y 29, "Search &amp; filter" at [1064,27,107,27], the
    /// "Back / Exit" prompt at [53,24,50,27] - then the panel's own title ("Collection") at
    /// [53,133,181,27] and one <c>Filtering</c> band across y 192: the keyword box at [53,192,373,32]
    /// (placeholder "Enter keyword"), "Check for updates" at [714,195,118,27], "Filter by:" at
    /// [843,195,187,27] reading "Subscribed" and "Sort by:" at [1040,195,187,27] reading
    /// "Alphabetical". The mods hang under that; this account has none, and an empty stop declares
    /// nothing, so Tab passes straight over it.
    ///
    /// Both drawn filters are real dropdowns - mod.io's <c>MultiTargetDropdown</c>, a
    /// <c>TMP_Dropdown</c> subclass (decompiled) - so each is a combo box opening the mod's own list
    /// over mod.io's popup, as every other page's dropdown does.
    ///
    /// THE TABS SWITCH ON ENTER, NOT ON FOCUS, for the reason the browse page records: the nav bar
    /// draws two text labels with no clickable control under them, and the only way to switch is to
    /// OPEN the other panel, which re-fetches the page.
    ///
    /// Escape is CLAIMED and runs mod.io's own cancel, which from this panel opens the browse page
    /// again (decompiled <c>Navigating.Cancel</c>); the drawn Close closes the whole browser, as the
    /// widget screen's did.
    /// </summary>
    public sealed class CommunityMapsCollectionScreen : LiveScreen<CommunityMapsCollectionAdapter>
    {
        private const string TabsStop = "community-maps-collection-tabs";
        private const string CommandsStop = "community-maps-collection-commands";
        private const string FiltersStop = "community-maps-collection-filters";
        private const string ItemsStop = "community-maps-collection-items";
        private const string FooterStop = "community-maps-collection-footer";

        private readonly GameTextEditor _editor = new GameTextEditor();
        /// <summary>The Collection page, read off mod.io's own singleton every frame
        /// (<see cref="CommunityMapsSources"/>). Whether it is the page SHOWING is the adapter's
        /// <c>IsPresent</c>, which reads the panel's own active state.</summary>
        protected override object ResolveMenu()
        {
            return CommunityMapsSources.Collection;
        }

        protected override CommunityMapsCollectionAdapter Adapt(object menu)
        {
            return new CommunityMapsCollectionAdapter((ModIOBrowser.Implementation.Collection)menu);
        }

        public override string Key
        {
            get { return "community-maps-collection"; }
        }

        /// <summary>Layer 2: over the browser home page.</summary>
        public override int Layer
        {
            get { return 2; }
        }

        /// <summary>The panel's own drawn title, which carries the count ("Collection (0)").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Live != null && Live.Cancel();
        }

        /// <summary>The page's own editor, over the keyword box. GraphScreen takes the rest of its
        /// lifecycle: the raw-input and field-ownership answers, the per-frame update, and the
        /// abandon on leaving and on popping.</summary>
        public override GameTextEditor Editor
        {
            get { return _editor; }
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

            builder.BeginStop(FiltersStop);
            BuildKeyword(builder);
            BuildCheckForUpdates(builder);
            BuildDropdown(builder, Live.FilterDropdown);
            BuildDropdown(builder, Live.SortDropdown);

            builder.BeginStop(ItemsStop);
            BuildItems(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the tab pair ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsCollectionAdapter.TabItem> tabs = Live.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                CommunityMapsCollectionAdapter.TabItem tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                CommunityMapsCollectionAdapter.TabItem captured = tab;
                NodeVtable vtable = GraphNodes.Tab(
                    () => captured.Label,
                    () => captured.IsSelected);
                vtable.OnActivate = () => captured.Select();
                builder.AddItem(Synthetic("tab/" + captured.Id, vtable));
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

        // ---- the filtering band ----

        /// <summary>The keyword box. Its label is the box's own placeholder, which is the only thing
        /// the band writes next to it.</summary>
        private void BuildKeyword(GraphBuilder builder)
        {
            GraphNodes.TmpEditField(
                builder,
                "community-maps-collection:keyword",
                Live.SearchField,
                () => Live.SearchFieldLabel,
                _editor);
        }

        private void BuildCheckForUpdates(GraphBuilder builder)
        {
            CommunityMapsCollectionAdapter.ButtonAction action = Live.CheckForUpdatesAction;
            if (action == null || !action.IsVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => action.Label,
                () => action.Activate(),
                action.IsEnabled);
            vtable.OnFocusVisual = () => action.Focus();
            builder.AddItem(Synthetic(action.Id, vtable));
        }

        private void BuildDropdown(GraphBuilder builder, CommunityMapsCollectionAdapter.DropdownItem list)
        {
            if (list == null || !list.IsVisible() || list.Subject == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.ComboBox(
                () => list.Label,
                () => list.CurrentLabel,
                () => DropListScreen.Open(list, list.Label, index => list.SetValue(index)),
                list.IsEnabled);
            vtable.OnFocusVisual = () => list.Focus();
            builder.AddItem(new DrawnNode(
                ControlId.For(list.Subject, "community-maps-collection:" + list.Id),
                vtable,
                list.Subject));
        }

        // ---- the subscribed mods ----

        private void BuildItems(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsCollectionAdapter.CollectionItem> items = Live.GetItems();
            for (int i = 0; i < items.Count; i++)
            {
                CommunityMapsCollectionAdapter.CollectionItem item = items[i];
                if (item == null || !item.IsVisible)
                {
                    continue;
                }

                CommunityMapsCollectionAdapter.CollectionItem captured = item;
                NodeVtable vtable = GraphNodes.Button(
                    () => captured.Label,
                    () => Live.ActivateItem(captured));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => captured.Status));
                vtable.OnFocusVisual = () => Live.FocusItem(captured);
                builder.AddItem(Synthetic("item/" + captured.Index, vtable));
            }
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
            return new SyntheticNode(
                ControlId.For(Marker(key), "community-maps-collection:" + key),
                vtable);
        }
    }
}
