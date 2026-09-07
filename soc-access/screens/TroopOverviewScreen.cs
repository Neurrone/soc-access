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
    public sealed class TroopOverviewScreen : GraphScreen
    {
        private const string ContentStop = "troop-overview-content";
        private const string CloseStop = "troop-overview-close";

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

        // ---- the lines ----

        private void BuildTowns(GraphBuilder builder)
        {
            IReadOnlyList<KingdomTroopOverviewAdapter.TownItem> towns = _adapter.GetTowns();
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
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "troop-overview:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => _adapter.Close())));
        }
    }
}
