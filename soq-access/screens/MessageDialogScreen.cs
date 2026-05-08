using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MessageDialogScreen : Screen
    {
        private readonly IMessageDialogAdapter _adapter;

        public MessageDialogScreen(IMessageDialogAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public object SourceKey
        {
            get { return _adapter != null ? _adapter.SourceKey : null; }
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.HasNegativeAction && _adapter.ActivateAction(DialogAction.Negative);
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(IMessageDialogAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            string dialogLabel = string.IsNullOrWhiteSpace(title) ? "dialog" : title + " dialog";
            ContainerWidget root = new ContainerWidget("message-dialog", dialogLabel);

            root.AddChild(new TextWidget(
                "body",
                () => adapter != null ? adapter.Body : string.Empty,
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Body);
                    }
                },
                includeParentLabelInAnnouncement: true));

            root.AddChild(new ButtonWidget(
                "positive",
                adapter != null ? adapter.PositiveLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(DialogAction.Positive),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Positive);
                    }
                },
                () => adapter != null && adapter.HasPositiveAction,
                () => adapter != null && adapter.HasPositiveAction));

            root.AddChild(new ButtonWidget(
                "negative",
                adapter != null ? adapter.NegativeLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(DialogAction.Negative),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Negative);
                    }
                },
                () => adapter != null && adapter.HasNegativeAction,
                () => adapter != null && adapter.HasNegativeAction));

            return root;
        }
    }
}
