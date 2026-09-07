using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The kingdom's troop overview, made navigable as a graph. Two places to be: the table of every
    /// town's recruitment, and the close.
    ///
    /// The whole page is ONE sheet stop whose REGIONS are the towns the menu draws, in drawn order,
    /// so Alt+Up and Alt+Down jump between towns and each names itself on the way in. Measured
    /// 2026-09-07: the title "Troop Overview"; then per town a header line (the settlement's name
    /// "Hazelpoint - Small Settlement" beside its "Tier: 2/2"), then one line per recruitable troop
    /// with the figure the game draws as "10 (+2)". The hierarchy order the menu is enumerated in IS
    /// the drawn order here.
    ///
    /// Two columns: the row's NAME and the VALUE drawn at the right of it - the town's tier on the
    /// header line, the troop's figure on a troop line. The game draws a caption over neither, so a
    /// crossing into the value column is label-free and the value says itself.
    /// <c>KingdomTroopOverviewIncomeEntry.SetTroop</c> composes the number available and the
    /// per-round income into one drawn label and keeps neither number, so there is nothing to split
    /// into an "available" and a "per turn" column: the composed text is one cell.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>KingdomTroopOverviewMenu.Hide</c>.
    /// Escape is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers
    /// <c>UI.ExitMenu</c> for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class TroopOverviewScreen : GraphScreen
    {
        private const string ContentStop = "troop-overview-content";
        private const string CloseStop = "troop-overview-close";
        private const string SheetKey = "troop-overview:";

        // The logical columns of a row: the name is the primary, and the figure drawn at the right of
        // it is the one metadata column.
        private const int ValueColumn = 1;
        private const int ColumnCount = 2;

        private readonly KingdomTroopOverviewAdapter _adapter;

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept
        // across rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        public TroopOverviewScreen(KingdomTroopOverviewAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            KingdomTroopOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomTroopOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomTroopOverviewAdapter adapter = new KingdomTroopOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new TroopOverviewScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "troop-overview"; }
        }

        /// <summary>The menu's own drawn title ("Troop Overview"). Nothing drawn means no screen
        /// name.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(ContentStop);
            BuildTowns(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the table ----

        private void BuildTowns(GraphBuilder builder)
        {
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            IReadOnlyList<KingdomTroopOverviewAdapter.TownItem> towns = _adapter.GetTowns();
            for (int t = 0; t < towns.Count; t++)
            {
                KingdomTroopOverviewAdapter.TownItem town = towns[t];
                if (town == null)
                {
                    continue;
                }

                sheet.Region(town.Name, new string[ColumnCount]);
                sheet.RowAt(TownPrimary(town), town.Entry, Cells(town.Tier, () => town.Name, null), town.Entry);
                for (int r = 0; r < town.Rows.Count; r++)
                {
                    KingdomTroopOverviewAdapter.RowItem row = town.Rows[r];
                    sheet.RowAt(TroopPrimary(row), row.Entry, Cells(row.Amount, () => row.Name, row.Focus), row.Entry);
                }
            }

            sheet.Finish();
            // The first town's header row, which is the top of the page rather than whatever the game
            // last selected.
            builder.LandStopOn(sheet.FirstRow);
        }

        /// <summary>The town's own line: the name the menu draws, as the button whose click moves the
        /// camera onto the settlement.</summary>
        private static NodeVtable TownPrimary(KingdomTroopOverviewAdapter.TownItem town)
        {
            KingdomTroopOverviewAdapter.TownItem it = town;
            return GraphNodes.Button(() => it.Name, () => it.MoveCamera());
        }

        /// <summary>A troop's own line: the name the menu draws, as the button whose click cycles the
        /// camera through the buildings producing it.</summary>
        private static NodeVtable TroopPrimary(KingdomTroopOverviewAdapter.RowItem row)
        {
            KingdomTroopOverviewAdapter.RowItem it = row;
            NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.Activate());
            vtable.OnFocusVisual = () => it.Focus();
            return vtable;
        }

        /// <summary>The row's one read-only cell: the figure the game drew, with the row's name as
        /// what a search matches it by. A row the game draws no figure on declares no cell - the
        /// sheet's rows are ragged by design.</summary>
        private static List<GraphSheet.SheetCell> Cells(string value, Func<string> rowName, Func<bool> focus)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return cells;
            }

            string text = value;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(() => text, watch: false) },
                SearchText = rowName,
                BufferHead = () => text,
            };
            if (focus != null)
            {
                vtable.OnFocusVisual = () => focus();
            }

            cells.Add(new GraphSheet.SheetCell(ValueColumn, 0, vtable));
            return cells;
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "troop-overview:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => _adapter.Close())));
        }
    }
}
