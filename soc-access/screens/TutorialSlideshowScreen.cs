using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The paged tutorial panel, made navigable as the list of pages it is - Endless Space 2 Access's
    /// pager, as the owner ruled on 2026-09-07.
    ///
    /// Two stops. The first is one row per page the tutorial has: standing on a row is what turns the
    /// panel to that page, so Up and Down read the tutorial through. The rows are keyed by their
    /// index rather than by a widget, because there is one drawn viewer and N paged contents - the
    /// panel rewrites the same description box - and every row therefore stands or falls with that
    /// box. The row of the page the panel is already showing is where focus lands.
    ///
    /// The turn happens inside the row's own text, which is the only thing that runs between the
    /// cursor arriving and the landing being spoken, and it is guarded twice: the panel is already on
    /// the page for every read but the first, and a read on a row the cursor is not standing on - a
    /// graph dump, a type-ahead pass over the stop - turns nothing. The turned-to page then reads as
    /// one line per paragraph the tutorial wrote it in.
    ///
    /// The game's Previous and Next arrows and its "1/3" counter are NOT declared: the list does what
    /// the arrows do, and the engine's own position stamp says which page the cursor is on.
    ///
    /// The second stop is what the panel draws along its bottom edge, in drawn order (measured
    /// 2026-09-07: the show-tutorials checkbox at x 434, then the closing button to the right of it).
    /// The closing button is declared from the start and says it is unavailable until the game
    /// enables it, which <c>UpdatePage</c> does once the last page has been shown - so turning to the
    /// last row enables it exactly as clicking through would. A single-page tutorial has one row and
    /// the button enabled at once.
    ///
    /// ESCAPE is the game's: <c>TutorialMenu.Open</c> registers <c>UI.ExitMenu</c> on <c>Close</c>
    /// unconditionally, so the key closes the panel from any page.
    /// </summary>
    public sealed class TutorialSlideshowScreen : LiveScreen<TutorialSlideshowAdapter>
    {
        private const string PagesStop = "tutorial-slideshow:pages";
        private const string ControlsStop = "tutorial-slideshow:controls";
        private const string PageKey = "tutorial:page/";

        /// <summary>The one tutorial menu the project container holds for the whole game. Both
        /// tutorial screens read it; the adapter's <c>IsPresent</c> says which shape it has drawn.
        /// </summary>
        private readonly ScreenSource<ITutorialMenu> _source = ScreenSource<ITutorialMenu>.FromProject();

        // The tutorial the panel was last showing. Mod-owned and outliving the menu on purpose: the
        // menu is the one object the project container holds for the whole game, so the adapter is
        // never replaced and there is nothing per-menu to hang this on.
        private ITutorialEntry _lastTutorial;

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override TutorialSlideshowAdapter Adapt(object menu)
        {
            return new TutorialSlideshowAdapter((TutorialMenu)menu);
        }

        public override string Key
        {
            get { return "tutorial-slideshow"; }
        }

        /// <summary>Layer 36: over the page it explains, combat included.</summary>
        public override int Layer
        {
            get { return 36; }
        }

        /// <summary>The tutorial entry's header, which the panel draws over the page.</summary>
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
        /// One tutorial REPLACED BY ANOTHER, with no frame between them:
        /// <c>TutorialManager.ShowTutorialInternal</c> closes and re-opens the menu in a single call,
        /// so the panel never goes inactive, the project container's menu object never changes and
        /// neither the manager's arrival announcement nor the adapter slot's own ever runs.
        ///
        /// The entry the menu is showing is what tells the two apart. A new one says the new header
        /// and gives up the cursor, which is standing on a page row of the tutorial that has gone:
        /// the panel is back on page 1 while the cursor claims page N. The next
        /// <c>EnsureFocus</c> seats it on the start row, which is the page the panel is on.
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

            AddPages(builder);
            AddControls(builder);
        }

        private void AddPages(GraphBuilder builder)
        {
            Component box = Live.DescriptionBox;
            int pages = Live.PageCount;
            if (box == null || pages <= 0)
            {
                return;
            }

            int current = Live.CurrentPage;
            builder.BeginStop(PagesStop);
            for (int i = 0; i < pages; i++)
            {
                ControlId id = ControlId.Structural(PageKey + i);
                builder.AddItem(new DrawnNode(id, Page(i), box));
                if (i == current)
                {
                    builder.SetStart(id);
                }
            }
        }

        private void AddControls(GraphBuilder builder)
        {
            builder.BeginStop(ControlsStop);

            Component toggle = Live.TutorialsToggle;
            if (toggle != null)
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(toggle, "tutorial-slideshow:tutorials"),
                    GraphNodes.Checkbox(
                        () => Live.TutorialsToggleLabel,
                        Live.IsTutorialsChecked,
                        Live.ToggleTutorials),
                    toggle));
            }

            Component close = Live.CloseButton;
            if (close != null && Live.IsCloseVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(close, "tutorial-slideshow:close"),
                    GraphNodes.Button(
                        () => Live.CloseLabel,
                        () => Live.ActivateClose(),
                        Live.IsCloseEnabled),
                    close));
            }
        }

        /// <summary>One page: the panel being turned to it, and then the words it is drawing, one
        /// part per paragraph the page was written in. No role word - a page is not a control the
        /// player works, it is what the tutorial has to say.</summary>
        private NodeVtable Page(int page)
        {
            int it = page;
            return GraphNodes.Paragraphs(() =>
            {
                ShowPage(it);
                return Live.DescriptionLines;
            });
        }

        /// <summary>Turn the panel to <paramref name="page"/>, if it is not there already and if that
        /// page's row is the one the cursor is standing on.</summary>
        private void ShowPage(int page)
        {
            GraphNavigator navigator = Navigator;
            if (navigator == null
                || navigator.FocusedIndex(PageKey) != page
                || Live.CurrentPage == page)
            {
                return;
            }

            Live.ShowPage(page);
        }
    }
}
