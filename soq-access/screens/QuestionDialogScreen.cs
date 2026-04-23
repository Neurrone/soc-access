using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class QuestionDialogScreen : Screen
    {
        private readonly QuestionDialogAdapter _adapter;

        public QuestionDialogScreen(object sourceKey, QuestionDialogAdapter adapter)
            : base(sourceKey, BuildRootWidget(adapter))
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
                return _adapter != null && _adapter.ActivateAction(2);
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(QuestionDialogAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            string dialogLabel = string.IsNullOrWhiteSpace(title) ? "dialog" : title + " dialog";
            ContainerWidget root = new ContainerWidget("question-dialog", dialogLabel);

            root.AddChild(new TextWidget(
                "body",
                () => adapter != null ? adapter.Body : string.Empty,
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(0);
                    }
                },
                includeParentLabelInAnnouncement: true));

            root.AddChild(new ButtonWidget(
                "positive",
                adapter != null ? adapter.PositiveLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(1),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(1);
                    }
                },
                () => adapter != null));

            root.AddChild(new ButtonWidget(
                "negative",
                adapter != null ? adapter.NegativeLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(2),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(2);
                    }
                },
                () => adapter != null));

            return root;
        }
    }
}
