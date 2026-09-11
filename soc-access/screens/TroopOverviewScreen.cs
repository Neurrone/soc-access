using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The kingdom's troop overview, made navigable as a graph. Two places to be: the lines the page
    /// draws, and the close.
    ///
    /// The page is LINES, not a table (owner ruling 2026-09-07). It is ONE stop whose REGIONS are the
    /// towns the menu draws, in drawn order, so Alt+Up and Alt+Down jump between towns and each names
    /// itself on the way in. Measured 2026-09-07: the title "Troop Overview"; then per town the
    /// settlement's name ("Hazelpoint - Small Settlement") beside its "Tier: 2/2", then one line per
    /// recruitable troop with the figure the game draws as "10 (+2)". The hierarchy order the menu is
    /// enumerated in IS the drawn order here.
    ///
    /// So each town is two kinds of line, in the order they are drawn: the town itself, as the button
    /// whose click moves the camera onto the settlement, and one button per troop, whose click cycles
    /// the camera through the buildings producing it. The figure the game draws at the right of a line
    /// - the town's tier, the troop's count - is that line's value.
    /// <c>KingdomTroopOverviewIncomeEntry.SetTroop</c> composes the number available and the per-round
    /// income into one drawn label and keeps neither number, so there is nothing to split into an
    /// "available" and a "per turn": the composed text is the whole value.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>KingdomTroopOverviewMenu.Hide</c>.
    /// Escape is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers
    /// <c>UI.ExitMenu</c> for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class TroopOverviewScreen : LiveScreen<KingdomTroopOverviewAdapter>
    {
        private const string ContentStop = "troop-overview-content";
        private const string CloseStop = "troop-overview-close";

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept
        // across rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        /// <summary>The kingdom HUD holds this menu in a field of its own
        /// (<see cref="HudSources"/>).</summary>
        private readonly ScreenSource<KingdomTroopOverviewMenu> _source =
            ScreenSource<KingdomTroopOverviewMenu>.FromOwner(HudSources.Kingdom, HudSources.TroopOverview);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override KingdomTroopOverviewAdapter Adapt(object menu)
        {
            return new KingdomTroopOverviewAdapter((KingdomTroopOverviewMenu)menu);
        }

        public override string Key
        {
            get { return "troop-overview"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The menu's own drawn title ("Troop Overview"). Nothing drawn means no screen
        /// name.</summary>
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
            BuildTowns(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the lines ----

        private void BuildTowns(GraphBuilder builder)
        {
            IReadOnlyList<KingdomTroopOverviewAdapter.TownItem> towns = Live.GetTowns();
            ControlId first = null;
            for (int t = 0; t < towns.Count; t++)
            {
                KingdomTroopOverviewAdapter.TownItem town = towns[t];
                if (town == null || town.Entry == null)
                {
                    continue;
                }

                builder.PushContext(town.Name);
                builder.SetRegion("troop-overview:town/" + t);

                ControlId id = ControlId.For(town.Entry, "troop-overview:town/" + t);
                builder.AddItem(new DrawnNode(id, TownLine(town), town.Entry));
                if (first == null)
                {
                    first = id;
                }

                for (int r = 0; r < town.Rows.Count; r++)
                {
                    KingdomTroopOverviewAdapter.RowItem row = town.Rows[r];
                    if (row == null || row.Entry == null)
                    {
                        continue;
                    }

                    builder.AddItem(new DrawnNode(
                        ControlId.For(row.Entry, "troop-overview:troop/" + t + "/" + r),
                        TroopLine(row),
                        row.Entry));
                }

                builder.PopContext();
            }

            builder.SetRegion(null);
            // The first town's own line, which is the top of the page rather than whatever the game
            // last selected.
            builder.LandStopOn(first);
        }

        /// <summary>The town's own line: the name the menu draws, as the button whose click moves the
        /// camera onto the settlement, with the tier drawn beside it.</summary>
        private static NodeVtable TownLine(KingdomTroopOverviewAdapter.TownItem town)
        {
            KingdomTroopOverviewAdapter.TownItem it = town;
            NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.MoveCamera());
            AddValue(vtable, it.Tier);
            return vtable;
        }

        /// <summary>A troop's own line: the name the menu draws, as the button whose click cycles the
        /// camera through the buildings producing it, with the figure drawn at the right of it.
        /// </summary>
        private static NodeVtable TroopLine(KingdomTroopOverviewAdapter.RowItem row)
        {
            KingdomTroopOverviewAdapter.RowItem it = row;
            NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.Activate());
            vtable.OnFocusVisual = () => it.Focus();
            AddValue(vtable, it.Amount);
            return vtable;
        }

        /// <summary>The figure the game drew at the right of the line, as that line's value. A line
        /// the game draws no figure on says nothing beyond its name.</summary>
        private static void AddValue(NodeVtable vtable, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string text = value;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => text, watch: false));
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.ModClose(builder, "troop-overview:close", _closeKey, () => Live.Close());
        }
    }
}
