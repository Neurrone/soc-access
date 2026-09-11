using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The kingdom's building overview, made navigable as a graph. Two places to be: the lines the
    /// page draws, and the close.
    ///
    /// The page is LINES, not a table (owner ruling 2026-09-07). It is ONE stop whose REGIONS are the
    /// categories the menu draws, in drawn order, so Alt+Up and Alt+Down jump between settlements and
    /// each names itself on the way in. Measured 2026-09-07: the title "Building Overview"; then per
    /// category the settlement's name ("Crowpoint - Small Settlement") beside its "Tier: 2/2", under
    /// it a band of six resource icons with "+N" figures beside them, and under that one line per
    /// building with its count drawn to the LEFT of its name and its tier at the right ("1 | Quarry |
    /// Tier: 1/2"). The hierarchy order the menu is enumerated in IS the drawn order here. The
    /// catch-all "Claimed" category draws neither a tier nor a camera target.
    ///
    /// So each category is three kinds of line, in the order they are drawn: the category itself, as
    /// the button whose click moves the camera onto the settlement; the income band as ONE line, the
    /// six figures being drawn under the name rather than beside it; and one button per building,
    /// labelled with the count and the name the way the menu lays them out.
    ///
    /// THE INCOME LINE IS REORDERED (owner compromise 2026-09-07): the resources are read in drawn
    /// order, but every figure the game did not draw as "+0" comes first and the "+0" figures follow
    /// in their own drawn order, so a settlement yielding gold and stone reads "Gold +300, Stone +1,
    /// Wood +0, Glimmerweave +0, Ancient Amber +0, Celestial Ore +0". The figures were never read
    /// before this port; the game greys the ones at zero and still draws "+0", which is what the line
    /// says. A category that draws no figure at all draws no income line.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>KingdomEntityOverviewMenu.Hide</c>.
    /// Escape is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers
    /// <c>UI.ExitMenu</c> for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class OwnedEntitiesScreen : LiveScreen<KingdomEntityOverviewAdapter>
    {
        private const string ContentStop = "owned-entities-content";
        private const string CloseStop = "owned-entities-close";

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept
        // across rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        /// <summary>The kingdom HUD holds this menu in a field of its own
        /// (<see cref="HudSources"/>).</summary>
        private readonly ScreenSource<KingdomEntityOverviewMenu> _source =
            ScreenSource<KingdomEntityOverviewMenu>.FromOwner(HudSources.Kingdom, HudSources.EntityOverview);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override KingdomEntityOverviewAdapter Adapt(object menu)
        {
            return new KingdomEntityOverviewAdapter((KingdomEntityOverviewMenu)menu);
        }

        public override string Key
        {
            get { return "owned-entities"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The menu's own drawn title ("Building Overview").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(ContentStop);
            BuildCategories(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the lines ----

        private void BuildCategories(GraphBuilder builder)
        {
            IReadOnlyList<KingdomEntityOverviewAdapter.CategoryItem> categories = Live.GetCategories();
            ControlId first = null;
            for (int c = 0; c < categories.Count; c++)
            {
                KingdomEntityOverviewAdapter.CategoryItem category = categories[c];
                if (category == null || category.Entry == null)
                {
                    continue;
                }

                builder.PushContext(category.Name);
                builder.SetRegion("owned-entities:category/" + c);

                // The catch-all ("Claimed") has no settlement to move the camera to, so its name is
                // the region's name only, said once on entry, and not a line of its own (owner
                // ruling 2026-09-07); its first line is the income line or the first building.
                if (category.HasCameraTarget)
                {
                    ControlId id = ControlId.For(category.Entry, "owned-entities:category/" + c);
                    builder.AddItem(new DrawnNode(id, CategoryLine(category), category.Entry));
                    if (first == null)
                    {
                        first = id;
                    }
                }

                ControlId income = BuildIncome(builder, category, c);
                if (first == null)
                {
                    first = income;
                }

                for (int r = 0; r < category.Rows.Count; r++)
                {
                    KingdomEntityOverviewAdapter.RowItem row = category.Rows[r];
                    if (row == null || row.Entry == null)
                    {
                        continue;
                    }

                    ControlId rowId = ControlId.For(row.Entry, "owned-entities:building/" + c + "/" + r);
                    builder.AddItem(new DrawnNode(rowId, BuildingLine(row), row.Entry));
                    if (first == null)
                    {
                        first = rowId;
                    }
                }

                builder.PopContext();
            }

            builder.SetRegion(null);
            // The first category's own line, which is the top of the page rather than whatever the
            // game last selected.
            builder.LandStopOn(first);
        }

        /// <summary>The category's own line: the name the menu draws, as the button whose click moves
        /// the camera onto the settlement, with the tier drawn beside it.</summary>
        private static NodeVtable CategoryLine(KingdomEntityOverviewAdapter.CategoryItem category)
        {
            KingdomEntityOverviewAdapter.CategoryItem it = category;
            NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.MoveCamera());
            GraphNodes.AddValue(vtable, it.Tier);
            return vtable;
        }

        /// <summary>The band of income figures under the category's name, as one read-only line: the
        /// mod's own label over one part per figure the game drew, the resources it yields something
        /// of before the ones it yields nothing of.</summary>
        /// <returns>The income line's id, or null when the category draws no figure.</returns>
        private static ControlId BuildIncome(
            GraphBuilder builder,
            KingdomEntityOverviewAdapter.CategoryItem category,
            int index)
        {
            List<string> earning = new List<string>();
            List<string> idle = new List<string>();
            for (int i = 0; i < category.Incomes.Count; i++)
            {
                KingdomEntityOverviewAdapter.IncomeItem income = category.Incomes[i];
                if (income == null || !income.IsDrawn || string.IsNullOrWhiteSpace(income.Text))
                {
                    continue;
                }

                string part = ModText.Get(ModStrings.UI.LabelValue, income.ResourceName, income.Text);
                (IsZeroFigure(income.Text) ? idle : earning).Add(part);
            }

            earning.AddRange(idle);
            if (earning.Count == 0)
            {
                return null;
            }

            NodeVtable vtable = GraphNodes.Text(() => ModText.Get(ModStrings.Screens.Income));
            for (int i = 0; i < earning.Count; i++)
            {
                string part = earning[i];
                vtable.Announcements.Add(GraphNodes.ValuePart(() => part, watch: false));
            }

            // A subject of its own: the reconciler seats the cursor by subject before it looks at
            // the structural key, so an income line sharing the category's component would hand the
            // cursor back to the category line on arrival.
            ControlId id = ControlId.Structural("owned-entities:income/" + index);
            builder.AddItem(new SyntheticNode(id, vtable));
            return id;
        }

        /// <summary>A building's own line: the count and the name the way the menu lays them out (the
        /// count is drawn to the LEFT of the name), as the button whose click cycles the camera
        /// through the buildings of that kind, with the tier drawn at the right of it.</summary>
        private static NodeVtable BuildingLine(KingdomEntityOverviewAdapter.RowItem row)
        {
            KingdomEntityOverviewAdapter.RowItem it = row;
            NodeVtable vtable = GraphNodes.Button(() => BuildingLabel(it), () => it.Activate());
            vtable.OnFocusVisual = () => it.Focus();
            GraphNodes.AddValue(vtable, it.Tier);
            return vtable;
        }

        private static string BuildingLabel(KingdomEntityOverviewAdapter.RowItem row)
        {
            return string.IsNullOrWhiteSpace(row.Amount)
                ? row.Name
                : ModText.Get(ModStrings.Common.ResourceAmount, row.Amount, row.Name);
        }

        /// <summary>Whether the game drew this figure as nothing gained.
        /// <c>KingdomEntityOverviewCategoryEntry.SetIncomeText</c> writes "+{amount}" and greys the
        /// text at zero, so a figure whose digits are all zeros is one of the greyed ones.</summary>
        private static bool IsZeroFigure(string text)
        {
            bool sawDigit = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    continue;
                }

                sawDigit = true;
                if (text[i] != '0')
                {
                    return false;
                }
            }

            return sawDigit;
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.ModClose(builder, "owned-entities:close", _closeKey, () => Live.Close());
        }
    }
}
