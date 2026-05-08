using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class WorldConfirmMenuScreen : Screen
    {
        private readonly WorldConfirmMenuAdapter _adapter;

        public WorldConfirmMenuScreen(WorldConfirmMenuAdapter adapter)
            : base(BuildRoot(adapter))
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
                return _adapter != null && _adapter.ActivateCancel();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(WorldConfirmMenuAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            ContainerWidget root = new ContainerWidget(
                "world-confirm-menu",
                string.IsNullOrWhiteSpace(title) ? "World confirmation menu" : title);

            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "world-confirm-title",
                () => adapter.Title,
                adapter.ClearNativeSelection,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "world-confirm-body",
                () => adapter.Body,
                adapter.ClearNativeSelection,
                includeParentLabelInAnnouncement: false));

            AddCosts(root, adapter);

            root.AddChild(new ButtonWidget(
                "world-confirm-confirm",
                () => adapter.ConfirmLabel,
                adapter.ActivateConfirm,
                adapter.ClearNativeSelection,
                adapter.IsConfirmEnabled));

            root.AddChild(new ButtonWidget(
                "world-confirm-cancel",
                () => adapter.CancelLabel,
                adapter.ActivateCancel,
                adapter.ClearNativeSelection,
                () => true));

            return root;
        }

        private static void AddCosts(ContainerWidget root, WorldConfirmMenuAdapter adapter)
        {
            IReadOnlyList<string> costs = adapter.GetCostLabels();
            for (int i = 0; i < costs.Count; i++)
            {
                int capturedIndex = i;
                root.AddChild(new TextWidget(
                    "world-confirm-cost-" + i,
                    () =>
                    {
                        IReadOnlyList<string> latestCosts = adapter.GetCostLabels();
                        return capturedIndex >= 0 && capturedIndex < latestCosts.Count
                            ? latestCosts[capturedIndex]
                            : string.Empty;
                    },
                    adapter.ClearNativeSelection,
                    includeParentLabelInAnnouncement: false));
            }
        }
    }
}
