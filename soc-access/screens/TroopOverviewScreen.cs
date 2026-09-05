using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class TroopOverviewScreen : Screen
    {
        private readonly KingdomTroopOverviewAdapter _adapter;

        public TroopOverviewScreen(KingdomTroopOverviewAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            KingdomTroopOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomTroopOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomTroopOverviewAdapter adapter = new KingdomTroopOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new TroopOverviewScreen(adapter);
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
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        private static ContainerWidget BuildRoot(KingdomTroopOverviewAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("troop-overview", adapter != null ? adapter.Title : string.Empty);
            string title = adapter != null ? adapter.Title : string.Empty;
            root.AddChild(new TextWidget(
                "troop-overview-title",
                () => title,
                adapter != null ? adapter.HideNativeTooltip : (System.Action)null,
                includeParentLabelInAnnouncement: false));

            if (adapter == null)
            {
                return root;
            }

            IReadOnlyList<KingdomTroopOverviewAdapter.GroupItem> groups = adapter.GetGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                root.AddChild(BuildGroupMenu(groups[i], i));
            }

            return root;
        }

        private static MenuWidget BuildGroupMenu(KingdomTroopOverviewAdapter.GroupItem group, int groupIndex)
        {
            string groupId = "troop-overview-town-" + groupIndex;
            MenuWidget menu = new MenuWidget(groupId, BuildGroupLabel(group, groupIndex));
            IReadOnlyList<KingdomTroopOverviewAdapter.RowItem> rows = group.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                KingdomTroopOverviewAdapter.RowItem row = rows[i];
                menu.AddItem(new MenuItemWidget(
                    groupId + "-row-" + i,
                    () => row.Label,
                    () => row.Status,
                    row.Activate,
                    () =>
                    {
                        if (row.Focus != null)
                        {
                            row.Focus();
                        }
                    },
                    () => true));
            }

            return menu;
        }

        private static string BuildGroupLabel(KingdomTroopOverviewAdapter.GroupItem group, int groupIndex)
        {
            if (group != null && !string.IsNullOrWhiteSpace(group.Label))
            {
                return group.Label;
            }

            return ModText.Get(ModStrings.Screens.Group, groupIndex + 1);
        }
    }
}
