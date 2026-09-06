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
    /// The player statistics page, made navigable as a graph. Three stops: the two tabs, everything
    /// the showing tab draws, and the page's buttons.
    ///
    /// The whole content of a tab is ONE sheet stop whose REGIONS are the panels the page draws, in
    /// the order it draws them, so Alt+Up and Alt+Down jump between them and each names itself on the
    /// way in. Measured 2026-09-06 at 1280x800 through `/gui/unity`, Conquest - Overall: the tabs at
    /// y 54 (Conquest - Overall at x 480, Conquest - Battle at x 643); then a band at y 94 of three
    /// panels, `GeneralContainer` at x 30 under the caption "General", `FactionContainer` at x 446
    /// under "Factions, play distribution" and `TopMapsContainer` at x 861 under "Top maps, #games";
    /// then `WieldersAndTroopsContainer` at y 420 under ONE caption, "Top wielder* and troops**",
    /// holding the wielders at x 30 and the troops at x 674, each with its own summary lines
    /// underneath ("Wielder max level: 15", "Played wielders: 13/64", "*Based on ..."). Back
    /// ("Main Menu", x 21) and Options (x 1233) are the main menu's header band.
    ///
    /// General is a LIST, not a table: the page draws three stat tiles side by side and then four
    /// full-width lines, so its region declares no columns and every entry is a line of its own. The
    /// four tables declare the columns the widget screen named them with, because the page draws NO
    /// column captions anywhere - the crossing into a column is the only place those words are said.
    ///
    /// The tabs switch on ENTER, not on focus: `PlayerStatsMenuNavigation.HandleSwitchedTab` shows one
    /// view and hides the other WITH AN ANIMATION (decompiled, lines 77 to 96), so arriving at a tab
    /// is not the same event as arriving at its page.
    ///
    /// Escape presses the drawn Back button, as the widget screen did.
    /// </summary>
    public sealed class PlayerStatsScreen : GraphScreen
    {
        private const string TabsStop = "player-stats-tabs";
        private const string ContentStop = "player-stats-content";
        private const string ButtonsStop = "player-stats-buttons";
        private const string SheetKey = "player-stats:";

        private readonly PlayerStatsAdapter _adapter;

        // A subject of its own per summary line, kept across rebuilds so the reconciler seats the
        // cursor on the same line: the page draws them as labels the mod has nothing else to key on.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public PlayerStatsScreen(PlayerStatsAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PlayerStatsMenuNavigation[] menus = Resources.FindObjectsOfTypeAll<PlayerStatsMenuNavigation>();
            for (int i = 0; i < menus.Length; i++)
            {
                PlayerStatsAdapter adapter = new PlayerStatsAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new PlayerStatsScreen(adapter);
                }
            }

            return null;
        }

        public bool Matches(PlayerStatsMenuNavigation menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.Source, menu);
        }

        public override string Key
        {
            get { return "player-stats"; }
        }

        /// <summary>The page's own drawn title ("Player stats").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        /// <summary>The tab bar, so arrival reads which page is showing before its first line.
        /// </summary>
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
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.Activate();
        }

        /// <summary>Kept for the detector, which calls it when the page's content changes. The graph is
        /// declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(ContentStop);
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            if (_adapter.IsOverallTabSelected)
            {
                BuildOverall(sheet);
            }
            else
            {
                BuildBattle(sheet);
            }

            sheet.Finish();

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // ---- the tabs ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<PlayerStatsAdapter.TabItem> tabs = _adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                PlayerStatsAdapter.TabItem tab = tabs[i];
                NodeVtable vtable = GraphNodes.Tab(
                    () => tab.Label,
                    () => _adapter.SelectedTabIndex == tab.Index);
                vtable.OnActivate = () => _adapter.ActivateTab(tab.Index);
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker(tab.Id), "player-stats:" + tab.Id),
                    vtable));
            }
        }

        // ---- the panels of the showing tab ----

        private void BuildOverall(GraphSheet sheet)
        {
            List(sheet, _adapter.OverallGeneralLabel, _adapter.GetOverallGeneralItems(), "overall-general");
            Table(
                sheet,
                _adapter.FactionsLabel,
                new[] { ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnPlayDistribution },
                new[] { "faction", "rank", "play-distribution" },
                _adapter.GetFactionRows(),
                "factions");
            Table(
                sheet,
                _adapter.TopMapsLabel,
                new[] { ModStrings.UI.ColumnMap, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnDetails, ModStrings.UI.ColumnGames },
                new[] { "map", "rank", "details", "games" },
                _adapter.GetMapRows(),
                "maps");
            Table(
                sheet,
                _adapter.TopWieldersLabel,
                new[] { ModStrings.UI.ColumnWielder, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnTimesRecruited },
                new[] { "wielder", "rank", "faction", "times-recruited" },
                _adapter.GetWielderRows(),
                "wielders");
            Summary(sheet, "wielders", _adapter.WielderSummary, _adapter.WielderSummaryTransform);
            Table(
                sheet,
                _adapter.TopTroopsLabel,
                new[] { ModStrings.UI.ColumnTroop, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnTimesTrained },
                new[] { "troop", "rank", "faction", "times-trained" },
                _adapter.GetTroopRows(),
                "troops");
            Summary(sheet, "troops", _adapter.TroopSummary, _adapter.TroopSummaryTransform);
        }

        private void BuildBattle(GraphSheet sheet)
        {
            List(sheet, _adapter.BattleGeneralLabel, _adapter.GetBattleGeneralItems(), "battle-general");
            Table(
                sheet,
                _adapter.SpellsLabel,
                new[] { ModStrings.UI.ColumnSpell, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnTimesCast },
                new[] { "spell", "rank", "times-cast" },
                _adapter.GetSpellRows(),
                "spells");
            Summary(sheet, "spells", _adapter.SpellSummary, _adapter.SpellSummaryTransform);
            Table(
                sheet,
                _adapter.EnemyTroopsLabel,
                new[] { ModStrings.UI.ColumnTroop, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnKills },
                new[] { "troop", "rank", "faction", "kills" },
                _adapter.GetEnemyTroopRows(),
                "enemy-troops");
        }

        /// <summary>A panel the page draws as plain lines rather than as a table: its caption is the
        /// region and each entry is a full-width line of it.</summary>
        private void List(GraphSheet sheet, string caption, IReadOnlyList<PlayerStatsAdapter.LabeledItem> items, string key)
        {
            sheet.Region(caption);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                PlayerStatsAdapter.LabeledItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Text(() => Plain(item.Label));
                vtable.OnFocusVisual = () => _adapter.ScrollIntoView(item.SourceTransform);
                sheet.Line(vtable, item.SourceTransform);
            }
        }

        /// <summary>One of the page's tables: its drawn caption is the region, the first column is the
        /// row's own name and the rest are its figures.</summary>
        private void Table(
            GraphSheet sheet,
            string caption,
            ModString[] columns,
            string[] columnIds,
            IReadOnlyList<PlayerStatsAdapter.TableRowItem> rows,
            string key)
        {
            string[] captions = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                captions[i] = ModText.Get(columns[i]);
            }

            sheet.Region(caption, captions);
            for (int r = 0; rows != null && r < rows.Count; r++)
            {
                PlayerStatsAdapter.TableRowItem row = rows[r];
                if (row == null)
                {
                    continue;
                }

                List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
                for (int c = 1; c < columnIds.Length; c++)
                {
                    string columnId = columnIds[c];
                    cells.Add(new GraphSheet.SheetCell(c, 0, Cell(captions[c], row, () => row.GetCellValue(columnId))));
                }

                sheet.RowAt(Primary(row), key + "/" + row.Id, cells, row.SourceTransform);
            }
        }

        /// <summary>The lines a table draws under itself - a maximum, a total, the asterisk's
        /// footnote - as rows of that table's own region, one per drawn line.</summary>
        private void Summary(GraphSheet sheet, string key, string summary, RectTransform transform)
        {
            IList<string> lines = SpokenLines.Of(new[] { summary });
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                NodeVtable vtable = GraphNodes.Text(() => line);
                vtable.OnFocusVisual = () => _adapter.ScrollIntoView(transform);
                sheet.Line(vtable, transform);
            }
        }

        private NodeVtable Primary(PlayerStatsAdapter.TableRowItem row)
        {
            PlayerStatsAdapter.TableRowItem it = row;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(() => Plain(it.Label)) },
                OnFocusVisual = () => _adapter.ScrollIntoView(it.SourceTransform),
            };
            return vtable;
        }

        /// <summary>One read-only cell: the drawn figure alone, the column's caption being spoken as
        /// the edge crossed into it, with the caption and the value as the buffer's head.</summary>
        private NodeVtable Cell(string caption, PlayerStatsAdapter.TableRowItem row, Func<string> value)
        {
            PlayerStatsAdapter.TableRowItem it = row;
            Func<string> text = () => Filled(Plain(value()));
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text, watch: false) },
                SearchText = () => Plain(it.Label),
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption, text()),
                OnFocusVisual = () => _adapter.ScrollIntoView(it.SourceTransform),
            };
        }

        /// <summary>One spoken line out of what the page drew. The stat tiles carry the break the
        /// prefab wraps their caption on ("Games\nPlayed: 5"), which is a rendering accident rather
        /// than two things to say, so the lines are joined back into one.</summary>
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

        // ---- the page's buttons ----

        private void BuildButtons(GraphBuilder builder)
        {
            // Back (x 21) then Options (x 1233) of the header band, left to right.
            AddButton(builder, "player-stats:back", _adapter.BackButton);
            AddButton(builder, "player-stats:options", _adapter.OptionsButton);
        }

        private static void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            IMenuButtonAdapter it = button;
            NodeVtable vtable = GraphNodes.Button(it.GetLabel, () => it.Activate(), it.IsEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(it.Button);
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button));
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
