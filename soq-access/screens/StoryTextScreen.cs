using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class StoryTextScreen : Screen
    {
        private readonly IStoryTextAdapter _adapter;

        public StoryTextScreen(IStoryTextAdapter adapter)
            : base(BuildRootWidget(adapter))
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

        private static ContainerWidget BuildRootWidget(IStoryTextAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("story-text", string.Empty);

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

        private static string BuildStoryText(IStoryTextAdapter adapter)
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
