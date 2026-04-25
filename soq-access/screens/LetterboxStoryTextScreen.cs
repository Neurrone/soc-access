using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LetterboxStoryTextScreen : Screen
    {
        private readonly LetterboxStoryTextAdapter _adapter;

        public LetterboxStoryTextScreen(LetterboxStoryTextAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRootWidget(adapter))
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
                return _adapter != null && _adapter.AdvanceNow();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(LetterboxStoryTextAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("letterbox-story-text", string.Empty);

            root.AddChild(new TextWidget(
                "story-text",
                () => BuildStoryText(adapter),
                null,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "next",
                "Next",
                () => adapter != null && adapter.AdvanceNow(),
                null,
                () => adapter != null && adapter.IsPresent()));

            return root;
        }

        private static string BuildStoryText(LetterboxStoryTextAdapter adapter)
        {
            if (adapter == null)
            {
                return string.Empty;
            }

            string title = adapter.Title;
            string body = adapter.Body;
            if (string.IsNullOrWhiteSpace(title))
            {
                return body;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return title;
            }

            return title + "\n" + body;
        }
    }
}
