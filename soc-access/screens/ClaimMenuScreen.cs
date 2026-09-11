using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The menu that asks what to do with a settlement just taken, made navigable as a graph. One
    /// stop in the dialog order: the heading, which is also the screen name, the body as the start
    /// node, then one node per choice the menu is offering.
    ///
    /// The choices are BUTTONS rather than a radio group (owner ruling 2026-09-07): picking one is
    /// doing it. <c>ClaimMenu</c> hangs the command off the toggle's value-changed event and hides
    /// the menu in the same breath, so there is no chosen state for a player to arrive on.
    ///
    /// Each choice draws three texts of its own - the title, the duration beside it, and the
    /// paragraph below - and they read in that order as announcement parts of the one node, not as a
    /// details section: a part is a review-buffer line already, and a section repeating it would put
    /// each line in the buffer twice. Measured 2026-09-07: Occupy, Raze and Loot are stacked top to
    /// bottom, which is also the order the settings list them in; the order is taken off each
    /// toggle's own transform every build so a layout the game changes is followed.
    ///
    /// ESCAPE is the game's: <c>ClaimMenu.Open</c> registers <c>UI.ExitMenu</c> on
    /// <c>HandleExitPressed</c>, which hides the menu only when the entity is already owned and
    /// answers with the negative beep otherwise. There is NO close node here: the game draws no close
    /// control, the choices are the exits, and the one hide path Escape reaches is the game's own,
    /// which is entitled to refuse.
    /// </summary>
    public sealed class ClaimMenuScreen : LiveScreen<ClaimMenuAdapter>
    {
        private const string DialogStop = "claim-menu";

        // A subject of its own for each node the menu gives no component for; two nodes sharing one
        // subject would collapse onto whichever was declared first.
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        /// <summary>The one claim window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<IClaimMenu> _source =
            ScreenSource<IClaimMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override ClaimMenuAdapter Adapt(object menu)
        {
            return new ClaimMenuAdapter((ClaimMenu)menu);
        }

        public override string Key
        {
            get { return "claim-menu"; }
        }

        /// <summary>Layer 30: raised by the battle result, over it.</summary>
        public override int Layer
        {
            get { return 30; }
        }

        /// <summary>The heading the menu draws over the choices ("Siege").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            Live?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            base.OnPop();
            Live?.HideNativeTooltip();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(DialogStop);

            if (!string.IsNullOrWhiteSpace(Live.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "claim-menu:heading"),
                    GraphNodes.Text(() => Live.Title)));
            }

            // The body is broken into its paragraphs ONCE: the guard and the node ask the same
            // question, and the paragraph node counts them at build.
            IList<string> bodyLines = Live.BodyLines;
            if (bodyLines.Count > 0)
            {
                ControlId bodyId = ControlId.For(_bodyKey, "claim-menu:body");
                builder.AddItem(new SyntheticNode(
                    bodyId,
                    GraphNodes.Paragraphs(() => bodyLines)));
                // Focus starts on the body, so arrival reads the heading once as the screen name and
                // then what the menu is asking before the choices.
                builder.SetStart(bodyId);
            }

            List<ClaimMenuAdapter.ChoiceItem> choices = InDrawnOrder(Live.GetChoices());
            for (int i = 0; i < choices.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem choice = choices[i];
                builder.AddItem(new DrawnNode(
                    ControlId.For(choice.Toggle, "claim-menu:" + IdSuffix(choice.Kind)),
                    Choice(choice),
                    choice.Toggle));
            }
        }

        /// <summary>What each of the game's four choices is called in a node id.</summary>
        private static string IdSuffix(ClaimChoiceKind kind)
        {
            switch (kind)
            {
                case ClaimChoiceKind.Raze:
                    return "raze";
                case ClaimChoiceKind.Loot:
                    return "loot";
                case ClaimChoiceKind.Convert:
                    return "convert";
                default:
                    return "occupy";
            }
        }

        /// <summary>One choice: what it is called, then the duration drawn beside it and the
        /// paragraphs of the text drawn below it, then the game's own click.</summary>
        private static NodeVtable Choice(ClaimMenuAdapter.ChoiceItem choice)
        {
            NodeVtable vtable = GraphNodes.Button(
                choice.GetTitle,
                () => choice.Activate(),
                () => choice.IsEnabled);
            vtable.Announcements.Add(GraphNodes.ValuePart(choice.GetDuration, watch: false));
            GraphNodes.ParagraphParts(vtable, choice.GetDescriptionLines);
            // The choice the cursor is on is the one the game shows as selected; the menu pushes a
            // selection layer with the first toggle as its default, and this keeps that in step.
            vtable.OnFocusVisual = () => choice.Focus();
            return vtable;
        }

        /// <summary>The choices top to bottom as the game draws them, measured off each toggle's own
        /// transform every build; the insertion sort is stable, so two choices at one height keep the
        /// adapter's order.</summary>
        private static List<ClaimMenuAdapter.ChoiceItem> InDrawnOrder(IReadOnlyList<ClaimMenuAdapter.ChoiceItem> choices)
        {
            List<ClaimMenuAdapter.ChoiceItem> drawn = new List<ClaimMenuAdapter.ChoiceItem>();
            List<float> tops = new List<float>();
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem choice = choices[i];
                if (choice == null || choice.Toggle == null)
                {
                    continue;
                }

                drawn.Add(choice);
                tops.Add(choice.Toggle.transform.position.y);
            }

            DrawnOrder.SortDescending(drawn, tops);
            return drawn;
        }

    }
}
