using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using TMPro;

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
    public sealed class CommunityMapsSearchFilterScreen : GraphScreen
    {
        private const string RowsStop = "search-filter-rows";
        private const string ButtonsStop = "search-filter-buttons";

        private readonly CommunityMapsSearchFilterAdapter _adapter;
        private readonly GameTextEditor _editor = new GameTextEditor();
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public CommunityMapsSearchFilterScreen(CommunityMapsSearchFilterAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsSearchFilterAdapter adapter = CommunityMapsSearchFilterAdapter.TryCreate();
            return adapter != null && adapter.IsPresent()
                ? new CommunityMapsSearchFilterScreen(adapter)
                : null;
        }

        public override string Key
        {
            get { return "community-maps-search-filter"; }
        }

        /// <summary>The panel's own drawn title ("Search &amp; filter").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return IsPresent(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.Close();
        }

        /// <summary>While the keyboard is on its way to the keyword box, what the player types next is
        /// meant for that box and must not start a search of the panel.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool OwnsGameField
        {
            get { return _editor.Pending || _editor.Editing; }
        }

        /// <summary>Kept for the detector, which calls it whenever the panel's content changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Update()
        {
            base.Update();
            _editor.Update(IsPresent());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            AddKeyword(builder);
            AddTags(builder);

            builder.BeginStop(ButtonsStop);
            IReadOnlyList<CommunityMapsSearchFilterAdapter.ActionItem> actions = _adapter.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                AddAction(builder, actions[i]);
            }
        }

        /// <summary>The keyword box. It is one of mod.io's own TMP fields rather than one of the
        /// game's, and the editing contract is the same; its label is the box's own placeholder,
        /// which is the only thing the panel writes next to it.</summary>
        private void AddKeyword(GraphBuilder builder)
        {
            TMP_InputField field = _adapter.SearchField;
            if (field == null || !field.gameObject.activeInHierarchy)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.EditField(
                () => _adapter.SearchFieldLabel,
                () => _editor.Editing ? null : field.text,
                () => _editor.Request(_adapter.SearchField),
                () => field.interactable);
            // Arriving puts the game's own selection on the box. Measured: without it, an activation
            // that follows a tag row - whose focus visual selected mod.io's toggle - selects the box
            // but never makes it FOCUSED, and the edit ends in silence; with it, the handover lands
            // every time.
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(field);
            builder.AddItem(new DrawnNode(
                ControlId.For(field, "search-filter:keyword"),
                vtable,
                field));
        }

        private void AddTags(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsSearchFilterAdapter.CategoryItem> categories = _adapter.GetCategories();
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
            string key = "search-filter:action/" + action.Id;
            if (action.Button != null)
            {
                builder.AddItem(new DrawnNode(ControlId.For(action.Button, key), vtable, action.Button));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(Marker(key), key), vtable));
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
