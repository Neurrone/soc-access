using System;
using System.Collections.Generic;
using System.Text;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.UI
{
    public sealed partial class GraphNavigator
    {
        // ---- type-ahead search ----
        //
        // Typing a letter on a graph screen searches what is on it and moves focus to the best
        // match; more letters narrow it, Up/Down step the matches, Home/End go to the ends, and
        // Escape or Backspace puts the keyboard back. There is no key that starts a search, because
        // a key nobody is told about is a key nobody uses.
        //
        // The characters do not come through the mod's bindings: a binding is one key meaning one
        // action, and this is text. They come from TypedCharacters, which is the input router's text
        // events in production and the dev server's /type route in a test - the same path either
        // way, gates included.

        private readonly TypeAhead _typeAhead = new TypeAhead();

        // Characters asked for over the dev server, taken by the next tick ahead of the keyboard.
        private readonly StringBuilder _typedQueue = new StringBuilder();

        // The tabular column focus was on when the search began: a result lands on the matched ROW
        // at that column, so searching never pulls the player out of the column they were reading.
        private int _searchColumn;

        // What the live search is looking through, built on its first keystroke and kept until it
        // ends: the fully-open build behind it is the most expensive thing a search does.
        private SearchScope _searchScope;

        // The branches this search opened, outermost first. Emptied when the search ends without
        // closing anything: the last landing's branch is where the player is standing.
        private readonly List<GraphNode> _searchOpened = new List<GraphNode>();

        /// <summary>Where typed characters come from this frame - the input router, in production.
        /// Null means nothing was typed.</summary>
        public Func<string> TypedCharacters;

        /// <summary>Whether a search is collecting the keyboard right now.</summary>
        public bool SearchIsActive
        {
            get { return _typeAhead.IsActive; }
        }

        /// <summary>What has been typed into the current search - for the dev server.</summary>
        public string SearchText
        {
            get { return _typeAhead.Buffer; }
        }

        /// <summary>How many controls the current search matched.</summary>
        public int SearchResultCount
        {
            get { return _typeAhead.ResultCount; }
        }

        /// <summary>
        /// Whether <paramref name="key"/> is one the focused screen is taking as TYPED TEXT rather
        /// than leaving to the game. Asked by the input router on the key press, so the game never
        /// sees a letter a search is about to use. Space only continues a search; on its own it is
        /// the game's.
        /// </summary>
        public bool TakesTypedKey(Key key)
        {
            if (!TypeAheadArmed())
            {
                return false;
            }

            if (key >= Key.A && key <= Key.Z)
            {
                return true;
            }

            return key == Key.Space && _typeAhead.HasBuffer;
        }

        /// <summary>Ask for <paramref name="text"/> to be typed - what the dev server's /type route
        /// does. Taken by the next tick, through the same gates a keypress passes.</summary>
        public void TypeText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _typedQueue.Append(text);
            }
        }

        /// <summary>
        /// The typing half of the frame: take what was typed and search with it. True when a
        /// character actually went into a search.
        /// </summary>
        public bool TypeAheadTick()
        {
            if (!TypeAheadArmed())
            {
                // Not ours to hear: a screen that opted out, or one handing the keyboard to the game.
                // Drained rather than left queued: letters typed at such a screen are not a search
                // the player deferred.
                NextTyped();
                ClearSearch();
                return false;
            }

            string typed = NextTyped();
            if (string.IsNullOrEmpty(typed))
            {
                return false;
            }

            if (OwnPendingFocus == null && _typeAhead.Strayed(FocusedKey))
            {
                // Something else moved focus; these letters start a fresh search from where the
                // player actually is.
                ClearSearch();
            }

            if (!_graph.Rerender())
            {
                return false;
            }

            bool taken = false;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (!char.IsLetter(c) && !(c == ' ' && _typeAhead.HasBuffer))
                {
                    continue;
                }

                GraphNode focused = _graph.CurrentNode;
                if (focused == null)
                {
                    break;
                }

                if (!_typeAhead.HasBuffer)
                {
                    _searchColumn = focused.Vtable.Column;
                }

                taken |= _typeAhead.Type(c, ScopeFor(focused));
            }

            return taken;
        }

        /// <summary>Give up the current search. Announced only when the player asked for it - the
        /// silent case is a search that stopped applying to where they are.</summary>
        public void ClearSearch(bool announce = false)
        {
            _searchScope = null;
            _searchOpened.Clear();
            if (!_typeAhead.IsActive && !_typeAhead.HasBuffer)
            {
                return;
            }

            _typeAhead.Clear();
            _searchColumn = 0;
            if (announce)
            {
                Say(ModText.Get(ModStrings.UI.SearchCleared), true);
            }
        }

        // While a search is up, the keys that walk its results belong to it. Everything else ends
        // the search and then does what it always does. True = the action was the search's.
        private bool SearchAction(string actionKey)
        {
            if (OwnPendingFocus == null && _typeAhead.Strayed(FocusedKey))
            {
                ClearSearch();
                return false;
            }

            switch (actionKey)
            {
                case "ui_up":
                    _typeAhead.Step(-1);
                    return true;
                case "ui_down":
                    _typeAhead.Step(1);
                    return true;
                case "ui_home":
                    _typeAhead.First();
                    return true;
                case "ui_end":
                    _typeAhead.Last();
                    return true;
                case "ui_back":
                case "ui_clear_search":
                    // The two keys that put the keyboard back (Escape and Backspace), and they go no
                    // further: the game must not also act on the key the player used to leave the
                    // search. Backspace is the way OUT rather than an edit of the typed letters: a
                    // search is re-typed in a keystroke, and the key is worth more as the exit.
                    ClearSearch(true);
                    return true;
                default:
                    ClearSearch();
                    return false;
            }
        }

        private bool TypeAheadArmed()
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            return _screen.AllowsTypeahead && !_screen.CapturesRawInput;
        }

        // The dev server's characters first - it queued them for exactly this - then the keyboard.
        private string NextTyped()
        {
            if (_typedQueue.Length > 0)
            {
                string queued = _typedQueue.ToString();
                _typedQueue.Length = 0;
                return queued;
            }

            Func<string> source = TypedCharacters;
            return source == null ? null : source();
        }

        // What this search looks through: whatever the screen offers, else the Tab-stop the cursor
        // is in, and either of those PLUS everything the page would declare with its branches open.
        private SearchScope ScopeFor(GraphNode focused)
        {
            if (_searchScope != null)
            {
                return _searchScope;
            }

            SearchScope declared = null;
            try
            {
                declared = _screen.TypeAheadScope(focused, _graph.Current);
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("GraphNavigator: " + _screen.Key + ".TypeAheadScope threw: " + e);
            }

            SearchScope basis = declared ?? SearchScope.OverStop(_graph.Current, focused.StopKey);
            _searchScope = SearchScope.Extend(basis, _graph.Current, DeepRender(), focused.StopKey, RevealDeep);
            return _searchScope;
        }

        // The page as it would be with every group open - what a search looks through beyond what is
        // declared.
        private GraphRender DeepRender()
        {
            try
            {
                GraphBuilder builder = new GraphBuilder(_state.Expanded);
                builder.ExpandAll = true;
                _screen.Build(builder);
                return builder.Build();
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("GraphNavigator: " + _screen.Key + ".Build with everything open threw: " + e);
                return null;
            }
        }

        // Land on a control only the fully-open build declared: open every branch it is inside,
        // outermost first, and answer with the control itself. Everything the LAST landing opened and
        // this one is not inside goes shut again.
        private ControlId RevealDeep(GraphNode node)
        {
            if (node == null)
            {
                return null;
            }

            List<GraphNode> branches = new List<GraphNode>();
            for (GraphNode at = node.Parent; at != null; at = at.Parent)
            {
                if (at.Expandable && at.Id != null)
                {
                    branches.Add(at);
                }
            }

            CloseOpenedExcept(branches);

            GraphRender standing = _graph == null ? null : _graph.Current;
            for (int i = branches.Count - 1; i >= 0; i--)
            {
                GraphNode branch = branches[i];
                GraphNode open = standing == null ? null : standing.NodeAt(branch.Id);
                if (open != null && open.Expanded)
                {
                    continue;
                }

                if (branch.Vtable.OnExpand != null)
                {
                    branch.Vtable.OnExpand();
                }
                else
                {
                    _state.Expanded.Add(branch.Id);
                }

                _searchOpened.Add(branch);
            }

            return node.Id;
        }

        private void CloseOpenedExcept(List<GraphNode> keep)
        {
            for (int i = _searchOpened.Count - 1; i >= 0; i--)
            {
                GraphNode opened = _searchOpened[i];
                if (Holds(keep, opened.Id))
                {
                    continue;
                }

                _searchOpened.RemoveAt(i);
                try
                {
                    if (opened.Vtable.OnCollapse != null)
                    {
                        opened.Vtable.OnCollapse();
                    }
                    else
                    {
                        _state.Expanded.Remove(opened.Id);
                    }
                }
                catch (Exception e)
                {
                    SocAccessMod.Instance?.LogWarning("GraphNavigator: closing a branch the search opened threw: " + e);
                }
            }
        }

        private static bool Holds(List<GraphNode> branches, ControlId id)
        {
            for (int i = 0; i < branches.Count; i++)
            {
                if (branches[i].Id != null && branches[i].Id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        // A result landing: focus it, keep the column the search started in, and read it out at
        // once. Answers with where focus ended up, which is what the search watches to know it is
        // still current.
        private ControlId LandOnSearchResult(ControlId id)
        {
            if (id == null)
            {
                return null;
            }

            if (!_graph.Focus(id))
            {
                // Not declared yet - the branches it is inside have just been asked to open, which
                // takes a build or several, so the landing goes to the pending-focus pass.
                FocusNode(id);
                return id;
            }

            FollowSearchColumn();
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return null;
            }

            CancelPendingFocus();
            SyncVisual(node);
            Say(GraphAnnouncer.Compose(_lastSpokenNode, node), true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            return node.Id;
        }

        // A search over a table matches rows and lands on their primary cell; the player was reading
        // a column, so step sideways back into it.
        private void FollowSearchColumn()
        {
            for (int step = 0; step < 64 && _searchColumn > 0; step++)
            {
                GraphNode node = _graph.CurrentNode;
                if (node == null || node.Vtable.SearchesAsItself || node.Vtable.Column >= _searchColumn)
                {
                    return;
                }

                if (!_graph.Move(GraphDir.Right).Moved)
                {
                    return;
                }
            }
        }

        private static void SayNoMatch(string text)
        {
            Say(ModText.Get(ModStrings.UI.SearchNoMatch, text), true);
        }
    }
}
