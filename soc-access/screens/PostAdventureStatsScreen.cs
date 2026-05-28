using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostAdventureStatsScreen : Screen
    {
        private static readonly FieldInfo StatsMenuField = AccessTools.Field(typeof(PostAdventureMenu), "_statsMenu");

        private readonly PostAdventureStatsAdapter _adapter;

        public PostAdventureStatsScreen(PostAdventureStatsAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PostAdventureMenu[] resultMenus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < resultMenus.Length; i++)
            {
                PostAdventureStatsMenu statsMenu = GetStatsMenu(resultMenus[i]);
                PostAdventureStatsAdapter adapter = new PostAdventureStatsAdapter(statsMenu);
                if (adapter.IsPresent())
                {
                    return new PostAdventureStatsScreen(adapter);
                }
            }

            return null;
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

        private static PostAdventureStatsMenu GetStatsMenu(PostAdventureMenu resultMenu)
        {
            return resultMenu != null && StatsMenuField != null
                ? StatsMenuField.GetValue(resultMenu) as PostAdventureStatsMenu
                : null;
        }

        private static ContainerWidget BuildRoot(PostAdventureStatsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("post-adventure-stats", ModText.Get(ModStrings.Screens.PostAdventureStats));
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
            root.AddChild(BuildGraphTable(adapter));

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
            MenuWidget menu = new MenuWidget("post-adventure-stats-graph-types", ModText.Get(ModStrings.Screens.GraphType));
            IReadOnlyList<PostAdventureStatsAdapter.GraphOption> options = adapter.GetGraphOptions();
            for (int i = 0; i < options.Count; i++)
            {
                PostAdventureStatsAdapter.GraphOption option = options[i];
                menu.AddItem(new MenuItemWidget(
                    option.Id,
                    () => option.Label,
                    () => adapter.SelectedGraphIndex == option.Index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () =>
                    {
                        if (!adapter.SelectGraph(option.Index))
                        {
                            return false;
                        }

                        SocAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureStatsChanged();
                        return true;
                    },
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
                ModText.Get(ModStrings.Screens.Teams),
                () => adapter.GetTeamOptions().Count > 0);

            IReadOnlyList<PostAdventureStatsAdapter.TeamOption> teams = adapter.GetTeamOptions();
            for (int i = 0; i < teams.Count; i++)
            {
                PostAdventureStatsAdapter.TeamOption team = teams[i];
                menu.AddItem(new MenuItemWidget(
                    team.Id,
                    () => team.Label,
                    () => adapter.IsTeamSelected(team.Entry) ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () =>
                    {
                        if (!adapter.ToggleTeam(team.Entry))
                        {
                            return false;
                        }

                        SocAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureStatsChanged();
                        return true;
                    },
                    () => adapter.FocusTeam(team.Entry),
                    () => team.Entry != null && team.Entry.gameObject != null && team.Entry.gameObject.activeInHierarchy));
            }

            return menu;
        }

        private static TableWidget BuildGraphTable(PostAdventureStatsAdapter adapter)
        {
            return new TableWidget(
                "post-adventure-stats-graph-table",
                adapter != null ? adapter.GraphTitle : string.Empty,
                BuildGraphColumns(adapter),
                BuildGraphRows(adapter));
        }

        private static IEnumerable<TableWidget.Column> BuildGraphColumns(PostAdventureStatsAdapter adapter)
        {
            yield return new TableWidget.Column("round", ModText.Get(ModStrings.UI.ColumnRound), null, null);
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams = adapter != null
                ? adapter.GetEnabledGraphTeams()
                : new PostAdventureStatsAdapter.GraphTeamColumn[0];
            for (int i = 0; i < teams.Count; i++)
            {
                PostAdventureStatsAdapter.GraphTeamColumn team = teams[i];
                yield return new TableWidget.Column(team.Id, team.Label, null, null);
            }
        }

        private static IReadOnlyList<TableWidget.Row> BuildGraphRows(PostAdventureStatsAdapter adapter)
        {
            List<TableWidget.Row> rows = new List<TableWidget.Row>();
            if (adapter == null)
            {
                return rows;
            }

            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams = adapter.GetEnabledGraphTeams();
            IReadOnlyList<PostAdventureStatsAdapter.GraphRoundRow> graphRows = adapter.GetGraphRows();
            for (int i = 0; i < graphRows.Count; i++)
            {
                PostAdventureStatsAdapter.GraphRoundRow graphRow = graphRows[i];
                rows.Add(new TableWidget.Row(
                    graphRow.Id,
                    graphRow.Round.ToString(),
                    columnId => GetGraphCellValue(graphRow, teams, columnId),
                    null,
                    adapter.HideNativeTooltip,
                    null));
            }

            return rows;
        }

        private static string GetGraphCellValue(
            PostAdventureStatsAdapter.GraphRoundRow row,
            IReadOnlyList<PostAdventureStatsAdapter.GraphTeamColumn> teams,
            string columnId)
        {
            if (row == null)
            {
                return string.Empty;
            }

            if (columnId == "round")
            {
                return row.Round.ToString();
            }

            if (teams != null)
            {
                for (int i = 0; i < teams.Count; i++)
                {
                    PostAdventureStatsAdapter.GraphTeamColumn team = teams[i];
                    if (team != null && team.Id == columnId)
                    {
                        return row.GetValue(team.TeamId);
                    }
                }
            }

            return string.Empty;
        }
    }
}
