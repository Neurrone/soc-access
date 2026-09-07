using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The summary a finished game ends on, made navigable as a graph. Four places to be, in the
    /// order the menu draws them: the chooser (which graph, and which teams are on it), the mod's own
    /// table of the chart, the two figures under the chart, and the close cross.
    ///
    /// Measured 2026-09-07 (<c>PostAdventureStatsMenu</c>): the title "Summary" at the top, the graph
    /// dropdown top left with the team list under it, the chart to the right of them (its axis labels
    /// are loose texts the menu spawns), "Number of rounds" and "Play time" under the chart, and the
    /// close cross top right. The teams are drawn in REVERSE of the order they are spawned in
    /// (spawned Neurrone, Nealuchi, Caldwell, Neelf; drawn Neelf at the top), so they are declared in
    /// the order of their drawn tops, measured every build.
    ///
    /// THE GRAPH TYPE IS A COMBO BOX and the teams are CHECKBOXES (owner ruling, 2026-09-07):
    /// arriving on a graph type must not change the chart, because switching one respawns every dot,
    /// every line and every label, so the choice is taken on Enter through the mod's own drop list
    /// over the game's popup. A team's tick is an ordinary toggle: the game rebuilds the chart from
    /// <c>HandleTeamEntryEnabledChanged</c> and the next build of this page reads the new columns.
    /// The dropdown draws no caption of its own - it draws the option it is set to - so the mod names
    /// it "Graph type".
    ///
    /// THE TABLE IS THE MOD'S, and stays one (owner ruling): the chart is a picture, and the numbers
    /// behind it are read as a sheet of one row per round with a column per enabled team, under a
    /// heading band the mod draws for it. A round a team lost a battle in says so after the figure,
    /// which is the skull the chart draws over that dot.
    ///
    /// ESCAPE IS THE GAME'S (<c>ConsumesBack</c> false): <c>PostAdventureStatsMenu.ShowMenu</c>
    /// registers <c>UI.ExitMenu</c> on its own close.
    /// </summary>
    public sealed class PostAdventureStatsScreen : GraphScreen
    {
        private const string ChooserStop = "post-adventure-stats-chooser";
        private const string TableStop = "post-adventure-stats-table";
        private const string FooterStop = "post-adventure-stats-footer";
        private const string CloseStop = "post-adventure-stats-close";
        private const string SheetKey = "post-adventure-stats:";

        private static readonly FieldInfo StatsMenuField = AccessTools.Field(typeof(PostAdventureMenu), "_statsMenu");

        private readonly PostAdventureStatsAdapter _adapter;

        // A subject of its own per synthesized line, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the menu gives no component the screen can key its figures on.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public PostAdventureStatsScreen(PostAdventureStatsAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PostAdventureMenu[] resultMenus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < resultMenus.Length; i++)
            {
                PostAdventureStatsMenu statsMenu = GetStatsMenu(resultMenus[i]);
                PostAdventureStatsAdapter adapter = new PostAdventureStatsAdapter(statsMenu);
                if (adapter.IsPresent())
                {
                    return new PostAdventureStatsScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "post-adventure-stats"; }
        }

        /// <summary>The menu's own drawn title ("Summary").</summary>
        public override string ScreenName
        {
            get
            {
                string header = _adapter != null ? _adapter.Header : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it when the page's content changes. The graph is
        /// declared afresh on every operation and reads the chart off the game each time, so there is
        /// nothing here to rebuild.</summary>
        public void Refresh(bool announceFocus = false)
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(ChooserStop);
            BuildChooser(builder);

            builder.BeginStop(TableStop);
            BuildTable(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- which graph, and whose lines ----

        private void BuildChooser(GraphBuilder builder)
        {
            BuildGraphType(builder);
            BuildTeams(builder);
        }

        /// <summary>The graph type, as the drop list the game's own dropdown opens. Enter opens it;
        /// what the chart shows does not move until an entry is taken.</summary>
        private void BuildGraphType(GraphBuilder builder)
        {
            PostAdventureStatsAdapter.GraphDropList chooser = _adapter.GraphChooser;
            Component subject = chooser != null ? chooser.Subject : null;
            if (subject == null || !chooser.IsVisible())
            {
                return;
            }

            PostAdventureStatsAdapter.GraphDropList it = chooser;
            Func<string> label = () => ModText.Get(ModStrings.Screens.GraphType);
            NodeVtable vtable = GraphNodes.ComboBox(
                label,
                () => SelectedOption(it),
                () => DropListScreen.Open(it, label(), index => _adapter.SelectGraph(index)),
                it.IsEnabled);
            vtable.OnFocusVisual = () => _adapter.FocusGraphDropdown();
            builder.AddItem(new DrawnNode(
                ControlId.For(subject, "post-adventure-stats:graph-type"),
                vtable,
                subject));
        }

        private static string SelectedOption(PostAdventureStatsAdapter.GraphDropList chooser)
        {
            IReadOnlyList<string> options = chooser.GetOptions();
            int value = chooser.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        /// <summary>One tick per team, in the order the menu draws them down the page.</summary>
        private void BuildTeams(GraphBuilder builder)
        {
            List<PostAdventureStatsAdapter.TeamOption> teams = DrawnOrder(_adapter.GetTeamOptions());
            for (int i = 0; i < teams.Count; i++)
            {
                PostAdventureStatsAdapter.TeamOption team = teams[i];
                Component entry = team.Entry;
                if (entry == null)
                {
                    continue;
                }

                PostAdventureStatsAdapter.TeamOption it = team;
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => it.Label,
                    () => _adapter.IsTeamSelected(it.Entry),
                    () => _adapter.ToggleTeam(it.Entry));
                vtable.OnFocusVisual = () => _adapter.FocusTeam(it.Entry);
                // The structural key carries the drawn index: a ControlId is equal on its structural
                // key alone, so the teams under one key would be one duplicate id.
                builder.AddItem(new DrawnNode(
                    ControlId.For(entry, "post-adventure-stats:team/" + i),
                    vtable,
                    entry));
            }
        }

        /// <summary>The teams top to bottom as the menu draws them, measured off each entry's own
        /// transform every build; the insertion sort is stable, so two entries at one y keep the
        /// order the menu spawned them in.</summary>
        private static List<PostAdventureStatsAdapter.TeamOption> DrawnOrder(
            IReadOnlyList<PostAdventureStatsAdapter.TeamOption> teams)
        {
            List<PostAdventureStatsAdapter.TeamOption> drawn = new List<PostAdventureStatsAdapter.TeamOption>();
            List<float> tops = new List<float>();
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                PostAdventureStatsAdapter.TeamOption team = teams[i];
                if (team == null || team.Entry == null)
                {
                    continue;
                }

                drawn.Add(team);
                tops.Add(team.Entry.transform != null ? team.Entry.transform.position.y : 0f);
            }

            for (int i = 1; i < drawn.Count; i++)
            {
                PostAdventureStatsAdapter.TeamOption moving = drawn[i];
                float top = tops[i];
                int j = i - 1;
                while (j >= 0 && tops[j] < top)
                {
                    drawn[j + 1] = drawn[j];
                    tops[j + 1] = tops[j];
                    j--;
                }

                drawn[j + 1] = moving;
                tops[j + 1] = top;
            }

            return drawn;
        }

        // ---- the chart, as a table ----

        private void BuildTable(GraphBuilder builder)
        {
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams = _adapter.GetEnabledGraphTeams();
            string[] columns = Columns(teams);
            BuildHeadingBand(builder, teams, columns);

            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(_adapter.GraphTitle, columns);
            IReadOnlyList<PostAdventureStatsAdapter.GraphRoundRow> rows = _adapter.GetGraphRows();
            for (int i = 0; i < rows.Count; i++)
            {
                PostAdventureStatsAdapter.GraphRoundRow row = rows[i];
                if (row == null)
                {
                    continue;
                }

                sheet.Row(RoundPrimary(row), row.Id, null, TeamCells(row, teams));
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on a ROUND, never on the heading band above it.
                builder.LandStopOn(sheet.FirstRow);
            }
        }

        /// <summary>The captions of the table, the round first: the columns are the mod's own, since
        /// the game draws a chart rather than a grid, so the crossing into a column is the only place
        /// a team's name is said while the player is reading figures.</summary>
        private static string[] Columns(IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams)
        {
            string[] columns = new string[teams.Count + 1];
            columns[0] = ModText.Get(ModStrings.UI.ColumnRound);
            for (int i = 0; i < teams.Count; i++)
            {
                columns[i + 1] = teams[i].Label;
            }

            return columns;
        }

        /// <summary>The captions as a row of the table's own stop immediately above the first round:
        /// Up out of a row reaches the heading of the column the cursor was in, and Down comes back.
        /// The row carries no positions - "1 of 5" there would count the table's columns, which is not
        /// a place in a list.</summary>
        private static void BuildHeadingBand(
            GraphBuilder builder,
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams,
            string[] columns)
        {
            builder.StartRow(null, false);
            for (int i = 0; i < columns.Length; i++)
            {
                string caption = columns[i];
                NodeVtable vtable = GraphNodes.Text(() => caption);
                vtable.Column = i;
                // A heading is not a cell of the row below it, so the sheet's one-result-per-row filter
                // would otherwise drop every heading past the first from type-ahead.
                vtable.SearchesAsItself = true;
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("post-adventure-stats:heading/" + (i == 0 ? "round" : teams[i - 1].Id)),
                    vtable));
            }

            builder.EndRow();
        }

        /// <summary>The row's own cell: the round number, which is what names the row on a vertical
        /// crossing.</summary>
        private static NodeVtable RoundPrimary(PostAdventureStatsAdapter.GraphRoundRow row)
        {
            string round = row.Round.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return GraphNodes.Text(() => round);
        }

        /// <summary>One figure per enabled team, in column order; a team with nothing recorded for the
        /// round reads as the sheet's own blank.</summary>
        private static Func<string>[] TeamCells(
            PostAdventureStatsAdapter.GraphRoundRow row,
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams)
        {
            Func<string>[] cells = new Func<string>[teams.Count];
            for (int i = 0; i < teams.Count; i++)
            {
                int teamId = teams[i].TeamId;
                cells[i] = () => row.GetValue(teamId);
            }

            return cells;
        }

        // ---- the figures under the chart ----

        private void BuildFooter(GraphBuilder builder)
        {
            AddLine(builder, "rounds", () => _adapter.TotalRounds);
            AddLine(builder, "playtime", () => _adapter.TotalPlayTime);
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            Component close = _adapter.CloseButton;
            if (close == null || !_adapter.IsCloseButtonVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.Close(),
                _adapter.IsCloseButtonEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(
                ControlId.For(close, "post-adventure-stats:close"),
                vtable,
                close));
        }

        // ---- the lines the menu gives nothing to key on ----

        private void AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(text()))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "post-adventure-stats:" + key),
                GraphNodes.Text(text)));
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

        private static PostAdventureStatsMenu GetStatsMenu(PostAdventureMenu resultMenu)
        {
            return resultMenu != null && StatsMenuField != null
                ? StatsMenuField.GetValue(resultMenu) as PostAdventureStatsMenu
                : null;
        }
    }
}
