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

            SoqAccessPlugin.Instance?.LogInfo(
                "QuestionDialogScreen built widget tree: title=\""
                + (_adapter != null ? _adapter.Title : string.Empty)
                + "\", positive=\""
                + (_adapter != null ? _adapter.PositiveLabel : string.Empty)
                + "\", negative=\""
                + (_adapter != null ? _adapter.NegativeLabel : string.Empty)
                + "\", body=\""
                + Truncate(_adapter != null ? _adapter.Body : string.Empty)
                + "\"");
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
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

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 120)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, 120) + "...";
        }
    }
}
