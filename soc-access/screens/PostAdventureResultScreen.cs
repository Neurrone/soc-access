using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
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
    public sealed class PostAdventureResultScreen : LiveScreen<PostAdventureResultAdapter>
    {
        private const string ResultStop = "post-adventure-result";

        /// <summary>The one post-adventure window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<IPostAdventureMenu> _source =
            ScreenSource<IPostAdventureMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override PostAdventureResultAdapter Adapt(object menu)
        {
            return new PostAdventureResultAdapter((PostAdventureMenu)menu);
        }

        public override string Key
        {
            get { return "post-adventure-result"; }
        }

        /// <summary>Layer 32: the end of the adventure, over everything in it.</summary>
        public override int Layer
        {
            get { return 32; }
        }

        /// <summary>The result the menu draws ("Defeat", or the victory text), read once on arrival
        /// and again as the page's first line.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? ResultTitle() : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>What the page is called: the words the menu drew over the outcome, and - where it
        /// drew none the mod can read - the mod's own word for the outcome the menu is showing.
        /// </summary>
        private string ResultTitle()
        {
            string title = Live.ResultTitle;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (Live.IsVictory)
            {
                return ModText.Get(ModStrings.Combat.Victory);
            }

            if (Live.IsDefeat)
            {
                return ModText.Get(ModStrings.Combat.Defeat);
            }

            return ModText.Get(ModStrings.Screens.PostAdventureResult);
        }

        /// <summary>The heading over the outcome's lines: the menu's own, or the mod's word for what
        /// they are.</summary>
        private string ObjectivesTitle()
        {
            string title = Live.ObjectivesTitle;
            return string.IsNullOrWhiteSpace(title) ? ModText.Get(ModStrings.Screens.Objectives) : title;
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(ResultStop);
            AddLine(builder, "title", ResultTitle);

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
            if (Live.DescriptionVisible)
            {
                return AddParagraphs(builder, "description", () => Live.DescriptionLines);
            }

            IReadOnlyList<PostAdventureResultAdapter.ObjectiveEntry> objectives = Live.GetObjectives();
            if (objectives == null || objectives.Count == 0)
            {
                return null;
            }

            ControlId first = null;
            builder.PushContext(ObjectivesTitle());
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
            AddButton(builder, "stats", Live.StatsButton);

            List<KeyValuePair<float, UIButton>> drawn = new List<KeyValuePair<float, UIButton>>(3);
            AddDrawn(drawn, Live.ContinueCampaignButton);
            AddDrawn(drawn, Live.RestartMapButton);
            AddDrawn(drawn, Live.QuitToMainButton);
            AddDrawn(drawn, Live.LoadButton);
            DrawnOrder.SortByKey(drawn);
            for (int i = 0; i < drawn.Count; i++)
            {
                AddButton(builder, "button/" + i, drawn[i].Value);
            }

            AddButton(builder, "player-stats", Live.PlayerStatsButton);
        }

        private void AddDrawn(List<KeyValuePair<float, UIButton>> drawn, UIButton button)
        {
            if (!Live.IsButtonVisible(button))
            {
                return;
            }

            drawn.Add(new KeyValuePair<float, UIButton>(DrawnOrder.LeftOf(button as Component), button));
        }

        private void AddButton(GraphBuilder builder, string key, UIButton button)
        {
            Component component = button as Component;
            if (component == null || !Live.IsButtonVisible(button))
            {
                return;
            }

            UIButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => Live.GetButtonLabel(it),
                () => Live.ActivateButton(it),
                () => Live.IsButtonEnabled(it));
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

        /// <summary>The same, for a text the menu may have written in more than one paragraph: one
        /// spoken line, one review-buffer line per paragraph.</summary>
        private ControlId AddParagraphs(GraphBuilder builder, string key, Func<IList<string>> lines)
        {
            IList<string> paragraphs = lines();
            if (paragraphs == null || paragraphs.Count == 0)
            {
                return null;
            }

            ControlId id = ControlId.For(Marker(key), "post-adventure:" + key);
            builder.AddItem(new SyntheticNode(id, GraphNodes.Paragraphs(lines)));
            return id;
        }
    }
}
