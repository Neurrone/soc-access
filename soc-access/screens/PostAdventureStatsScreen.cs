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

            root.AddChild(new TextWidget(
                "post-adventure-stats-graph-todo",
                () => ModText.Get(ModStrings.Screens.GraphAccessibilityTodo),
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
            MenuWidget menu = new MenuWidget("post-adventure-stats-graph-types", ModText.Get(ModStrings.Screens.GraphType));
            IReadOnlyList<PostAdventureStatsAdapter.GraphOption> options = adapter.GetGraphOptions();
            for (int i = 0; i < options.Count; i++)
            {
                PostAdventureStatsAdapter.GraphOption option = options[i];
                menu.AddItem(new MenuItemWidget(
                    option.Id,
                    () => option.Label,
                    () => adapter.SelectedGraphIndex == option.Index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
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
                    () => adapter.ToggleTeam(team.Entry),
                    () => adapter.FocusTeam(team.Entry),
                    () => team.Entry != null && team.Entry.gameObject != null && team.Entry.gameObject.activeInHierarchy));
            }

            return menu;
        }
    }
}
