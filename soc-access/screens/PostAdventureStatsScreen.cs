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
    public sealed class PostAdventureStatsScreen : Screen
    {
        private static readonly FieldInfo StatsMenuField = AccessTools.Field(typeof(PostAdventureMenu), "_statsMenu");

        private readonly PostAdventureStatsAdapter _adapter;

        public PostAdventureStatsScreen(PostAdventureStatsAdapter adapter)
            : base(BuildRoot(adapter, null))
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

        public void Refresh(bool announceFocus = false)
        {
            if (!IsPresent())
            {
                return;
            }

            FocusState focusState = CaptureFocusState();
            RootWidget = BuildRoot(_adapter, focusState);
            if (announceFocus)
            {
                UIManager.RequestFocus(RootWidget);
            }
            else
            {
                UIManager.RequestFocusSilently(RootWidget);
            }
        }

        private FocusState CaptureFocusState()
        {
            Widget focusedChild = RootWidget != null ? RootWidget.FocusedChild : null;
            MenuWidget menu = focusedChild as MenuWidget;
            MenuItemWidget menuItem = menu != null ? menu.FocusedItem : null;
            TableWidget table = focusedChild as TableWidget;
            return new FocusState(
                focusedChild != null ? focusedChild.Id : null,
                menuItem != null ? menuItem.Id : null,
                table != null,
                table != null ? table.FocusedRowIndex : 0,
                table != null ? table.FocusedColumnIndex : 0);
        }

        private static PostAdventureStatsMenu GetStatsMenu(PostAdventureMenu resultMenu)
        {
            return resultMenu != null && StatsMenuField != null
                ? StatsMenuField.GetValue(resultMenu) as PostAdventureStatsMenu
                : null;
        }

        private static ContainerWidget BuildRoot(PostAdventureStatsAdapter adapter, FocusState focusState)
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

            root.AddChild(BuildGraphTypeMenu(adapter, focusState));
            root.AddChild(BuildTeamsMenu(adapter, focusState));
            root.AddChild(BuildGraphTable(adapter, focusState));

            root.AddChild(new ButtonWidget(
                "post-adventure-stats-close",
                () => adapter.GetCloseButtonLabel(),
                adapter.Close,
                adapter.HideNativeTooltip,
                adapter.IsCloseButtonEnabled));

            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                root.SetFocusedChildById(focusState.RootChildId);
            }

            return root;
        }

        private static MenuWidget BuildGraphTypeMenu(PostAdventureStatsAdapter adapter, FocusState focusState)
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

                        SocAccessMod.Instance?.ScreenDetector?.OnPostAdventureStatsChanged();
                        return true;
                    },
                    () =>
                    {
                        adapter.FocusGraphDropdown();
                        if (adapter.SelectedGraphIndex != option.Index && adapter.SelectGraph(option.Index))
                        {
                            SocAccessMod.Instance?.ScreenDetector?.OnPostAdventureStatsChanged();
                        }
                    },
                    () => true));
            }

            if (focusState != null
                && focusState.RootChildId == menu.Id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }
            else
            {
                menu.SetFocusedItemById("post-adventure-stats-graph-" + adapter.SelectedGraphIndex);
            }

            return menu;
        }

        private static MenuWidget BuildTeamsMenu(PostAdventureStatsAdapter adapter, FocusState focusState)
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
                    () => adapter.IsTeamSelected(team.Entry)
                        ? ModText.Get(ModStrings.UI.StatusChecked)
                        : ModText.Get(ModStrings.UI.StatusUnchecked),
                    () =>
                    {
                        if (!adapter.ToggleTeam(team.Entry))
                        {
                            return false;
                        }

                        SocAccessMod.Instance?.ScreenDetector?.OnPostAdventureStatsChanged(announceFocus: true);
                        return true;
                    },
                    () => adapter.FocusTeam(team.Entry),
                    () => team.Entry != null && team.Entry.gameObject != null && team.Entry.gameObject.activeInHierarchy));
            }

            if (focusState != null
                && focusState.RootChildId == menu.Id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }

            return menu;
        }

        private static TableWidget BuildGraphTable(PostAdventureStatsAdapter adapter, FocusState focusState)
        {
            TableWidget table = new TableWidget(
                "post-adventure-stats-graph-table",
                adapter != null ? adapter.GraphTitle : string.Empty,
                BuildGraphColumns(adapter),
                BuildGraphRows(adapter));

            if (focusState != null && focusState.RootChildId == table.Id && focusState.IsTable)
            {
                table.SetFocusedCell(focusState.TableRowIndex, focusState.TableColumnIndex);
            }

            return table;
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

        private sealed class FocusState
        {
            public FocusState(
                string rootChildId,
                string menuItemId,
                bool isTable,
                int tableRowIndex,
                int tableColumnIndex)
            {
                RootChildId = rootChildId;
                MenuItemId = menuItemId;
                IsTable = isTable;
                TableRowIndex = tableRowIndex;
                TableColumnIndex = tableColumnIndex;
            }

            public string RootChildId { get; private set; }

            public string MenuItemId { get; private set; }

            public bool IsTable { get; private set; }

            public int TableRowIndex { get; private set; }

            public int TableColumnIndex { get; private set; }
        }
    }
}
