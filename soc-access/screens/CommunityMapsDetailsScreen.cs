using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class CommunityMapsDetailsScreen : Screen
    {
        private readonly CommunityMapsDetailsAdapter _adapter;

        public CommunityMapsDetailsScreen(CommunityMapsDetailsAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsDetailsAdapter adapter = CommunityMapsDetailsAdapter.TryCreate();
            return adapter != null && adapter.IsPresent() ? new CommunityMapsDetailsScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private static ContainerWidget BuildRoot(CommunityMapsDetailsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-details", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "community-maps-details-title",
                () => adapter.Title,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Title)));

            root.AddChild(new ButtonWidget(
                "community-maps-details-subscribe",
                () => adapter.SubscribeLabel,
                adapter.Subscribe,
                null,
                () => !string.IsNullOrWhiteSpace(adapter.SubscribeLabel),
                () => !string.IsNullOrWhiteSpace(adapter.SubscribeLabel)));
            root.AddChild(new ButtonWidget(
                "community-maps-details-downloads",
                ModText.Get(ModStrings.Screens.Downloads),
                adapter.OpenDownloadsMenu,
                null,
                () => adapter.HasDownloadsMenu,
                () => adapter.HasDownloadsMenu));

            root.AddChild(BuildActionsMenu(adapter));
            root.AddChild(new ButtonWidget(
                "community-maps-details-report",
                () => adapter.ReportLabel,
                adapter.Report,
                null,
                () => !string.IsNullOrWhiteSpace(adapter.ReportLabel),
                () => !string.IsNullOrWhiteSpace(adapter.ReportLabel)));
            root.AddChild(BuildDetailsMenu(adapter));
            root.AddChild(BuildTagsMenu(adapter));

            root.AddChild(new TextWidget(
                "community-maps-details-summary",
                () => adapter.Summary,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Summary)));

            root.AddChild(new TextWidget(
                "community-maps-details-description",
                () => BuildLabelValue(adapter.DescriptionLabel, adapter.Description),
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Description)));

            root.AddChild(new ButtonWidget(
                "community-maps-details-back",
                () => adapter.BackLabel,
                adapter.Close,
                null,
                () => true,
                () => !string.IsNullOrWhiteSpace(adapter.BackLabel)));

            return root;
        }

        private static MenuWidget BuildActionsMenu(CommunityMapsDetailsAdapter adapter)
        {
            IReadOnlyList<CommunityMapsDetailsAdapter.ActionItem> actions = adapter.GetVoteActions();
            MenuWidget menu = new MenuWidget(
                "community-maps-details-actions",
                ModText.Get(ModStrings.Screens.Options),
                () => actions.Count > 0);
            for (int i = 0; i < actions.Count; i++)
            {
                CommunityMapsDetailsAdapter.ActionItem action = actions[i];
                CommunityMapsDetailsAdapter.ActionItem captured = action;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-details-action-" + captured.Id,
                    () => captured.Label,
                    () => BuildVoteStatus(captured),
                    () => ActivateVoteAction(menu, captured),
                    null,
                    () => true));
            }

            return menu;
        }

        private static string BuildLabelValue(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return value ?? string.Empty;
            }

            return label + "\n" + (value ?? string.Empty);
        }

        private static string BuildVoteStatus(CommunityMapsDetailsAdapter.ActionItem action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            string count = action.Status;
            if (!action.IsSelected)
            {
                return count;
            }

            string selected = ModText.Get(ModStrings.UI.Selected);
            return string.IsNullOrWhiteSpace(count)
                ? selected
                : ModText.Get(ModStrings.Common.ListSeparator, selected, count);
        }

        private static bool ActivateVoteAction(MenuWidget menu, CommunityMapsDetailsAdapter.ActionItem action)
        {
            if (action == null || action.Activate == null)
            {
                return false;
            }

            bool handled = action.Activate();
            if (handled)
            {
                UIManager.RequestFocus(menu);
            }

            return handled;
        }

        private static MenuWidget BuildDetailsMenu(CommunityMapsDetailsAdapter adapter)
        {
            IReadOnlyList<CommunityMapsDetailsAdapter.DetailItem> details = adapter.GetDetails();
            MenuWidget menu = new MenuWidget(
                "community-maps-details-facts",
                ModText.Get(ModStrings.UI.ColumnDetails),
                () => details.Count > 0);
            for (int i = 0; i < details.Count; i++)
            {
                CommunityMapsDetailsAdapter.DetailItem detail = details[i];
                CommunityMapsDetailsAdapter.DetailItem captured = detail;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-details-fact-" + captured.Id,
                    () => captured.Label,
                    () => captured.Value,
                    null,
                    null,
                    () => true));
            }

            return menu;
        }

        private static MenuWidget BuildTagsMenu(CommunityMapsDetailsAdapter adapter)
        {
            IReadOnlyList<CommunityMapsDetailsAdapter.TagItem> tags = adapter.GetTags();
            MenuWidget menu = new MenuWidget(
                "community-maps-details-tags",
                ModText.Get(ModStrings.Screens.Categories),
                () => tags.Count > 0);
            for (int i = 0; i < tags.Count; i++)
            {
                CommunityMapsDetailsAdapter.TagItem tag = tags[i];
                CommunityMapsDetailsAdapter.TagItem captured = tag;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-details-tag-" + captured.Index,
                    () => captured.Label,
                    null,
                    null,
                    null,
                    () => true));
            }

            return menu;
        }
    }
}
