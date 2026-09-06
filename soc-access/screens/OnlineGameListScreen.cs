using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The online game list, made navigable as a graph. Three stops: the region the list is fetched
    /// for, the list itself, and the commands under it.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the window at [196,104,889,593]; the
    /// Region dropdown at [803,125,238,27] above everything; the drawn heading band `TitleBar` at
    /// [237,157,795,26] with a status icon (x 237, no caption of its own), "Game name" (x 309) and
    /// "Players" (x 837); the games as `GameListEntry(Clone)` rows 36 px tall at x 237, each drawing
    /// a status icon (x 256), the game's name (x 312) and its player count (x 839); and the commands
    /// along the bottom at y 632 - Host Game (x 233), Load and Host Game (416), Join With Game Code
    /// (689) and Join Game (873). Back ("Main Menu", x 21) and Options (x 1233) are the main menu's
    /// header band.
    ///
    /// The list is a <see cref="GraphSheet"/> of one region with Game name as the primary column. The
    /// heading band is drawn and so is declared as a row above the first game - as read-only text,
    /// because these headings are images the game wires no click to: this table does not sort.
    ///
    /// Two lines appear only while the game draws them. The STATUS ("Looking for games", "Connecting")
    /// is an overlay the game puts over the whole list area, so it reads at the top of the list's own
    /// stop and an empty list is then the band and that one line. The SELECTED GAME line names what
    /// Join Game would take, and reads at the head of the commands.
    ///
    /// Escape: `GameListMenu.ReregisterDefaultInput` registers a gamepad button and nothing else
    /// (decompiled, line 251), so the key would do nothing here; the screen claims it and presses the
    /// drawn Main Menu button, as the widget screen did.
    /// </summary>
    public sealed class OnlineGameListScreen : GraphScreen
    {
        private const string RegionStop = "online-game-region";
        private const string TableStop = "online-game-table";
        private const string ButtonsStop = "online-game-buttons";
        private const string SheetKey = "online-game:";

        private readonly OnlineGameListAdapter _adapter;

        // Subjects of their own for the two lines the game draws no widget the mod can key on, kept
        // across rebuilds so the reconciler seats the cursor on the same line.
        private readonly object _statusMarker = new object();
        private readonly object _selectedMarker = new object();
        private readonly object[] _bandMarkers = { new object(), new object(), new object() };

        public OnlineGameListScreen(OnlineGameListAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OnlineGameListAdapter adapter = OnlineGameListAdapter.TryCreateActive();
            return adapter != null ? new OnlineGameListScreen(adapter) : null;
        }

        public bool Matches(GameListMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "online-game-list"; }
        }

        /// <summary>The page's own drawn title ("Game List").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return TableStop; }
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

        /// <summary>Kept for the detector, which calls them as the list arrives from the network. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
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

            builder.BeginStop(RegionStop);
            BuildRegion(builder);

            builder.BeginStop(TableStop);
            BuildTable(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        private void BuildRegion(GraphBuilder builder)
        {
            OnlineGameListAdapter.RegionDropList region = _adapter.Region;
            Component subject = region != null ? region.Subject : null;
            if (subject == null || !region.IsVisible())
            {
                return;
            }

            OnlineGameListAdapter.RegionDropList it = region;
            Func<string> label = () => _adapter.RegionLabel;
            NodeVtable vtable = GraphNodes.ComboBox(
                label,
                () => CurrentOption(it),
                () => DropListScreen.Open(it, label(), index => it.SetValue(index)),
                it.IsEnabled);
            vtable.OnFocusVisual = it.Focus;
            builder.AddItem(new DrawnNode(ControlId.For(subject, "online-game:region"), vtable, subject));
        }

        private static string CurrentOption(OnlineGameListAdapter.RegionDropList region)
        {
            IReadOnlyList<string> options = region.GetOptions();
            int value = region.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        private void BuildTable(GraphBuilder builder)
        {
            if (_adapter.IsStatusVisible)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_statusMarker, "online-game:status"),
                    GraphNodes.Text(() => _adapter.StatusText)));
            }

            string[] captions =
            {
                GameText.Get("Lobby/GameList/GameName", "Game Name"),
                ModText.Get(ModStrings.UI.Status),
                GameText.Get("Common/Players", "Players")
            };
            BuildHeadingBand(builder, captions);

            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(_adapter.Title, captions);
            IReadOnlyList<OnlineGameListAdapter.GameRow> rows = _adapter.GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                OnlineGameListAdapter.GameRow row = rows[i];
                if (row == null || row.Entry == null)
                {
                    continue;
                }

                sheet.RowAt(Primary(row), row.Id, Cells(row, captions), row.Entry);
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                builder.LandStopOn(sheet.FirstRow);
            }
        }

        /// <summary>
        /// The heading band the game draws over the list, as a row above the first game so that Up out
        /// of a row reaches its own column's heading. Read-only: the headings are images
        /// (`TitleStatus`, `TitleEntryGameName`, `TitleEntryPlayers`) with no click on them, this list
        /// having no sorting at all. The status column's heading draws an icon and no words, so it
        /// takes the mod's own word for it, as the widget table did.
        /// </summary>
        private void BuildHeadingBand(GraphBuilder builder, IReadOnlyList<string> captions)
        {
            builder.StartRow(positions: false);
            for (int column = 0; column < captions.Count; column++)
            {
                string caption = captions[column];
                NodeVtable vtable = GraphNodes.Text(() => caption);
                vtable.Column = column;
                // A heading is not a cell of the row below it, so the sheet's one-result-per-row
                // filter would otherwise drop every heading past the first from type-ahead.
                vtable.SearchesAsItself = true;
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_bandMarkers[column], "online-game:heading/" + column),
                    vtable));
            }

            builder.EndRow();
        }

        /// <summary>The game's own cell: its name and the game's own click, which is what selects it
        /// for Join Game and fills the selected-game line.</summary>
        private static NodeVtable Primary(OnlineGameListAdapter.GameRow row)
        {
            OnlineGameListAdapter.GameRow it = row;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(() => Plain(it.Name)) },
                OnActivate = () => it.Activate(),
                OnFocusVisual = it.FocusNative,
            };
        }

        private static List<GraphSheet.SheetCell> Cells(
            OnlineGameListAdapter.GameRow row,
            IReadOnlyList<string> captions)
        {
            OnlineGameListAdapter.GameRow it = row;
            return new List<GraphSheet.SheetCell>
            {
                new GraphSheet.SheetCell(1, 0, Cell(captions, 1, row, () => it.Status, it.GetCellTooltip("status"))),
                new GraphSheet.SheetCell(2, 0, Cell(captions, 2, row, () => it.Players, null)),
            };
        }

        /// <summary>One read-only cell: the drawn value alone, the column's caption being spoken as the
        /// edge crossed into it, with the caption and the value as the buffer's head. Every cell carries
        /// the row's click, as the map select table's do: Enter anywhere along a row means that row.
        /// </summary>
        private static NodeVtable Cell(
            IReadOnlyList<string> captions,
            int column,
            OnlineGameListAdapter.GameRow row,
            Func<string> value,
            Tooltip tooltip)
        {
            OnlineGameListAdapter.GameRow it = row;
            string caption = captions != null && column < captions.Count ? captions[column] : string.Empty;
            Func<string> text = () => Filled(Plain(value()));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text) },
                Sections = GraphNodes.Sections(null, tooltip),
                SearchText = () => Plain(it.Name),
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption, text()),
                OnActivate = () => it.Activate(),
            };
            GraphNodes.Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>What the game wrote, without the renderer's markup: this list draws its player
        /// count with the game's own colour tags ("2/&lt;low&gt;4&lt;/low&gt;"), which a screen reader
        /// must not spell out.</summary>
        private static string Plain(string value)
        {
            return string.Join(" ", SpokenLines.Of(new[] { value }));
        }

        private static string Filled(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return GraphSheet.BlankText != null ? GraphSheet.BlankText() : string.Empty;
        }

        private void BuildButtons(GraphBuilder builder)
        {
            if (_adapter.IsSelectedEntryTextVisible)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_selectedMarker, "online-game:selected"),
                    GraphNodes.Text(() => _adapter.SelectedEntryText)));
            }

            AddButton(builder, "online-game:host", _adapter.HostGameButton);
            AddButton(builder, "online-game:load-and-host", _adapter.HostSavedGameButton);
            AddButton(builder, "online-game:join-with-code", _adapter.JoinWithCodeButton);
            AddButton(builder, "online-game:join", _adapter.JoinSelectedButton);
            AddButton(builder, "online-game:options", _adapter.OptionsButton);
            AddButton(builder, "online-game:back", _adapter.BackButton);
        }

        private void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            IMenuButtonAdapter it = button;
            NodeVtable vtable = GraphNodes.Button(
                it.GetLabel,
                () => it.Activate(),
                it.IsEnabled,
                _adapter.GetButtonTooltip(it));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(it.Button as Component);
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button));
        }
    }
}
