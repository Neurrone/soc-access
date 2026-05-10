using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TroopOverviewScreen : Screen
    {
        private readonly KingdomTroopOverviewAdapter _adapter;

        public TroopOverviewScreen(KingdomTroopOverviewAdapter adapter)
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
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        private static ContainerWidget BuildRoot(KingdomTroopOverviewAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("troop-overview", "Troop overview");
            string title = adapter != null ? adapter.Title : "Troop overview";
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
                root.AddChild(BuildGroupMenu(groups[i]));
            }

            return root;
        }

        private static MenuWidget BuildGroupMenu(KingdomTroopOverviewAdapter.GroupItem group)
        {
            MenuWidget menu = new MenuWidget(group.Id, group.Label);
            IReadOnlyList<KingdomTroopOverviewAdapter.RowItem> rows = group.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                KingdomTroopOverviewAdapter.RowItem row = rows[i];
                menu.AddItem(new MenuItemWidget(
                    row.Id,
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
    }
}
