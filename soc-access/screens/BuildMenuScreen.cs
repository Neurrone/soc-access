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
    /// The build menu, made navigable as a graph. Five places to be, in the order the menu draws
    /// them: the build site and its controls, the size tabs, the list of buildings, the details of
    /// the one selected, and the close cross.
    ///
    /// Measured 2026-09-07: header, size tabs Small / Medium / Large in a row, the size and
    /// build-time captions, the building column on the left, the details on the right (name,
    /// description, income, cost, reason), the auto-select toggle at the top right.
    ///
    /// ARRIVING ON A BUILDING SELECTS IT (owner ruling 2026-09-07): the click only refills the
    /// details pane on the right, so arriving at a building and reading what it costs is one event,
    /// which is what the mouse does. THE SIZE TABS DO NOT switch on arrival: switching re-pools every
    /// building button under them, so walking the bar would take the list the player is reading away.
    /// Their focus visual is the game's own selection alone and Enter is the switch. The tier tabs in
    /// the details are the same: they redraw the pane under them, and they are declared as ONE ROW
    /// (2026-09-07), the bar the pane draws, walked with Left and Right.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu IS an
    /// <c>AdventureMenuBackground</c> with <c>_canClose</c> true, so it draws the close cross and
    /// registers <c>UI.ExitMenu</c> on its own close in <c>AnimateEntry</c>.
    /// </summary>
    public sealed class BuildMenuScreen : LiveScreen<BuildMenuAdapter>
    {
        private const string SiteStop = "build-site";
        private const string SizesStop = "build-sizes";
        private const string BuildingsStop = "build-buildings";
        private const string DetailsStop = "build-details";
        private const string CloseStop = "build-close";

        /// <summary>The one build window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<IBuildMenu> _source =
            ScreenSource<IBuildMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override BuildMenuAdapter Adapt(object menu)
        {
            return new BuildMenuAdapter((BuildMenu)menu);
        }

        public override string Key
        {
            get { return "build-menu"; }
        }

        /// <summary>Layer 24: over the settlement page that opens it.</summary>
        public override int Layer
        {
            get { return 24; }
        }

        /// <summary>The header the menu draws over the page ("Build").</summary>
        public override string ScreenName
        {
            get
            {
                string header = Live != null ? Live.HeaderText : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(SiteStop);
            BuildSite(builder);

            builder.BeginStop(SizesStop);
            BuildSizes(builder);

            builder.BeginStop(BuildingsStop);
            BuildBuildings(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the build site ----

        private void BuildSite(GraphBuilder builder)
        {
            Component tutorial = Live.TutorialButton;
            if (tutorial != null && Live.IsTutorialButtonVisible())
            {
                AddButton(
                    builder,
                    tutorial,
                    "tutorial",
                    Live.GetTutorialButtonLabel,
                    () => Live.ActivateTutorial(),
                    null);
            }

            AddLine(builder, "current-site", () => Live.BuildSiteSummary);

            AddButton(
                builder,
                Live.PreviousBuildSiteButton,
                "previous-site",
                () => Live.PreviousBuildSiteButtonLabel,
                () => Live.ActivatePreviousBuildSite(),
                Live.IsPreviousBuildSiteEnabled);

            AddButton(
                builder,
                Live.NextBuildSiteButton,
                "next-site",
                () => Live.NextBuildSiteButtonLabel,
                () => Live.ActivateNextBuildSite(),
                Live.IsNextBuildSiteEnabled);

            Component toggle = Live.AutoSelectToggle;
            if (toggle != null && Live.IsAutoSelectVisible())
            {
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => Live.AutoSelectLabel,
                    Live.IsAutoSelectChecked,
                    Live.ToggleAutoSelect);
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(toggle);
                builder.AddItem(new DrawnNode(
                    ControlId.For(toggle, "build:auto-select"),
                    vtable,
                    toggle));
            }
        }

        // ---- the size tabs ----

        private void BuildSizes(GraphBuilder builder)
        {
            IReadOnlyList<BuildMenuAdapter.CategoryItem> categories = Live.GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                BuildMenuAdapter.CategoryItem category = categories[i];
                Component button = category.Button;
                if (button == null)
                {
                    continue;
                }

                BuildMenuAdapter.CategoryItem it = category;
                NodeVtable vtable = GraphNodes.Tab(
                    () => it.Label,
                    () => it.IsSelected,
                    () => it.Enabled);
                // Drawn under the bar: how long anything of this size takes to build.
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.BuildTime, watch: false));
                vtable.OnActivate = () => Live.ActivateCategory(it.Size);
                vtable.OnFocusVisual = () => Live.SelectCategory(it.Size);
                builder.AddItem(new DrawnNode(
                    ControlId.For(button, "build:size/" + category.Index),
                    vtable,
                    button));
            }
        }

        // ---- the buildings ----

        private void BuildBuildings(GraphBuilder builder)
        {
            IReadOnlyList<BuildMenuAdapter.BuildingItem> buildings = Live.GetBuildings();
            ControlId selected = null;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildMenuAdapter.BuildingItem building = buildings[i];
                Component button = building.Button;
                if (button == null)
                {
                    continue;
                }

                BuildMenuAdapter.BuildingItem it = building;
                string label = building.Label;
                NodeVtable vtable = GraphNodes.Button(
                    () => label,
                    () => { if (it.Focus != null) it.Focus(); },
                    () => it.IsAvailable,
                    it.Tooltip != null ? it.Tooltip() : null);
                vtable.Announcements.Add(GraphNodes.SelectedPart(() => it.IsSelected));
                // Arrival IS the selection: the click only refills the details pane beside the list.
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                ControlId id = ControlId.For(button, "build:building/" + i);
                builder.AddItem(new DrawnNode(id, vtable, button));
                if (building.IsSelected)
                {
                    selected = id;
                }
            }

            // The building the details pane is describing, so arriving reads what the player sees.
            builder.LandStopOn(selected);
        }

        // ---- the details ----

        private void BuildDetails(GraphBuilder builder)
        {
            IList<string> summary = SelectedBuildingSummaryLines();
            if (summary.Count > 0)
            {
                // One spoken line, one review-buffer line per paragraph the game wrote it in.
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("summary"), "build:summary"),
                    GraphNodes.Paragraphs(() => summary)));
            }

            BuildTiers(builder);
            BuildSection(builder, "available-research", Live.AvailableResearchHeader, Live.GetAvailableResearchItems());

            IReadOnlyList<BuildMenuAdapter.SectionMenu> sections = Live.GetIncomeAndGarrisonMenus();
            for (int i = 0; i < sections.Count; i++)
            {
                BuildSection(builder, "section/" + i, sections[i].Label, sections[i].Items);
            }

            BuildRequirements(builder);
            // Both are composed off the pane the build has just read, so each is read once and the
            // node is declared over that string rather than over another read of it.
            string cost = Live.CurrentTierCostText;
            AddLine(builder, "cost", () => cost);
            string warning = Live.CannotBuyText;
            AddLine(builder, "warning", () => warning);

            BuildPurchase(builder);
        }

        /// <summary>The building the pane is describing, read as the player sees it: the name and the
        /// opening paragraph of the description as one line, then a line per further paragraph.
        /// </summary>
        private IList<string> SelectedBuildingSummaryLines()
        {
            IList<string> description = Live.SelectedBuildingDescriptionLines;
            List<string> lines = new List<string>();
            string first = JoinParts(
                Live.SelectedBuildingName,
                description.Count > 0 ? description[0] : null);
            if (!string.IsNullOrWhiteSpace(first))
            {
                lines.Add(first);
            }

            for (int i = 1; i < description.Count; i++)
            {
                lines.Add(description[i]);
            }

            return lines;
        }

        private static string JoinParts(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return first.TrimEnd('.') + ". " + second;
        }

        /// <summary>The tier tabs the details pane draws as ONE bar, declared as one row: Left and
        /// Right walk it, Up and Down step past it to the rest of the pane, and the position is the
        /// tab's place in the bar. Enter is still the switch, for the reason the size tabs are
        /// (arriving must not redraw the pane the player is reading).</summary>
        private void BuildTiers(GraphBuilder builder)
        {
            IReadOnlyList<BuildMenuAdapter.TierItem> tiers = Live.GetTiers();
            List<BuildMenuAdapter.TierItem> drawn = new List<BuildMenuAdapter.TierItem>();
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Button != null)
                {
                    drawn.Add(tiers[i]);
                }
            }

            if (drawn.Count == 0)
            {
                return;
            }

            builder.StartRow("build:tiers");
            for (int i = 0; i < drawn.Count; i++)
            {
                BuildMenuAdapter.TierItem tier = drawn[i];
                Component button = tier.Button;
                BuildMenuAdapter.TierItem it = tier;
                NodeVtable vtable = GraphNodes.Tab(
                    () => it.Label,
                    () => it.IsSelected,
                    null,
                    it.Tooltip != null ? it.Tooltip() : null);
                vtable.OnActivate = () => { if (it.Activate != null) it.Activate(); };
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                builder.AddItem(new DrawnNode(
                    ControlId.For(button, "build:tier/" + it.Level),
                    vtable,
                    button));
            }

            builder.EndRow();
        }

        /// <summary>One of the bands the details pane draws under a caption - the available research,
        /// the income, the garrison. The caption is the REGION its rows belong to rather than a row of
        /// its own, because there is nothing there to operate.</summary>
        private void BuildSection(
            GraphBuilder builder,
            string key,
            string caption,
            IReadOnlyList<BuildMenuAdapter.SectionItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion("build:" + key);
            }

            for (int i = 0; i < items.Count; i++)
            {
                BuildMenuAdapter.SectionItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                BuildMenuAdapter.SectionItem it = item;
                string label = item.Label;
                Component target = item.Target;
                NodeVtable vtable = GraphNodes.Text(
                    () => label,
                    null,
                    it.Tooltip != null ? it.Tooltip() : null);
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                ControlId id = ControlId.For(
                    target != null ? (object)target : Marker(key + "/" + i),
                    "build:" + key + "/" + i);
                builder.AddItem(target == null
                    ? (NodeDeclaration)new SyntheticNode(id, vtable)
                    : new DrawnNode(id, vtable, target));
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        private void BuildRequirements(GraphBuilder builder)
        {
            IReadOnlyList<BuildMenuAdapter.RequirementItem> requirements = Live.GetRequirements();
            if (requirements.Count == 0)
            {
                return;
            }

            string caption = Live.RequirementsHeader;
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion("build:requirements");
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                BuildMenuAdapter.RequirementItem requirement = requirements[i];
                BuildMenuAdapter.RequirementItem it = requirement;
                string label = requirement.Label;
                NodeVtable vtable = GraphNodes.Text(() => label, null, it.Tooltip);
                // The row is the one place the player learns a requirement is not met, so the state
                // is said once, after the requirement it is about.
                vtable.Announcements.Add(GraphNodes.ValuePart(
                    () => it.IsMet ? null : ModText.Get(ModStrings.UI.StatusMissing)));
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("requirement/" + i), "build:requirement/" + i),
                    vtable));
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        private void BuildPurchase(GraphBuilder builder)
        {
            Component purchase = Live.PurchaseButton;
            if (purchase == null || !Live.IsBuildButtonVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.BuildButtonLabel,
                () => Live.ActivateBuild(),
                Live.IsBuildButtonEnabled);
            vtable.OnFocusVisual = () => Live.FocusBuildButton();
            builder.AddItem(new DrawnNode(
                ControlId.For(purchase, "build:purchase"),
                vtable,
                purchase));
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            GraphNodes.DrawnClose(
                builder,
                "build:close",
                Live.CloseButton,
                Live.IsCloseVisible,
                () => Live.ActivateClose());
        }

        // ---- shared ----

        private void AddButton(
            GraphBuilder builder,
            Component component,
            string key,
            Func<string> label,
            Action activate,
            Func<bool> enabled)
        {
            if (component == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(label, activate, enabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(component);
            builder.AddItem(new DrawnNode(ControlId.For(component, "build:" + key), vtable, component));
        }

        private void AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            GraphNodes.TextLine(builder, Marker(key), "build:" + key, text);
        }
    }
}
