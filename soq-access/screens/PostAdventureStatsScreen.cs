using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostAdventureStatsScreen : Screen
    {
        private readonly PostAdventureStatsAdapter _adapter;

        public PostAdventureStatsScreen(PostAdventureStatsAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(PostAdventureStatsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("post-adventure-stats", "Post adventure stats");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "post-adventure-stats-header",
                () => adapter.Header,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "post-adventure-stats-playtime",
                () => adapter.TotalPlayTime,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.TotalPlayTime)));

            root.AddChild(new TextWidget(
                "post-adventure-stats-rounds",
                () => adapter.TotalRounds,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.TotalRounds)));

            root.AddChild(BuildGraphTypeMenu(adapter));
            root.AddChild(BuildTeamsMenu(adapter));

            root.AddChild(new TextWidget(
                "post-adventure-stats-graph-todo",
                () => "TODO: implement accessibility for graph",
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "post-adventure-stats-close",
                () => adapter.GetCloseButtonLabel(),
                adapter.Close,
                adapter.HideNativeTooltip,
                adapter.IsCloseButtonEnabled));

            return root;
        }

        private static MenuWidget BuildGraphTypeMenu(PostAdventureStatsAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("post-adventure-stats-graph-types", "Graph type");
            IReadOnlyList<PostAdventureStatsAdapter.GraphOption> options = adapter.GetGraphOptions();
            for (int i = 0; i < options.Count; i++)
            {
                PostAdventureStatsAdapter.GraphOption option = options[i];
                menu.AddItem(new MenuItemWidget(
                    option.Id,
                    () => option.Label,
                    () => adapter.SelectedGraphIndex == option.Index ? "selected" : string.Empty,
                    () => adapter.SelectGraph(option.Index),
                    () => adapter.FocusGraphDropdown(),
                    () => true));
            }

            menu.SetFocusedItemById("post-adventure-stats-graph-" + adapter.SelectedGraphIndex);
            return menu;
        }

        private static MenuWidget BuildTeamsMenu(PostAdventureStatsAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "post-adventure-stats-teams",
                "Teams",
                () => adapter.GetTeamOptions().Count > 0);

            IReadOnlyList<PostAdventureStatsAdapter.TeamOption> teams = adapter.GetTeamOptions();
            for (int i = 0; i < teams.Count; i++)
            {
                PostAdventureStatsAdapter.TeamOption team = teams[i];
                menu.AddItem(new MenuItemWidget(
                    team.Id,
                    () => team.Label,
                    () => adapter.IsTeamSelected(team.Entry) ? "selected" : string.Empty,
                    () => adapter.ToggleTeam(team.Entry),
                    () => adapter.FocusTeam(team.Entry),
                    () => team.Entry != null && team.Entry.gameObject != null && team.Entry.gameObject.activeInHierarchy));
            }

            return menu;
        }
    }
}
