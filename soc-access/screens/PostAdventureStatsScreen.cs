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
    /// behind it are read as a sheet of one row per round with a column per enabled team, each
    /// crossing naming its column (no heading band: it would say the captions twice). A round a team
    /// lost a battle in says so after the figure, which is the skull the chart draws over that dot.
    /// A game with no round recorded declares no table at all; the footer says so.
    ///
    /// ESCAPE IS THE GAME'S (<c>ConsumesBack</c> false): <c>PostAdventureStatsMenu.ShowMenu</c>
    /// registers <c>UI.ExitMenu</c> on its own close.
    /// </summary>
    public sealed class PostAdventureStatsScreen : LiveScreen<PostAdventureStatsAdapter>
    {
        private const string ChooserStop = "post-adventure-stats-chooser";
        private const string TableStop = "post-adventure-stats-table";
        private const string FooterStop = "post-adventure-stats-footer";
        private const string CloseStop = "post-adventure-stats-close";
        private const string SheetKey = "post-adventure-stats:";

        private static readonly FieldInfo StatsMenuField = AccessTools.Field(typeof(PostAdventureMenu), "_statsMenu");

        /// <summary>The one post-adventure stats window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<IPostAdventureStatsMenu> _source =
            ScreenSource<IPostAdventureStatsMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override PostAdventureStatsAdapter Adapt(object menu)
        {
            return new PostAdventureStatsAdapter((PostAdventureStatsMenu)menu);
        }

        public override string Key
        {
            get { return "post-adventure-stats"; }
        }

        /// <summary>Layer 33: over the adventure result that opens it.</summary>
        public override int Layer
        {
            get { return 33; }
        }

        /// <summary>The menu's own drawn title ("Summary").</summary>
        public override string ScreenName
        {
            get
            {
                string header = Live != null ? Live.Header : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(ChooserStop);
            BuildChooser(builder);

            // No table stop at all when the statistics hold no round: the footer says how many
            // rounds there were, and a stop with nothing in it would be a table without rows. The
            // rows are built ONCE - the guard and the table are the same question, and building
            // them walks every team's every round.
            IReadOnlyList<PostAdventureStatsAdapter.GraphRoundRow> graphRows = Live.GetGraphRows();
            if (graphRows.Count > 0)
            {
                builder.BeginStop(TableStop);
                BuildTable(builder, graphRows);
            }

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
            PostAdventureStatsAdapter.GraphDropList chooser = Live.GraphChooser;
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
                () => DropListScreen.Open(it, label(), index => Live.SelectGraph(index)),
                it.IsEnabled);
            vtable.OnFocusVisual = () => Live.FocusGraphDropdown();
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
            List<PostAdventureStatsAdapter.TeamOption> teams = InDrawnOrder(Live.GetTeamOptions());
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
                    () => Live.IsTeamSelected(it.Entry),
                    () => Live.ToggleTeam(it.Entry));
                vtable.OnFocusVisual = () => Live.FocusTeam(it.Entry);
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
        private static List<PostAdventureStatsAdapter.TeamOption> InDrawnOrder(
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

            DrawnOrder.SortDescending(drawn, tops);
            return drawn;
        }

        // ---- the chart, as a table ----

        private void BuildTable(GraphBuilder builder, IReadOnlyList<PostAdventureStatsAdapter.GraphRoundRow> rows)
        {
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams = Live.GetEnabledGraphTeams();
            string[] columns = Columns(teams);
            // No heading band: the sheet names the column on every crossing ("Neurrone, 1234"), so a
            // row of the captions would say them a second time (owner ruling 2026-09-07).
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(Live.GraphTitle, columns);
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
                // Tab into the table lands on a ROUND.
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
            AddLine(builder, "rounds", () => Live.TotalRounds);
            AddLine(builder, "playtime", () => Live.TotalPlayTime);
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.DrawnClose(
                builder,
                "post-adventure-stats:close",
                Live.CloseButton,
                Live.IsCloseButtonVisible,
                () => Live.Close(),
                Live.IsCloseButtonEnabled);
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

        private static PostAdventureStatsMenu GetStatsMenu(PostAdventureMenu resultMenu)
        {
            return resultMenu != null && StatsMenuField != null
                ? StatsMenuField.GetValue(resultMenu) as PostAdventureStatsMenu
                : null;
        }
    }
}
