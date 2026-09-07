using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The page a finished game ends on, made navigable as a graph. ONE place to be: the result, what
    /// it says, and the buttons under it - the page is a dialog, and a dialog is one stop.
    ///
    /// Measured 2026-09-07 (<c>PostAdventureMenu</c>): the result ("Defeat", or the victory text)
    /// centred, the description under it ("Neurrone surrendered.") or a list of objective rows in its
    /// place, then the Statistics button drawn ABOVE the pair Main Menu (LEFT, x 506) and Load game
    /// (RIGHT, x 648), and last the "Your player statistics has been updated" link, which fades in
    /// about two seconds later than the rest. A campaign draws Continue Campaign or Restart Map in
    /// place of the pair. Everything below Statistics is declared in the order of the drawn left
    /// edges, measured every build, so the campaign variants read in their own drawn order too.
    ///
    /// The reading order is Endless Space 2 Access's three-part dialog contract, as the message
    /// dialogs use it: the result title is BOTH the screen's name and its first line, so arrival says
    /// it once; focus starts on what the page actually tells the player - the description, or the
    /// first objective where the page lists objectives instead; then the buttons. The objectives sit
    /// under the caption the page draws over them, so entering them names the list once.
    ///
    /// THERE IS NO CLOSE AND NO ESCAPE (owner ruling, 2026-09-07): the menu draws no close control
    /// and registers no input callback of any kind, so there is nothing for a close node to press and
    /// nothing for the key to do (<c>ConsumesBack</c> false).
    /// </summary>
    public sealed class PostAdventureResultScreen : GraphScreen
    {
        private const string ResultStop = "post-adventure-result";

        private readonly PostAdventureResultAdapter _adapter;

        // A subject of its own per synthesized line, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the menu gives no component the screen can key its texts on.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public PostAdventureResultScreen(PostAdventureResultAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PostAdventureMenu[] menus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PostAdventureResultAdapter adapter = new PostAdventureResultAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new PostAdventureResultScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "post-adventure-result"; }
        }

        /// <summary>The result the menu draws ("Defeat", or the victory text), read once on arrival
        /// and again as the page's first line.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.ResultTitle : null;
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

            builder.BeginStop(ResultStop);
            AddLine(builder, "title", () => _adapter.ResultTitle);

            ControlId start = BuildMessage(builder);
            BuildButtons(builder);

            if (start != null)
            {
                // What the page is telling the player, the title having been said as the screen's name.
                builder.SetStart(start);
            }
        }

        // ---- what the page says ----

        /// <summary>The description the menu drew, or the objectives it drew in place of one, under
        /// their own caption. Answers where focus starts.</summary>
        private ControlId BuildMessage(GraphBuilder builder)
        {
            if (_adapter.DescriptionVisible)
            {
                return AddLine(builder, "description", () => _adapter.Description);
            }

            IReadOnlyList<PostAdventureResultAdapter.ObjectiveEntry> objectives = _adapter.GetObjectives();
            if (objectives == null || objectives.Count == 0)
            {
                return null;
            }

            ControlId first = null;
            builder.PushContext(_adapter.ObjectivesTitle);
            for (int i = 0; i < objectives.Count; i++)
            {
                PostAdventureResultAdapter.ObjectiveEntry objective = objectives[i];
                if (objective == null || !objective.IsVisible)
                {
                    continue;
                }

                ControlId id = ControlId.For(Marker("objective/" + i), "post-adventure:objective/" + i);
                builder.AddItem(new SyntheticNode(id, GraphNodes.Text(() => objective.Label)));
                if (first == null)
                {
                    first = id;
                }
            }

            builder.PopContext();
            return first;
        }

        // ---- the buttons ----

        /// <summary>Statistics first, where the menu draws it, then everything under it in the order
        /// of the drawn left edges, and last the player-statistics link - which is declared only once
        /// the menu has faded it in, since it arrives about two seconds after the rest.</summary>
        private void BuildButtons(GraphBuilder builder)
        {
            AddButton(builder, "stats", _adapter.StatsButton);

            List<KeyValuePair<float, UIButton>> drawn = new List<KeyValuePair<float, UIButton>>(3);
            AddDrawn(drawn, _adapter.ContinueCampaignButton);
            AddDrawn(drawn, _adapter.RestartMapButton);
            AddDrawn(drawn, _adapter.QuitToMainButton);
            AddDrawn(drawn, _adapter.LoadButton);
            SortByLeftEdge(drawn);
            for (int i = 0; i < drawn.Count; i++)
            {
                AddButton(builder, "button/" + i, drawn[i].Value);
            }

            AddButton(builder, "player-stats", _adapter.PlayerStatsButton);
        }

        private void AddDrawn(List<KeyValuePair<float, UIButton>> drawn, UIButton button)
        {
            if (!_adapter.IsButtonVisible(button))
            {
                return;
            }

            Component component = button as Component;
            float left = component != null && component.transform != null ? component.transform.position.x : 0f;
            drawn.Add(new KeyValuePair<float, UIButton>(left, button));
        }

        /// <summary>Stable insertion sort, so two buttons at one x keep the order the menu declares
        /// them in.</summary>
        private static void SortByLeftEdge(List<KeyValuePair<float, UIButton>> drawn)
        {
            for (int i = 1; i < drawn.Count; i++)
            {
                KeyValuePair<float, UIButton> moving = drawn[i];
                int j = i - 1;
                while (j >= 0 && drawn[j].Key > moving.Key)
                {
                    drawn[j + 1] = drawn[j];
                    j--;
                }

                drawn[j + 1] = moving;
            }
        }

        private void AddButton(GraphBuilder builder, string key, UIButton button)
        {
            Component component = button as Component;
            if (component == null || !_adapter.IsButtonVisible(button))
            {
                return;
            }

            UIButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.GetButtonLabel(it),
                () => _adapter.ActivateButton(it),
                () => _adapter.IsButtonEnabled(it));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(component);
            builder.AddItem(new DrawnNode(ControlId.For(component, "post-adventure:" + key), vtable, component));
        }

        // ---- the lines the menu gives nothing to key on ----

        private ControlId AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(text()))
            {
                return null;
            }

            ControlId id = ControlId.For(Marker(key), "post-adventure:" + key);
            builder.AddItem(new SyntheticNode(id, GraphNodes.Text(text)));
            return id;
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
