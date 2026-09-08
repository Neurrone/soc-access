using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The kingdom's research overview, made navigable as a graph. Five places to be, in the order
    /// the menu draws them: the tutorial button at the top left, the faction strip on a
    /// mixed-factions map, the row of research-building tabs, the research itself, and the close.
    ///
    /// Measured 2026-09-07: header, the tutorial button top left, two building tabs in a row, then
    /// one column per category with its stack buttons below the caption. Each drawn category caption
    /// is the REGION its stacks belong to, so Alt+Up and Alt+Down jump between categories and the
    /// caption is spoken on the way in; the whole grid is ONE stop, because it is one page of things
    /// to buy rather than several places to be.
    ///
    /// THE TABS SWITCH ON ENTER, not on focus (owner ruling 2026-09-07): both bars respawn the page
    /// under them - <c>ResearchMenu.SetContentForFaction</c> rebuilds the tab row and
    /// <c>HandleBuildingTabSwitched</c> re-pools every stack button - so arriving at a tab must not
    /// take the page the player is reading away. Focus only moves the game's own selection.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control at all and is dismissed by
    /// clicking the blocker behind it, so the node runs the game's own <c>ResearchMenu.Hide</c>.
    /// Escape is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers
    /// <c>UI.ExitMenu</c> for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class ResearchScreen : LiveScreen<ResearchMenuAdapter>
    {
        private const string TutorialStop = "research-tutorial";
        private const string FactionsStop = "research-factions";
        private const string BuildingsStop = "research-buildings";
        private const string ResearchStop = "research-items";
        private const string CloseStop = "research-close";

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept
        // across rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<ResearchScreen>(FindActive());
        }

        public static ResearchMenuAdapter FindActive()
        {
            ResearchMenu[] menus = Resources.FindObjectsOfTypeAll<ResearchMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ResearchMenuAdapter adapter = new ResearchMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return (adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "research"; }
        }

        /// <summary>Layer 24: over the settlement page that opens it.</summary>
        public override int Layer
        {
            get { return 24; }
        }

        /// <summary>The header the menu draws, which on a mixed-factions map also says whose research
        /// is showing.</summary>
        public override string ScreenName
        {
            get
            {
                string header = Live != null ? Live.HeaderText : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TutorialStop);
            BuildTutorial(builder);

            if (Live.HasFactionSelector())
            {
                builder.BeginStop(FactionsStop);
                BuildFactions(builder);
            }

            builder.BeginStop(BuildingsStop);
            BuildBuildings(builder);

            builder.BeginStop(ResearchStop);
            BuildResearch(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the tutorial button ----

        private void BuildTutorial(GraphBuilder builder)
        {
            Component button = Live.TutorialButton;
            if (button == null || !Live.IsTutorialButtonVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                Live.GetTutorialButtonLabel,
                () => Live.ActivateTutorial());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(ControlId.For(button, "research:tutorial"), vtable, button));
        }

        // ---- the faction strip ----

        private void BuildFactions(GraphBuilder builder)
        {
            IReadOnlyList<ResearchMenuAdapter.FactionItem> factions = Live.GetFactions();
            for (int i = 0; i < factions.Count; i++)
            {
                ResearchMenuAdapter.FactionItem faction = factions[i];
                Component button = faction.Button;
                if (button == null)
                {
                    continue;
                }

                ResearchMenuAdapter.FactionItem it = faction;
                NodeVtable vtable = GraphNodes.Tab(() => it.Label, () => it.IsSelected);
                vtable.OnActivate = () => { if (it.Activate != null) it.Activate(); };
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                builder.AddItem(new DrawnNode(
                    ControlId.For(button, "research:faction/" + faction.FactionIndex),
                    vtable,
                    button));
            }
        }

        // ---- the building tabs ----

        private void BuildBuildings(GraphBuilder builder)
        {
            IReadOnlyList<ResearchMenuAdapter.BuildingItem> buildings = Live.GetBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                ResearchMenuAdapter.BuildingItem building = buildings[i];
                Component button = building.Button;
                if (button == null)
                {
                    continue;
                }

                ResearchMenuAdapter.BuildingItem it = building;
                NodeVtable vtable = GraphNodes.Tab(() => it.Label, () => it.IsSelected);
                // Both drawn on the tab: what the building researches, and, when the team owns none
                // of it, that nothing under the tab can be bought.
                GraphNodes.ParagraphParts(vtable, () => it.DescriptionLines);
                vtable.Announcements.Add(GraphNodes.ValuePart(
                    () => it.MissingBuilding ? ModText.Get(ModStrings.Screens.MissingBuilding) : null));
                vtable.OnActivate = () => { if (it.Activate != null) it.Activate(); };
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                builder.AddItem(new DrawnNode(
                    ControlId.For(button, "research:building/" + i),
                    vtable,
                    button));
            }
        }

        // ---- the research grid ----

        private void BuildResearch(GraphBuilder builder)
        {
            IReadOnlyList<ResearchMenuAdapter.CategoryItem> categories = Live.GetCategories();
            ControlId first = null;
            for (int c = 0; c < categories.Count; c++)
            {
                ResearchMenuAdapter.CategoryItem category = categories[c];
                if (category == null || category.Items.Count == 0)
                {
                    continue;
                }

                builder.PushContext(category.Label);
                builder.SetRegion("research:category/" + c);
                for (int i = 0; i < category.Items.Count; i++)
                {
                    ResearchMenuAdapter.ResearchItem item = category.Items[i];
                    Component button = item != null ? item.Button : null;
                    if (button == null)
                    {
                        continue;
                    }

                    ResearchMenuAdapter.ResearchItem it = item;
                    NodeVtable vtable = GraphNodes.Button(
                        () => ResearchLabel(it),
                        () => { if (it.Activate != null) it.Activate(); },
                        it.IsEnabled,
                        it.Tooltip);
                    vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                    ControlId id = ControlId.For(button, "research:item/" + c + "/" + i);
                    builder.AddItem(new DrawnNode(id, vtable, button));
                    if (first == null)
                    {
                        first = id;
                    }
                }

                builder.PopContext();
            }

            builder.SetRegion(null);
            // The first stack rather than whatever the game last selected: nothing here is "current".
            builder.LandStopOn(first);
        }

        /// <summary>What a stack reads as: its name, and the tier the team already owns of it.</summary>
        private static string ResearchLabel(ResearchMenuAdapter.ResearchItem item)
        {
            if (item.OwnedTier <= 0)
            {
                return item.Label;
            }

            string tierHeader = string.IsNullOrWhiteSpace(item.TierHeader)
                ? ModText.Get(ModStrings.Screens.Tier)
                : item.TierHeader;
            return ModText.Get(ModStrings.Screens.ResearchTier, item.Label, tierHeader, item.OwnedTier);
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            // The menu draws no close control: the mouse closes it by clicking the blocker behind it,
            // so the node the keyboard needs is the mod's own, running the game's own hide.
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "research:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => Live.Close())));
        }
    }
}
