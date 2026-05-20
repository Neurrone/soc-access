using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class OwnedEntitiesScreen : Screen
    {
        private readonly KingdomEntityOverviewAdapter _adapter;

        public OwnedEntitiesScreen(KingdomEntityOverviewAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            KingdomEntityOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomEntityOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomEntityOverviewAdapter adapter = new KingdomEntityOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new OwnedEntitiesScreen(adapter);
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

        private static ContainerWidget BuildRoot(KingdomEntityOverviewAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("owned-entities", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            string title = adapter.Title;
            root.AddChild(new TextWidget(
                "owned-entities-title",
                () => title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(title)));

            IReadOnlyList<KingdomEntityOverviewAdapter.GroupItem> groups = adapter.GetGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                root.AddChild(BuildGroupMenu(groups[i], i));
            }

            return root;
        }

        private static MenuWidget BuildGroupMenu(KingdomEntityOverviewAdapter.GroupItem group, int groupIndex)
        {
            string groupId = "owned-entities-group-" + groupIndex;
            MenuWidget menu = new MenuWidget(groupId, BuildGroupLabel(group, groupIndex));
            IReadOnlyList<KingdomEntityOverviewAdapter.RowItem> rows = group.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                KingdomEntityOverviewAdapter.RowItem row = rows[i];
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

        private static string BuildGroupLabel(KingdomEntityOverviewAdapter.GroupItem group, int groupIndex)
        {
            if (group != null && !string.IsNullOrWhiteSpace(group.Label))
            {
                return group.Label;
            }

            return ModText.Get(ModStrings.Screens.Group, groupIndex + 1);
        }
    }
}
