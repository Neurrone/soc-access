using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostAdventureResultScreen : Screen
    {
        private readonly PostAdventureResultAdapter _adapter;

        public PostAdventureResultScreen(PostAdventureResultAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PostAdventureMenu[] menus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PostAdventureResultAdapter adapter = new PostAdventureResultAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new PostAdventureResultScreen(adapter);
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

        private static ContainerWidget BuildRoot(PostAdventureResultAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("post-adventure-result", "Post adventure result");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "post-adventure-result-title",
                () => adapter.ResultTitle,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "post-adventure-description",
                () => adapter.Description,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.DescriptionVisible));

            root.AddChild(BuildObjectivesMenu(adapter));
            AddButton(root, "post-adventure-stats", adapter, () => adapter.StatsButton);
            AddButton(root, "post-adventure-continue", adapter, () => adapter.ContinueCampaignButton);
            AddButton(root, "post-adventure-restart", adapter, () => adapter.RestartMapButton);
            AddButton(root, "post-adventure-load", adapter, () => adapter.LoadButton);
            AddButton(root, "post-adventure-quit-to-main", adapter, () => adapter.QuitToMainButton);
            AddButton(root, "post-adventure-player-stats", adapter, () => adapter.PlayerStatsButton);
            return root;
        }

        private static MenuWidget BuildObjectivesMenu(PostAdventureResultAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "post-adventure-objectives",
                "Objectives",
                () => adapter.GetObjectives().Count > 0);

            IReadOnlyList<PostAdventureResultAdapter.ObjectiveEntry> objectives = adapter.GetObjectives();
            for (int i = 0; i < objectives.Count; i++)
            {
                PostAdventureResultAdapter.ObjectiveEntry objective = objectives[i];
                menu.AddItem(new MenuItemWidget(
                    "post-adventure-objective-" + i,
                    () => objective.Label,
                    () => objective.Status,
                    activate: null,
                    onFocus: adapter.HideNativeTooltip,
                    isVisible: () => objective.IsVisible));
            }

            return menu;
        }

        private static void AddButton(ContainerWidget root, string id, PostAdventureResultAdapter adapter, System.Func<UIButton> getButton)
        {
            root.AddChild(new ButtonWidget(
                id,
                () => adapter.GetButtonLabel(getButton()),
                () => adapter.ActivateButton(getButton()),
                adapter.HideNativeTooltip,
                () => adapter.IsButtonEnabled(getButton()),
                () => adapter.IsButtonVisible(getButton())));
        }
    }
}
