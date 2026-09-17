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
    public sealed class PlayerStatsScreen : LiveScreen<PlayerStatsAdapter>
    {
        private const string TabsStop = "player-stats-tabs";
        private const string ContentStop = "player-stats-content";
        private const string ButtonsStop = "player-stats-buttons";
        private const string SheetKey = "player-stats:";

        /// <summary>The page's navigation object, an unbound scene object found by one gated walk of
        /// its own scene's roots (<see cref="MenuSceneSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return MenuSceneSources.PlayerStats.Current;
        }

        protected override PlayerStatsAdapter Adapt(object menu)
        {
            return new PlayerStatsAdapter((PlayerStatsMenuNavigation)menu);
        }

        public override string Key
        {
            get { return "player-stats"; }
        }

        /// <summary>Layer 30: over the panels it is opened from.</summary>
        public override int Layer
        {
            get { return 30; }
        }

        /// <summary>The page's own drawn title ("Player stats").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        /// <summary>The tab bar, so arrival reads which page is showing before its first line.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override IMenuButtonAdapter BackButton
        {
            get { return Live != null ? Live.BackButton : null; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(ContentStop);
            BuildContent(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // Mod-owned: what the last build of the showing tab declared, handed back while the keys it
        // was recorded under still answer the same. One snapshot for the whole page, because one
        // sheet builds all of it: five regions of tables, lists and footnotes. Keyed on what the game
        // owns, so a new page answers with new identities and the block is minted again; there is no
        // reset hook and none is needed.
        private readonly SheetSnapshot _content = new SheetSnapshot();

        /// <summary>The showing tab's panels, as one sheet of regions.</summary>
        private void BuildContent(GraphBuilder builder)
        {
            bool overall = Live.IsOverallTabSelected;
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            object[] keys = overall ? OverallKeys() : BattleKeys();
            if (!_content.TryReplay(sheet, keys))
            {
                _content.Record(sheet, keys);
                if (overall)
                {
                    BuildOverall(sheet);
                }
                else
                {
                    BuildBattle(sheet);
                }

                sheet.Finish();
                _content.Keep();
            }
            else
            {
                sheet.Finish();
            }
        }

        /// <summary>
        /// What the overall tab's panels were built from: which tab is showing, each table's rows
        /// (the adapter hands back the same list while the game is drawing the same entries), the
        /// drawn caption over each panel, and the two kinds of text this page bakes into a node
        /// rather than reading when the node is read - the general panel's lines and the footnotes
        /// under the tables.
        ///
        /// Those last two are what notices a page whose figures arrive a frame after its entries go
        /// live: the keys are one set, so a general line that filled in rebuilds every region with
        /// it, table rows included. A row's own name and figures are read when the row is read; what
        /// a kept block holds of them is the name baked into a vertical crossing, and on a page of
        /// finished statistics that name does not move.
        /// </summary>
        private object[] OverallKeys()
        {
            return new object[]
            {
                true,
                Live.GetFactionRows(),
                Live.GetMapRows(),
                Live.GetWielderRows(),
                Live.GetTroopRows(),
                SheetSnapshot.Words(new[]
                {
                    Live.OverallGeneralLabel,
                    Live.FactionsLabel,
                    Live.TopMapsLabel,
                    Live.TopWieldersLabel,
                    Live.TopTroopsLabel
                }),
                Lines(Live.GetOverallGeneralItems()),
                SheetSnapshot.Words(new[] { Live.WielderSummary, Live.TroopSummary })
            };
        }

        /// <summary>The same for the battle tab (<see cref="OverallKeys"/>).</summary>
        private object[] BattleKeys()
        {
            return new object[]
            {
                false,
                Live.GetSpellRows(),
                Live.GetEnemyTroopRows(),
                SheetSnapshot.Words(new[]
                {
                    Live.BattleGeneralLabel,
                    Live.SpellsLabel,
                    Live.EnemyTroopsLabel
                }),
                Lines(Live.GetBattleGeneralItems()),
                SheetSnapshot.Words(new[] { Live.SpellSummary })
            };
        }

        private static string Lines(IReadOnlyList<PlayerStatsAdapter.LabeledItem> items)
        {
            if (items == null)
            {
                return null;
            }

            string[] labels = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                labels[i] = items[i] != null ? items[i].Label : null;
            }

            return SheetSnapshot.Words(labels);
        }

        // ---- the tabs ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<PlayerStatsAdapter.TabItem> tabs = Live.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                PlayerStatsAdapter.TabItem tab = tabs[i];
                NodeVtable vtable = GraphNodes.Tab(
                    () => TabLabel(tab),
                    () => Live.SelectedTabIndex == tab.Index);
                vtable.OnActivate = () => Live.ActivateTab(tab.Index);
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker(tab.Id), "player-stats:" + tab.Id),
                    vtable));
            }
        }

        /// <summary>What a tab is called: what the page drew on it, or - for a page that names it
        /// nowhere the mod can read - the mod's own name for that half of the statistics.</summary>
        private static string TabLabel(PlayerStatsAdapter.TabItem tab)
        {
            if (!string.IsNullOrWhiteSpace(tab.Label))
            {
                return tab.Label;
            }

            return ModText.Get(tab.IsOverall
                ? ModStrings.Screens.PlayerStatsOverall
                : ModStrings.Screens.PlayerStatsBattle);
        }

        // ---- the panels of the showing tab ----

        private void BuildOverall(GraphSheet sheet)
        {
            List(sheet, Live.OverallGeneralLabel, Live.GetOverallGeneralItems());
            Table(
                sheet,
                Live.FactionsLabel,
                new[] { ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnPlayDistribution },
                Live.GetFactionRows(),
                "factions",
                "faction");
            Table(
                sheet,
                Live.TopMapsLabel,
                new[] { ModStrings.UI.ColumnMap, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnDetails, ModStrings.UI.ColumnGames },
                Live.GetMapRows(),
                "maps",
                "map");
            Table(
                sheet,
                Live.TopWieldersLabel,
                new[] { ModStrings.UI.ColumnWielder, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnTimesRecruited },
                Live.GetWielderRows(),
                "wielders",
                "wielder");
            Summary(sheet, "wielders", Live.WielderSummary, Live.WielderSummaryTransform);
            Table(
                sheet,
                Live.TopTroopsLabel,
                new[] { ModStrings.UI.ColumnTroop, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnTimesTrained },
                Live.GetTroopRows(),
                "troops",
                "troop");
            Summary(sheet, "troops", Live.TroopSummary, Live.TroopSummaryTransform);
        }

        private void BuildBattle(GraphSheet sheet)
        {
            List(sheet, Live.BattleGeneralLabel, Live.GetBattleGeneralItems());
            Table(
                sheet,
                Live.SpellsLabel,
                new[] { ModStrings.UI.ColumnSpell, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnTimesCast },
                Live.GetSpellRows(),
                "spells",
                "spell");
            Summary(sheet, "spells", Live.SpellSummary, Live.SpellSummaryTransform);
            Table(
                sheet,
                Live.EnemyTroopsLabel,
                new[] { ModStrings.UI.ColumnTroop, ModStrings.UI.ColumnRank, ModStrings.UI.ColumnFaction, ModStrings.UI.ColumnKills },
                Live.GetEnemyTroopRows(),
                "enemy-troops",
                "enemy-troop");
        }

        /// <summary>A panel the page draws as plain lines rather than as a table: its caption is the
        /// region and each entry is a full-width line of it.</summary>
        private void List(GraphSheet sheet, string caption, IReadOnlyList<PlayerStatsAdapter.LabeledItem> items)
        {
            sheet.Region(caption);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                PlayerStatsAdapter.LabeledItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Text(() => CellText.Plain(item.Label));
                vtable.OnFocusVisual = () => Live.ScrollIntoView(item.SourceTransform);
                sheet.Line(vtable, item.SourceTransform);
            }
        }

        /// <summary>
        /// One of the page's tables: its drawn caption is the region, the first column is the row's
        /// own name, the second is where the row sits in the table, and the rest are the figures the
        /// game draws across it, in the order it draws them.
        ///
        /// The place is the SCREEN's wording - the page draws no rank anywhere, the mod says it - so
        /// the row reports its index and this puts the number into <c>UI.RankNumber</c>.
        /// </summary>
        private void Table(
            GraphSheet sheet,
            string caption,
            ModString[] columns,
            IReadOnlyList<PlayerStatsAdapter.TableRowItem> rows,
            string key,
            string rowKey)
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

                PlayerStatsAdapter.TableRowItem it = row;
                List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>
                {
                    new GraphSheet.SheetCell(
                        RankColumn,
                        0,
                        Cell(captions[RankColumn], it, () => ModText.Get(ModStrings.UI.RankNumber, it.Index + 1))),
                };
                for (int c = RankColumn + 1; c < columns.Length; c++)
                {
                    int value = c - RankColumn - 1;
                    cells.Add(new GraphSheet.SheetCell(c, 0, Cell(captions[c], it, () => it.Value(value))));
                }

                sheet.RowAt(Primary(row), key + "/" + rowKey + "-" + row.Index, cells, row.SourceTransform);
            }
        }

        /// <summary>Every table on the page draws the row's own name first and its place second.
        /// </summary>
        private const int RankColumn = 1;

        /// <summary>The lines a table draws under itself - a maximum, a total, the asterisk's
        /// footnote - as rows of that table's own region, one per drawn line.</summary>
        private void Summary(GraphSheet sheet, string key, string summary, RectTransform transform)
        {
            IList<string> lines = SpokenLines.Of(new[] { summary });
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                NodeVtable vtable = GraphNodes.Text(() => line);
                vtable.OnFocusVisual = () => Live.ScrollIntoView(transform);
                sheet.Line(vtable, transform);
            }
        }

        private NodeVtable Primary(PlayerStatsAdapter.TableRowItem row)
        {
            PlayerStatsAdapter.TableRowItem it = row;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(() => CellText.Plain(it.Label)) },
                OnFocusVisual = () => Live.ScrollIntoView(it.SourceTransform),
            };
            return vtable;
        }

        /// <summary>One read-only cell: the drawn figure alone, the column's caption being spoken as
        /// the edge crossed into it, with the caption and the value as the buffer's head.</summary>
        private NodeVtable Cell(string caption, PlayerStatsAdapter.TableRowItem row, Func<string> value)
        {
            PlayerStatsAdapter.TableRowItem it = row;
            Func<string> text = () => CellText.Filled(CellText.Plain(value()));
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text, watch: false) },
                SearchText = () => CellText.Plain(it.Label),
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption, text()),
                OnFocusVisual = () => Live.ScrollIntoView(it.SourceTransform),
            };
        }

        // ---- the page's buttons ----

        private void BuildButtons(GraphBuilder builder)
        {
            // Back (x 21) then Options (x 1233) of the header band, left to right.
            GraphNodes.MenuButton(builder, "player-stats:back", Live.BackButton);
            GraphNodes.MenuButton(builder, "player-stats:options", Live.OptionsButton);
        }
    }
}
