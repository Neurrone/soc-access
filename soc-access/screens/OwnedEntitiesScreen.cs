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
    /// The kingdom's building overview, made navigable as a graph. Two places to be: the table of
    /// everything the team owns, and the close.
    ///
    /// The whole page is ONE sheet stop whose REGIONS are the categories the menu draws, in drawn
    /// order, so Alt+Up and Alt+Down jump between settlements and each names itself on the way in.
    /// Measured 2026-09-07: the title "Building Overview"; then per category a header line (the
    /// settlement's name "Crowpoint - Small Settlement" beside its "Tier: 2/2") over a band of six
    /// resource icons with "+N" figures beside them, then one line per building with its count, its
    /// name and its tier ("1 | Quarry | Tier: 1/2"). The hierarchy order the menu is enumerated in IS
    /// the drawn order here. The catch-all "Claimed" category draws neither a tier nor a camera
    /// target.
    ///
    /// The columns are the row's NAME, its COUNT, its TIER and then the six INCOMES, which are the
    /// only ones the game draws a caption for - the resource icons - so they are the only ones the
    /// sheet labels a crossing with. Rows are RAGGED and that is what the sheet is for: the category
    /// header carries the tier and the six incomes and no count, a building row carries a count and a
    /// tier and no incomes, and a cell that is not drawn is simply not declared. Every column is a
    /// column IDENTITY, so Up and Down from a tier reach the neighbouring rows' tiers and fall to the
    /// row's name where there is none.
    ///
    /// The six income figures are drawn and were never read before this port; the game greys the ones
    /// at zero and still draws "+0", which is what those cells say.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>KingdomEntityOverviewMenu.Hide</c>.
    /// Escape is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers
    /// <c>UI.ExitMenu</c> for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class OwnedEntitiesScreen : GraphScreen
    {
        private const string ContentStop = "owned-entities-content";
        private const string CloseStop = "owned-entities-close";
        private const string SheetKey = "owned-entities:";

        // The logical columns of a row. The primary (0) is the name, because that is what names the
        // row on a vertical crossing; the count is drawn to its LEFT, but a sheet always emits the
        // primary first, so it reads to the right of the name.
        private const int CountColumn = 1;
        private const int TierColumn = 2;
        private const int FirstIncomeColumn = 3;

        private readonly KingdomEntityOverviewAdapter _adapter;

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept
        // across rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        public OwnedEntitiesScreen(KingdomEntityOverviewAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            KingdomEntityOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomEntityOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomEntityOverviewAdapter adapter = new KingdomEntityOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new OwnedEntitiesScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "owned-entities"; }
        }

        /// <summary>The menu's own drawn title ("Building Overview").</summary>
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
            BuildCategories(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the table ----

        private void BuildCategories(GraphBuilder builder)
        {
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            IReadOnlyList<KingdomEntityOverviewAdapter.CategoryItem> categories = _adapter.GetCategories();
            string[] columns = Columns(categories);
            for (int c = 0; c < categories.Count; c++)
            {
                KingdomEntityOverviewAdapter.CategoryItem category = categories[c];
                if (category == null)
                {
                    continue;
                }

                sheet.Region(category.Name, columns);
                sheet.RowAt(CategoryPrimary(category), category.Entry, CategoryCells(category), category.Entry);
                for (int r = 0; r < category.Rows.Count; r++)
                {
                    KingdomEntityOverviewAdapter.RowItem row = category.Rows[r];
                    sheet.RowAt(BuildingPrimary(row), row.Entry, BuildingCells(row), row.Entry);
                }
            }

            sheet.Finish();
            // The first category's header row, which is the top of the page rather than whatever the
            // game last selected.
            builder.LandStopOn(sheet.FirstRow);
        }

        /// <summary>The captions the game draws over the columns: none over the name, the count or the
        /// tier, and a resource icon over each income figure.</summary>
        private static string[] Columns(IReadOnlyList<KingdomEntityOverviewAdapter.CategoryItem> categories)
        {
            IReadOnlyList<KingdomEntityOverviewAdapter.IncomeItem> incomes =
                categories.Count > 0 ? categories[0].Incomes : new KingdomEntityOverviewAdapter.IncomeItem[0];
            string[] columns = new string[FirstIncomeColumn + incomes.Count];
            for (int i = 0; i < incomes.Count; i++)
            {
                columns[FirstIncomeColumn + i] = incomes[i].ResourceName;
            }

            return columns;
        }

        /// <summary>The category's own line: the name the menu draws, as the button whose click moves
        /// the camera onto the settlement. The catch-all has no settlement to move to, so there it is
        /// a line rather than a button.</summary>
        private static NodeVtable CategoryPrimary(KingdomEntityOverviewAdapter.CategoryItem category)
        {
            KingdomEntityOverviewAdapter.CategoryItem it = category;
            return it.HasCameraTarget
                ? GraphNodes.Button(() => it.Name, () => it.MoveCamera())
                : GraphNodes.Text(() => it.Name);
        }

        private List<GraphSheet.SheetCell> CategoryCells(KingdomEntityOverviewAdapter.CategoryItem category)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            AddCell(cells, TierColumn, null, category.Tier, () => category.Name, null);
            for (int i = 0; i < category.Incomes.Count; i++)
            {
                KingdomEntityOverviewAdapter.IncomeItem income = category.Incomes[i];
                if (!income.IsDrawn)
                {
                    continue;
                }

                AddCell(cells, FirstIncomeColumn + i, income.ResourceName, income.Text, () => category.Name, null);
            }

            return cells;
        }

        /// <summary>A building's own line: the name the menu draws, as the button whose click cycles
        /// the camera through the buildings of that kind.</summary>
        private static NodeVtable BuildingPrimary(KingdomEntityOverviewAdapter.RowItem row)
        {
            KingdomEntityOverviewAdapter.RowItem it = row;
            NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.Activate());
            vtable.OnFocusVisual = () => it.Focus();
            return vtable;
        }

        private static List<GraphSheet.SheetCell> BuildingCells(KingdomEntityOverviewAdapter.RowItem row)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            AddCell(cells, CountColumn, null, row.Amount, () => row.Name, row.Focus);
            AddCell(cells, TierColumn, null, row.Tier, () => row.Name, row.Focus);
            return cells;
        }

        /// <summary>One read-only cell: the drawn figure alone, the column's caption being spoken as
        /// the edge crossed into it, with the caption and the value as the buffer's head. A cell the
        /// game draws nothing in is not declared at all - the sheet's rows are ragged by design.
        /// </summary>
        private static void AddCell(
            List<GraphSheet.SheetCell> cells,
            int column,
            string caption,
            string value,
            Func<string> rowName,
            Func<bool> focus)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string text = value;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(() => text, watch: false) },
                SearchText = rowName,
                BufferHead = string.IsNullOrWhiteSpace(caption)
                    ? (Func<string>)(() => text)
                    : () => ModText.Get(ModStrings.Common.ListSeparator, caption, text),
            };
            if (focus != null)
            {
                vtable.OnFocusVisual = () => focus();
            }

            cells.Add(new GraphSheet.SheetCell(column, 0, vtable));
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "owned-entities:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => _adapter.Close())));
        }
    }
}
