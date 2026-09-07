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
    /// draws in capitals and which is also the screen name, the body as the start node, then the two
    /// controls in the order they are drawn.
    ///
    /// Measured 2026-09-07: the OK button is drawn at y 440, ABOVE the show-tutorials checkbox at
    /// y 480, while the widget tree read the checkbox first. The OK button stays declared while the
    /// game has it disabled and says so; only a button the game stops drawing goes away.
    ///
    /// ESCAPE is the game's: <c>TutorialMenu.Open</c> registers <c>UI.ExitMenu</c> on <c>Close</c>
    /// unconditionally, outside the gamepad branch, so the key closes the popup without the mod.
    /// </summary>
    public sealed class TutorialSimpleScreen : GraphScreen
    {
        private const string PopupStop = "tutorial-simple";

        private readonly TutorialSimpleAdapter _adapter;

        // A subject of its own for each node the popup gives no component for.
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        public TutorialSimpleScreen(TutorialSimpleAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TutorialMenu[] menus = Resources.FindObjectsOfTypeAll<TutorialMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TutorialMenu menu = menus[i];
                if (!TutorialSlideshowScreen.IsLiveSceneMenu(menu))
                {
                    continue;
                }

                TutorialSlideshowAdapter slideshowAdapter = new TutorialSlideshowAdapter(menu);
                if (slideshowAdapter.IsPresent())
                {
                    continue;
                }

                TutorialSimpleAdapter adapter = new TutorialSimpleAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new TutorialSimpleScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "tutorial-simple"; }
        }

        /// <summary>The tutorial's title, as the popup draws it (the game uppercases it itself).</summary>
        public override string ScreenName
        {
            get
            {
                string header = _adapter != null ? _adapter.Header : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
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

            builder.BeginStop(PopupStop);

            if (!string.IsNullOrWhiteSpace(_adapter.Header))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "tutorial-simple:heading"),
                    GraphNodes.Text(() => _adapter.Header)));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Description))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "tutorial-simple:body");
                builder.AddItem(new SyntheticNode(bodyId, GraphNodes.Text(() => _adapter.Description)));
                // Focus starts on the body: arrival says the title once as the screen name and then
                // what the tutorial has to say.
                builder.SetStart(bodyId);
            }

            Component ok = _adapter.OkButton;
            if (ok != null && _adapter.IsOkVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(ok, "tutorial-simple:ok"),
                    GraphNodes.Button(
                        () => ModText.Get(ModStrings.Screens.Ok),
                        () => _adapter.ActivateOk(),
                        _adapter.IsOkEnabled),
                    ok));
            }

            Component toggle = _adapter.TutorialsToggle;
            if (toggle != null)
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(toggle, "tutorial-simple:tutorials"),
                    GraphNodes.Checkbox(
                        () => _adapter.TutorialsToggleLabel,
                        _adapter.IsTutorialsChecked,
                        _adapter.ToggleTutorials),
                    toggle));
            }
        }
    }
}
