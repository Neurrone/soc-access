using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The one-page tutorial popup, made navigable as a graph. One stop: the title, which the game
    /// draws in capitals and which is also the screen name, the body as the start node - one part per
    /// paragraph the game broke it into - then the two controls in the order they are drawn.
    ///
    /// Measured 2026-09-07: the OK button is drawn at y 440, ABOVE the show-tutorials checkbox at
    /// y 480, while the widget tree read the checkbox first. The OK button stays declared while the
    /// game has it disabled and says so; only a button the game stops drawing goes away.
    ///
    /// ESCAPE is the game's: <c>TutorialMenu.Open</c> registers <c>UI.ExitMenu</c> on <c>Close</c>
    /// unconditionally, outside the gamepad branch, so the key closes the popup without the mod.
    /// </summary>
    public sealed class TutorialSimpleScreen : LiveScreen<TutorialSimpleAdapter>
    {
        private const string PopupStop = "tutorial-simple";

        // A subject of its own for each node the popup gives no component for.
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        /// <summary>The same tutorial menu the slideshow screen reads; the two adapters' own
        /// <c>IsPresent</c> are mutually exclusive - each answers true only for the container the
        /// menu has actually drawn - so the two screens never both stand up over one menu.</summary>
        private readonly ScreenSource<ITutorialMenu> _source = ScreenSource<ITutorialMenu>.FromProject();

        // The tutorial the popup was last showing. Mod-owned and outliving the menu on purpose: the
        // menu is the one object the project container holds for the whole game, so the adapter is
        // never replaced and there is nothing per-menu to hang this on.
        private ITutorialEntry _lastTutorial;

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override TutorialSimpleAdapter Adapt(object menu)
        {
            return new TutorialSimpleAdapter((TutorialMenu)menu);
        }

        public override string Key
        {
            get { return "tutorial-simple"; }
        }

        /// <summary>Layer 36: over the page it explains, combat included.</summary>
        public override int Layer
        {
            get { return 36; }
        }

        /// <summary>The tutorial's title, as the popup draws it (the game uppercases it itself).</summary>
        public override string ScreenName
        {
            get
            {
                string header = Live != null ? Live.Header : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            WatchTutorial();
        }

        /// <summary>
        /// One popup REPLACED BY ANOTHER, with no frame between them:
        /// <c>TutorialManager.ShowTutorialInternal</c> closes and re-opens the menu in a single call,
        /// so the popup never goes inactive, the project container's menu object never changes and
        /// neither the manager's arrival announcement nor the adapter slot's own ever runs. The
        /// heading and body nodes are keyed on subjects of the screen's own, so the second popup is
        /// silent outright.
        ///
        /// The entry the menu is showing is what tells the two apart: a new one says the new title
        /// and gives up the cursor, which the next <c>EnsureFocus</c> seats on the body - now the
        /// new tutorial's - and reads.
        /// </summary>
        private void WatchTutorial()
        {
            if (!IsActive())
            {
                _lastTutorial = null;
                return;
            }

            ITutorialEntry tutorial = Live.CurrentTutorial;
            if (ReferenceEquals(tutorial, _lastTutorial))
            {
                return;
            }

            bool replaced = _lastTutorial != null;
            _lastTutorial = tutorial;
            if (!replaced)
            {
                return;
            }

            SayNameIfChanged();
            GraphNavigator navigator = Navigator;
            if (navigator != null && ReferenceEquals(navigator.Screen, this))
            {
                navigator.Blur();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(PopupStop);

            if (!string.IsNullOrWhiteSpace(Live.Header))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "tutorial-simple:heading"),
                    GraphNodes.Text(() => Live.Header)));
            }

            // The guard and the node ask the same question, and the adapter splits the text at most
            // once a frame, so the node reads the live answer rather than one captured here.
            if (Live.DescriptionLines.Count > 0)
            {
                ControlId bodyId = ControlId.For(_bodyKey, "tutorial-simple:body");
                builder.AddItem(new SyntheticNode(
                    bodyId,
                    GraphNodes.Paragraphs(() => Live.DescriptionLines)));
                // Focus starts on the body: arrival says the title once as the screen name and then
                // what the tutorial has to say.
                builder.SetStart(bodyId);
            }

            Component ok = Live.OkButton;
            if (ok != null && Live.IsOkVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(ok, "tutorial-simple:ok"),
                    GraphNodes.Button(
                        () => ModText.Get(ModStrings.Screens.Ok),
                        () => Live.ActivateOk(),
                        Live.IsOkEnabled),
                    ok));
            }

            Component toggle = Live.TutorialsToggle;
            if (toggle != null)
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(toggle, "tutorial-simple:tutorials"),
                    GraphNodes.Checkbox(
                        () => Live.TutorialsToggleLabel,
                        Live.IsTutorialsChecked,
                        Live.ToggleTutorials),
                    toggle));
            }
        }
    }
}
