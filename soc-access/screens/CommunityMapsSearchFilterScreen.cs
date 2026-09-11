using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The community maps browser's search and filter panel, made navigable as a graph. Two stops:
    /// the keyword box with the tag checkboxes under it, and the buttons in the footer.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the panel down the right of the window
    /// at [853,0,427,800] - the keyword box at [880,60,373,32], then a scrolling list of tags (1035
    /// px of rows in a 608 px viewport) in which each category draws a CAPTION of its own
    /// ("Content Type" at y 128, "Map Type" at y 336, "Languages" at y 651, "Contests" at y 1045)
    /// over its rows - and the footer at y 747 holding Search (x 880), Clear filter (x 977) and
    /// Cancel (x 1164).
    ///
    /// A drawn caption is the REGION its tags belong to: Alt+Up and Alt+Down jump between them and
    /// the name is spoken on the way in. Each tag is a checkbox, ticked through the game's own
    /// toggle. The panel scrolls itself, following the natively selected row.
    ///
    /// Escape is CLAIMED and runs mod.io's own Close: this panel is the browser's, not the game's,
    /// and nothing registers the key for it - the same finding the community maps modal recorded.
    /// </summary>
    public sealed class CommunityMapsSearchFilterScreen : LiveScreen<CommunityMapsSearchFilterAdapter>
    {
        private const string RowsStop = "search-filter-rows";
        private const string ButtonsStop = "search-filter-buttons";

        private readonly GameTextEditor _editor = new GameTextEditor();
        /// <summary>The search and filter panel, read off mod.io's own singleton every frame
        /// (<see cref="CommunityMapsSources"/>). mod.io hides this panel's game object when a search
        /// opens the results page, so the adapter's <c>IsPresent</c> is what answers "the player has
        /// left the filter" - the detector used to be told.</summary>
        protected override object ResolveMenu()
        {
            return CommunityMapsSources.SearchPanel;
        }

        protected override CommunityMapsSearchFilterAdapter Adapt(object menu)
        {
            return new CommunityMapsSearchFilterAdapter(menu);
        }

        public override string Key
        {
            get { return "community-maps-search-filter"; }
        }

        /// <summary>Layer 5: over the results it filters.</summary>
        public override int Layer
        {
            get { return 5; }
        }

        /// <summary>The panel's own drawn title ("Search &amp; filter").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Live != null && Live.Close();
        }

        /// <summary>The page's own editor, over the keyword box. GraphScreen takes the rest of its
        /// lifecycle: the raw-input and field-ownership answers, the per-frame update, and the
        /// abandon on leaving and on popping.</summary>
        public override GameTextEditor Editor
        {
            get { return _editor; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            AddKeyword(builder);
            AddTags(builder);

            builder.BeginStop(ButtonsStop);
            IReadOnlyList<CommunityMapsSearchFilterAdapter.ActionItem> actions = Live.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                AddAction(builder, actions[i]);
            }
        }

        /// <summary>The keyword box. Its label is the box's own placeholder, which is the only thing
        /// the panel writes next to it.</summary>
        private void AddKeyword(GraphBuilder builder)
        {
            GraphNodes.TmpEditField(
                builder,
                "search-filter:keyword",
                Live.SearchField,
                () => Live.SearchFieldLabel,
                _editor);
        }

        private void AddTags(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsSearchFilterAdapter.CategoryItem> categories = Live.GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                CommunityMapsSearchFilterAdapter.CategoryItem category = categories[i];
                if (category == null || category.Tags.Count == 0)
                {
                    continue;
                }

                builder.PushContext(category.Label);
                builder.SetRegion("search-filter:category/" + category.Index);
                for (int tagIndex = 0; tagIndex < category.Tags.Count; tagIndex++)
                {
                    CommunityMapsSearchFilterAdapter.TagItem tag = category.Tags[tagIndex];
                    if (tag == null)
                    {
                        continue;
                    }

                    string key = "search-filter:tag/" + category.Index + "/" + tag.Index;
                    NodeVtable vtable = GraphNodes.Checkbox(
                        () => tag.Label,
                        () => tag.IsSelected,
                        () => tag.Toggle(),
                        null,
                        null);
                    vtable.OnFocusVisual = tag.Focus;
                    // Synthesized: mod.io rebuilds the row objects as the list scrolls, and the tag
                    // itself - a category and a name - is what the row stands for either way.
                    builder.AddItem(new SyntheticNode(ControlId.For(Marker(key), key), vtable));
                }

                builder.PopContext();
                builder.SetRegion(null);
            }
        }

        private void AddAction(GraphBuilder builder, CommunityMapsSearchFilterAdapter.ActionItem action)
        {
            if (action == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => action.Label,
                () => action.Activate(),
                () => action.IsEnabled);
            vtable.OnFocusVisual = action.Focus;
            string key = "search-filter:action/" + action.Key;
            if (action.Button != null)
            {
                builder.AddItem(new DrawnNode(ControlId.For(action.Button, key), vtable, action.Button));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(Marker(key), key), vtable));
        }
    }
}
