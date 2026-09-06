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
    public sealed class CommunityMapsCollectionScreen : GraphScreen
    {
        private const string TabsStop = "community-maps-collection-tabs";
        private const string CommandsStop = "community-maps-collection-commands";
        private const string FiltersStop = "community-maps-collection-filters";
        private const string ItemsStop = "community-maps-collection-items";
        private const string FooterStop = "community-maps-collection-footer";

        private readonly CommunityMapsCollectionAdapter _adapter;
        private readonly GameTextEditor _editor = new GameTextEditor();
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public CommunityMapsCollectionScreen(CommunityMapsCollectionAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsCollectionAdapter adapter = CommunityMapsCollectionAdapter.TryCreate();
            return adapter != null && adapter.IsPresent()
                ? new CommunityMapsCollectionScreen(adapter)
                : null;
        }

        public override string Key
        {
            get { return "community-maps-collection"; }
        }

        /// <summary>The panel's own drawn title, which carries the count ("Collection (0)").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return IsPresent(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.Cancel();
        }

        /// <summary>While the keyboard is on its way to the keyword box, what the player types next is
        /// meant for that box and must not start a search of the page.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool OwnsGameField
        {
            get { return _editor.Pending || _editor.Editing; }
        }

        /// <summary>Asked by the detector before it refreshes the page. The keyword box is the one
        /// thing here that must not be disturbed mid-edit.</summary>
        public bool IsSearchInputFocused()
        {
            return _editor.Pending || _editor.Editing;
        }

        /// <summary>Kept for the detector. The graph is declared afresh on every operation, so there is
        /// nothing to defer.</summary>
        public void DeferRefreshUntilSearchInputUnfocused()
        {
        }

        /// <summary>Kept for the detector, which calls it whenever the collection changes. The graph is
        /// declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Update()
        {
            base.Update();
            _editor.Update(IsPresent());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
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
            BuildDropdown(builder, _adapter.FilterDropdown);
            BuildDropdown(builder, _adapter.SortDropdown);

            builder.BeginStop(ItemsStop);
            BuildItems(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the tab pair ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsCollectionAdapter.TabItem> tabs = _adapter.GetTabs();
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
                () => _adapter.SearchFilterLabel,
                () => _adapter.OpenSearchFilter(),
                () => _adapter.HasSearchFilter);
            builder.AddItem(Synthetic("search-filter", searchFilter));

            NodeVtable downloads = GraphNodes.Button(
                () => _adapter.DownloadsLabel,
                () => _adapter.OpenDownloadsMenu(),
                () => _adapter.HasDownloadsMenu);
            builder.AddItem(Synthetic("downloads", downloads));
        }

        // ---- the filtering band ----

        /// <summary>The keyword box. It is one of mod.io's own TMP fields rather than one of the
        /// game's, and the editing contract is the same; its label is the box's own placeholder, which
        /// is the only thing the band writes next to it.</summary>
        private void BuildKeyword(GraphBuilder builder)
        {
            TMP_InputField field = _adapter.SearchField;
            if (field == null || !field.gameObject.activeInHierarchy)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.EditField(
                () => _adapter.SearchFieldLabel,
                () => _editor.Editing ? null : field.text,
                () => _editor.Request(_adapter.SearchField),
                () => field.interactable);
            // Arriving puts the game's own selection on the box - the search filter panel's finding:
            // without it an activation that follows a row whose focus visual selected one of mod.io's
            // own controls selects the box but never makes it FOCUSED, and the edit ends in silence.
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(field);
            builder.AddItem(new DrawnNode(
                ControlId.For(field, "community-maps-collection:keyword"),
                vtable,
                field));
        }

        private void BuildCheckForUpdates(GraphBuilder builder)
        {
            CommunityMapsCollectionAdapter.ButtonAction action = _adapter.CheckForUpdatesAction;
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
            IReadOnlyList<CommunityMapsCollectionAdapter.CollectionItem> items = _adapter.GetItems();
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
                    () => _adapter.ActivateItem(captured));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => captured.Status));
                vtable.OnFocusVisual = () => _adapter.FocusItem(captured);
                builder.AddItem(Synthetic("item/" + captured.Index, vtable));
            }
        }

        // ---- the way out ----

        private void BuildFooter(GraphBuilder builder)
        {
            NodeVtable close = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.Close());
            builder.AddItem(Synthetic("close", close));
        }

        private SyntheticNode Synthetic(string key, NodeVtable vtable)
        {
            return new SyntheticNode(
                ControlId.For(Marker(key), "community-maps-collection:" + key),
                vtable);
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
