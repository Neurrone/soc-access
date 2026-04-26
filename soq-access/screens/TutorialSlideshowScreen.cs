using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TutorialSlideshowScreen : Screen
    {
        private readonly TutorialSlideshowAdapter _adapter;

        public TutorialSlideshowScreen(TutorialSlideshowAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (_adapter != null && _adapter.IsCloseAvailable())
                {
                    _adapter.ActivateClose();
                }

                return true;
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(TutorialSlideshowAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("tutorial-slideshow-screen", "Tutorial");
            root.AddChild(new TextWidget(
                "tutorial-slideshow-header",
                () => adapter != null ? adapter.Header : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new TextWidget(
                "tutorial-slideshow-description",
                () => adapter != null ? adapter.Description : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-previous",
                "Previous",
                () => adapter != null && adapter.ActivatePrevious(),
                null,
                () => adapter != null && adapter.IsPreviousAvailable(),
                () => adapter != null && adapter.IsPreviousAvailable()));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-next",
                "Next",
                () => adapter != null && adapter.ActivateNext(),
                null,
                () => adapter != null && adapter.IsNextAvailable(),
                () => adapter != null && adapter.IsNextAvailable()));
            root.AddChild(new CheckboxWidget(
                "tutorial-slideshow-tutorials-toggle",
                adapter != null ? adapter.TutorialsToggleLabel : "Show tutorials",
                () => { if (adapter != null) adapter.ToggleTutorials(); },
                () => adapter != null && adapter.IsTutorialsChecked(),
                () => adapter != null));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-close",
                adapter != null ? adapter.CloseLabel : "Close",
                () => adapter != null && adapter.ActivateClose(),
                null,
                () => adapter != null && adapter.IsCloseAvailable(),
                () => adapter != null && adapter.IsCloseAvailable()));
            return root;
        }
    }
}
