using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's map select page, made navigable as a graph. Four places to be, and Tab moves
    /// between them: the filter buttons of the header band, the table of maps, the preview panel
    /// beside it, and the page's buttons.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the header band at y 104
    /// (<c>HeaderWithSortButtons</c>, seven <c>TableSortUIButton</c>s at x 95, 151, 376, 523, 690,
    /// 795 and 880) with the filter buttons drawn over it at y 108 (x 122, 495, 661, 767, 851, 906);
    /// the maps below it as <c>SelectMapLobbyMenuEntry(Clone)</c> rows 34 px tall at x 95, each
    /// drawing its type icon, name, tag text, win-condition icons, player count, size and its played
    /// badge; the preview panel at x 954 with the map's name at y 319, its description in a scroll
    /// rect at y 367 and the win-condition icons at y 298; Confirm at [974,677], and the lobby's Back
    /// (x 21) and Options (x 1233) in the band above.
    ///
    /// The table is a <see cref="GraphSheet"/> with ONE region, named with the page's own title (the
    /// game draws no caption over the table itself). Name is the primary column, so Up and Down read
    /// the map and the vertical crossings into a metadata column say which map it is; a column's
    /// caption is spoken as the edge crossed into it. The WIN CONDITION cell is read as one piece per
    /// drawn icon under that one column, each with the icon's own tooltip, so a map with two ways to
    /// win is two steps rather than one sentence. The heading band is a ROW of the table's own stop,
    /// declared above the first map so Up reaches it, and the stop's Tab landing is pinned to the
    /// first data row (<c>GraphBuilder.LandStopOn</c>) - which is also what lets the landing find the
    /// SELECTED map, since the search for the alternative in force starts there.
    ///
    /// ARRIVING ON A ROW SELECTS THAT MAP: the row's focus visual is the game's own
    /// <c>SetSelectedEntry</c> (through <c>FocusEntry</c>), which is what fills the preview, so there
    /// is no native way to look at a map without picking it - the same as the random layout page's
    /// cards. Enter is the entry's own click and does the same thing.
    ///
    /// The filters are expandable groups: Right opens the game's own checkbox list
    /// (<c>UIFilterDropdown.Show</c>) and lands on its first box, Left closes it again, and the
    /// expanded state is read off the dropdown's own container every build.
    ///
    /// Escape: <c>MapSelectMenu.SetupAndAnimateAfterLoad</c> registers only
    /// <c>InputActions.UI.Confirm</c> on its keyboard branch (decompiled, line 296) and
    /// <c>LobbyNavigation</c> registers no input callback at all, so the key would do nothing here;
    /// the screen claims it and presses the drawn Back button.
    /// </summary>
    public sealed class AdventureLobbyMapSelectScreen : GraphScreen
    {
        private const string FiltersStop = "map-select-filters";
        private const string TableStop = "map-select-table";
        private const string DetailsStop = "map-select-details";
        private const string ButtonsStop = "map-select-buttons";
        private const string SheetKey = "map-select:";

        /// <summary>The adapter's column index behind each of the sheet's logical columns. Name leads
        /// because the sheet emits the primary first, whatever the game draws leftmost; the rest are
        /// in the drawn order of the header band (Type, Tag, Win Condition, Players, Size, Completed).
        /// </summary>
        private static readonly int[] SheetColumns = { 1, 0, 2, 3, 4, 5, 6 };

        private readonly AdventureLobbyMapSelectAdapter _adapter;

        // A subject of its own for the preview line, kept across rebuilds so the reconciler seats the
        // cursor on the same node while the selection under it changes.
        private readonly object _detailsMarker = new object();

        public AdventureLobbyMapSelectScreen(AdventureLobbyMapSelectAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyMapSelectAdapter adapter = FindActiveMapSelectMenu(null);
            return adapter != null ? new AdventureLobbyMapSelectScreen(adapter) : null;
        }

        public bool Matches(MapSelectMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "map-select"; }
        }

        /// <summary>The page's own drawn title ("Select Map").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        /// <summary>The filters, which is where the page starts: the table is the whole point of the
        /// page, but a player arriving on it cannot tell that the list has been narrowed, and the
        /// filters are drawn above the table.</summary>
        public override object InitialFocusStop
        {
            get { return FiltersStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.Activate();
        }

        /// <summary>Kept for the detector, which calls them whenever the page or its selection
        /// changes. The graph is declared afresh on every operation, so there is nothing to rebuild.
        /// </summary>
        public void Refresh()
        {
        }

        public void Refresh(bool announceFocus)
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(FiltersStop);
            BuildFilters(builder);

            builder.BeginStop(TableStop);
            BuildTable(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // ---- the filter buttons of the header band ----

        /// <summary>The whole stop stands under one context named "Filters", which is what the path diff
        /// says on the way in: the game draws no caption over the band, and a stop that opens with
        /// "Type, group, collapsed" tells a player arriving from the table nothing about where they
        /// are.</summary>
        private void BuildFilters(GraphBuilder builder)
        {
            builder.PushContext(ModText.Get(ModStrings.UI.Filters));
            IReadOnlyList<MapSelectFilterAdapter> filters = _adapter.GetFilters();
            for (int i = 0; i < filters.Count; i++)
            {
                MapSelectFilterAdapter filter = filters[i];
                Component subject = filter != null ? filter.Subject : null;
                if (subject == null || !filter.IsVisible)
                {
                    continue;
                }

                MapSelectFilterAdapter it = filter;
                NodeVtable group = GraphNodes.Group(() => it.Label);
                // The list is the game's own popup, so opening the group opens it and closing the
                // group closes it; the state is read back off the dropdown, so a list the game shuts
                // on its own (a click elsewhere) collapses the group with no hook needed.
                group.OnExpand = () => it.OpenNative();
                group.OnCollapse = () => it.CloseNative();
                builder.BeginGroup(
                    new DrawnNode(ControlId.For(subject, "map-select:filter/" + i), group, subject),
                    expanded: it.IsOpen);
                BuildFilterOptions(builder, it, i);
                builder.EndGroup();
            }

            AddButton(builder, "map-select:clear-filters", _adapter.GetClearFiltersButton());
            builder.PopContext();
        }

        private static void BuildFilterOptions(GraphBuilder builder, MapSelectFilterAdapter filter, int index)
        {
            IReadOnlyList<MapSelectFilterAdapter.Option> options = filter.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                MapSelectFilterAdapter.Option option = options[i];
                Component subject = option != null ? option.Subject : null;
                if (subject == null || !option.IsVisible)
                {
                    continue;
                }

                MapSelectFilterAdapter.Option it = option;
                NodeVtable box = GraphNodes.Checkbox(
                    () => it.Label,
                    () => it.IsChecked,
                    it.Toggle,
                    () => it.IsEnabled,
                    it.GetTooltip());
                box.OnFocusVisual = it.FocusNative;
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "map-select:filter/" + index + "/" + it.Index),
                    box,
                    subject));
            }
        }

        // ---- the table ----

        private void BuildTable(GraphBuilder builder)
        {
            IReadOnlyList<string> captions = _adapter.GetColumnLabels();
            BuildSortBand(builder, captions);

            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(_adapter.Title, SheetCaptions(captions));
            IReadOnlyList<AdventureLobbyMapSelectRowAdapter> rows = _adapter.GetVisibleRows();
            for (int i = 0; i < rows.Count; i++)
            {
                AdventureLobbyMapSelectRowAdapter row = rows[i];
                if (row == null || row.Entry == null)
                {
                    continue;
                }

                sheet.RowAt(Primary(row), row.NativeKey, Cells(row, captions), row.Entry);
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on a MAP, never on the heading band above it - and, since
                // the search for the alternative in force starts here, on the map the game opened on.
                builder.LandStopOn(sheet.FirstRow);
            }
        }

        /// <summary>
        /// The drawn heading band, as a row of the table's own stop immediately above the first map:
        /// Up out of a row reaches the heading of the column the cursor was in, and Down comes back.
        /// The row carries no positions - "1 of 7" there would count the table's columns, which is not
        /// a place in a list - and each heading is stamped with the column it stands over so the seam
        /// pairs column by column.
        /// </summary>
        private void BuildSortBand(GraphBuilder builder, IReadOnlyList<string> captions)
        {
            IReadOnlyList<MapSelectSortButtonAdapter> sortButtons = _adapter.GetSortButtons();
            builder.StartRow(positions: false);
            for (int column = 0; column < SheetColumns.Length; column++)
            {
                MapSelectSortButtonAdapter sort = At(sortButtons, SheetColumns[column]);
                Component subject = sort != null ? sort.Button : null;
                if (subject == null)
                {
                    continue;
                }

                MapSelectSortButtonAdapter it = sort;
                Func<string> caption = Caption(captions, SheetColumns[column]);
                NodeVtable vtable = GraphNodes.Button(caption, () => it.Activate());
                // The game draws the sorted column's arrow, so the heading says which way it points.
                vtable.Announcements.Add(GraphNodes.ValuePart(() => SortDirection(it)));
                vtable.Column = column;
                // A heading is not a cell of the row below it, so the sheet's one-result-per-row
                // filter would otherwise drop every heading past the first from type-ahead.
                vtable.SearchesAsItself = true;
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(subject);
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "map-select:sort/" + column),
                    vtable,
                    subject));
            }

            builder.EndRow();
        }

        private static string SortDirection(MapSelectSortButtonAdapter sort)
        {
            MapSelectSortDirection direction = sort.Direction;
            if (direction == MapSelectSortDirection.Ascending)
            {
                return ModText.Get(ModStrings.UI.SortAscending);
            }

            return direction == MapSelectSortDirection.Descending
                ? ModText.Get(ModStrings.UI.SortDescending)
                : null;
        }

        /// <summary>The map's own cell: its name, whether it is the map the page has selected, and the
        /// game's own click. Nothing is said about the click beyond what the live parts say, the
        /// selection being what arriving here already did.</summary>
        private static NodeVtable Primary(AdventureLobbyMapSelectRowAdapter row)
        {
            AdventureLobbyMapSelectRowAdapter it = row;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => it.Name),
                    GraphNodes.SelectedPart(() => it.IsSelected),
                },
                OnActivate = () => it.Activate(),
                OnFocusVisual = it.FocusNative,
            };
            return vtable;
        }

        /// <summary>The map's metadata cells, in the sheet's column order. The win condition is read as
        /// one piece per drawn icon under its one column; every other column is one piece.</summary>
        private static List<GraphSheet.SheetCell> Cells(
            AdventureLobbyMapSelectRowAdapter row,
            IReadOnlyList<string> captions)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            AdventureLobbyMapSelectRowAdapter it = row;
            Add(cells, 1, captions, row, () => it.TypeLabel, it.GetCellTooltip("type"));
            Add(cells, 2, captions, row, () => ModText.JoinList(it.TagLabels), it.GetCellTooltip("tag"));

            IReadOnlyList<string> conditions = row.WinConditionLabels;
            IReadOnlyList<Tooltip> tooltips = row.WinConditionTooltips;
            if (conditions.Count == 0)
            {
                Add(cells, 3, captions, row, () => string.Empty, null);
            }
            else
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    string condition = conditions[i];
                    cells.Add(new GraphSheet.SheetCell(3, i, Cell(
                        captions,
                        3,
                        row,
                        () => condition,
                        i < tooltips.Count ? tooltips[i] : null)));
                }
            }

            Add(cells, 4, captions, row, () => it.Players > 0 ? it.Players.ToString() : string.Empty, null);
            Add(cells, 5, captions, row, () => it.SizeLabel, null);
            Add(cells, 6, captions, row, () => it.IsCompleted ? it.CompletedLabel : it.NotCompletedLabel, null);
            return cells;
        }

        private static void Add(
            List<GraphSheet.SheetCell> cells,
            int column,
            IReadOnlyList<string> captions,
            AdventureLobbyMapSelectRowAdapter row,
            Func<string> value,
            Tooltip tooltip)
        {
            cells.Add(new GraphSheet.SheetCell(column, 0, Cell(captions, column, row, value, tooltip)));
        }

        /// <summary>
        /// One read-only cell: the drawn value alone, since the column's caption is spoken as the edge
        /// crossed into it, with the caption and the value together as what the review buffer opens
        /// with - nobody arrives at a buffer across an edge. An empty cell reads the sheet's own blank
        /// word rather than being dropped, so the columns stay the same all the way down.
        ///
        /// EVERY CELL CARRIES THE ROW'S CLICK: a player reading across a map's size or player count and
        /// pressing Enter means "this map", and having to walk back to the Name column first is a rule
        /// the drawn table does not have - clicking anywhere on the row selects it.
        /// </summary>
        private static NodeVtable Cell(
            IReadOnlyList<string> captions,
            int column,
            AdventureLobbyMapSelectRowAdapter row,
            Func<string> value,
            Tooltip tooltip)
        {
            AdventureLobbyMapSelectRowAdapter it = row;
            Func<string> caption = Caption(captions, SheetColumns[column]);
            Func<string> text = () => Filled(value());
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text, watch: false) },
                Sections = GraphNodes.Sections(null, tooltip),
                // One search result per map, whichever column the cursor is standing in.
                SearchText = () => it.Name,
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption(), text()),
                OnActivate = () => it.Activate(),
            };
            GraphNodes.Aim(vtable, tooltip);
            return vtable;
        }

        private static string Filled(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return GraphSheet.BlankText != null ? GraphSheet.BlankText() : string.Empty;
        }

        /// <summary>The column captions in the sheet's own order, the primary's first.</summary>
        private static string[] SheetCaptions(IReadOnlyList<string> captions)
        {
            string[] columns = new string[SheetColumns.Length];
            for (int i = 0; i < SheetColumns.Length; i++)
            {
                columns[i] = Caption(captions, SheetColumns[i])();
            }

            return columns;
        }

        private static Func<string> Caption(IReadOnlyList<string> captions, int index)
        {
            string caption = captions != null && index >= 0 && index < captions.Count
                ? captions[index]
                : string.Empty;
            return () => caption;
        }

        // ---- the preview panel ----

        /// <summary>
        /// The preview beside the table, as the one line it is: the map's name as the panel draws it,
        /// then the description it draws under it, read on arrival and held in the review buffer one
        /// drawn line at a time, a map's dossier running to a paragraph or more. The name is watched
        /// live, so the panel being refilled under a standing cursor says which map it is now showing.
        ///
        /// THE WIN CONDITIONS ARE NOT READ OUT. The panel draws them as ICONS whose words the game only
        /// reveals on hover (<c>LobbyMapPreview</c> hangs each icon's <c>GameModes/*/Name</c> and
        /// objective on it as a tooltip), so a sentence naming them is not something the page says: it
        /// is buffer-only, where the player who wants it goes to look. The description, by contrast, is
        /// drawn text (<c>LobbyMapPreviewText.GetInfo</c> reads the panel's own <c>_mpInfo</c> mesh) and
        /// stays in the readout.
        /// </summary>
        private void BuildDetails(GraphBuilder builder)
        {
            AdventureLobbyMapSelectRowAdapter selected = _adapter.SelectedRow;
            if (selected == null)
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => PreviewTitle(), live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Composed(() => SpokenLines.Of(new[] { Description() })),
                    NodeSection.Buffer(() => SpokenLines.Of(new[] { PreviewWinConditions() })),
                },
            };
            builder.AddItem(new SyntheticNode(
                ControlId.For(_detailsMarker, "map-select:preview"),
                vtable));
        }

        private string PreviewTitle()
        {
            string title = _adapter.PreviewTitle;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            AdventureLobbyMapSelectRowAdapter selected = _adapter.SelectedRow;
            return selected != null ? selected.Name : string.Empty;
        }

        private string Description()
        {
            AdventureLobbyMapSelectRowAdapter selected = _adapter.SelectedRow;
            return selected != null ? selected.Description : string.Empty;
        }

        private string PreviewWinConditions()
        {
            AdventureLobbyMapSelectRowAdapter selected = _adapter.SelectedRow;
            return selected != null ? ModText.JoinList(selected.WinConditionLabels) : string.Empty;
        }

        // ---- the page's buttons ----

        private void BuildButtons(GraphBuilder builder)
        {
            // Back (x 21) and Options (x 1233) in the header band, then Confirm at the bottom right.
            AddButton(builder, "map-select:back", _adapter.BackButton);
            AddButton(builder, "map-select:options", _adapter.OptionsButton);
            AddButton(builder, "map-select:confirm", _adapter.SelectButton);
        }

        private static void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(button.GetLabel, () => button.Activate(), button.IsEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button.Button);
            builder.AddItem(new DrawnNode(ControlId.For(button.Button, key), vtable, button.Button));
        }

        private static T At<T>(IReadOnlyList<T> items, int index) where T : class
        {
            return items != null && index >= 0 && index < items.Count ? items[index] : null;
        }

        public static AdventureLobbyMapSelectAdapter FindActiveMapSelectMenu(MapSelectMenu targetMenu)
        {
            MapSelectMenu[] menus = Resources.FindObjectsOfTypeAll<MapSelectMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapSelectMenu menu = menus[i];
                if (!IsLiveSceneMapSelectMenu(menu))
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneMapSelectMenu(MapSelectMenu menu)
        {
            if (menu == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)menu).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
