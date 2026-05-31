using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CommunityMapsHomeScreen : Screen
    {
        private readonly CommunityMapsHomeAdapter _adapter;

        public CommunityMapsHomeScreen(CommunityMapsHomeAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsHomeAdapter adapter = CommunityMapsHomeAdapter.TryCreate();
            return adapter != null
                && adapter.IsPresent()
                && adapter.IsBrowseSelected
                    ? new CommunityMapsHomeScreen(adapter)
                    : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

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

        private static ContainerWidget BuildRoot(CommunityMapsHomeAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-home", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTabs(adapter));
            root.AddChild(new ButtonWidget(
                "community-maps-search-filter",
                () => adapter.SearchFilterLabel,
                adapter.OpenSearchFilter,
                null,
                () => adapter.HasSearchFilter,
                () => adapter.HasSearchFilter));
            root.AddChild(new ButtonWidget(
                "community-maps-downloads",
                ModText.Get(ModStrings.Screens.Downloads),
                adapter.OpenDownloadsMenu,
                null,
                () => adapter.HasDownloadsMenu,
                () => adapter.HasDownloadsMenu));
            root.AddChild(BuildFeaturedMenu(adapter));
            root.AddChild(new ButtonWidget(
                "community-maps-featured-subscribe",
                (System.Func<string>)(() => adapter.FeaturedSubscribeLabel),
                adapter.SubscribeFeatured,
                () => adapter.FocusFeatured(),
                () => adapter.HasFeatured,
                () => adapter.HasFeatured));
            root.AddChild(new ButtonWidget(
                "community-maps-featured-options",
                (System.Func<string>)(() => adapter.MoreOptionsLabel),
                adapter.OpenFeaturedOptions,
                () => adapter.FocusFeatured(),
                () => adapter.HasFeatured,
                () => adapter.HasFeatured));

            IReadOnlyList<CommunityMapsHomeAdapter.RowItem> rows = adapter.GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                CommunityMapsHomeAdapter.RowItem row = rows[i];
                root.AddChild(BuildRowMenu(adapter, row));
                AddRowActionButtons(root, adapter, row);
            }
            root.AddChild(new ButtonWidget(
                "community-maps-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                null,
                () => true));

            return root;
        }

        private static MenuWidget BuildTabs(CommunityMapsHomeAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("community-maps-tabs", ModText.Get(ModStrings.Screens.Tabs));
            IReadOnlyList<CommunityMapsHomeAdapter.TabItem> tabs = adapter.GetTabs();
            string selectedId = null;
            for (int i = 0; i < tabs.Count; i++)
            {
                CommunityMapsHomeAdapter.TabItem tab = tabs[i];
                CommunityMapsHomeAdapter.TabItem captured = tab;
                if (captured.IsSelected)
                {
                    selectedId = "community-maps-tab-" + captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    "community-maps-tab-" + captured.Id,
                    () => captured.Label,
                    () => captured.IsSelected ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    captured.Select,
                    () => captured.Select(),
                    () => true));
            }

            menu.SetFocusedItemById(selectedId);
            return menu;
        }

        private static MenuWidget BuildFeaturedMenu(CommunityMapsHomeAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "community-maps-featured",
                adapter.FeaturedLabel,
                () => adapter.HasFeatured,
                () => adapter.FocusFeatured(),
                null);

            IReadOnlyList<CommunityMapsHomeAdapter.FeaturedItem> items = adapter.GetFeaturedItems();
            for (int i = 0; i < items.Count; i++)
            {
                CommunityMapsHomeAdapter.FeaturedItem item = items[i];
                CommunityMapsHomeAdapter.FeaturedItem captured = item;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-featured-item-" + captured.Index,
                    () => captured.Label,
                    null,
                    () => adapter.ActivateFeaturedItem(captured),
                    () => adapter.FocusFeaturedItem(captured),
                    () => true));
            }

            menu.SetFocusByIndexSilently(adapter.FeaturedIndex);

            return menu;
        }

        private static MenuWidget BuildRowMenu(CommunityMapsHomeAdapter adapter, CommunityMapsHomeAdapter.RowItem row)
        {
            string label = string.IsNullOrWhiteSpace(row.Label)
                ? ModText.Get(ModStrings.Screens.Group, (row.Index + 1).ToString())
                : row.Label;
            MenuWidget menu = new MenuWidget(
                "community-maps-row-" + row.Index,
                label,
                () => row.Items != null && row.Items.Count > 0);

            for (int i = 0; i < row.Items.Count; i++)
            {
                CommunityMapsHomeAdapter.ModItem item = row.Items[i];
                CommunityMapsHomeAdapter.ModItem captured = item;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-row-" + captured.RowIndex + "-item-" + captured.Index,
                    () => captured.Label,
                    () => captured.Status,
                    () => adapter.ActivateItem(captured),
                    () => adapter.FocusItem(captured),
                    () => true));
            }

            return menu;
        }

        private static void AddRowActionButtons(
            ContainerWidget root,
            CommunityMapsHomeAdapter adapter,
            CommunityMapsHomeAdapter.RowItem row)
        {
            if (root == null || adapter == null || row == null)
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                "community-maps-row-" + row.Index + "-subscribe",
                () => adapter.SelectedSubscribeLabel,
                adapter.SubscribeSelectedItem,
                null,
                () => adapter.IsSelectedItemInRow(row.Index),
                () => adapter.IsSelectedItemInRow(row.Index)));
            root.AddChild(new ButtonWidget(
                "community-maps-row-" + row.Index + "-options",
                () => adapter.MoreOptionsLabel,
                adapter.OpenSelectedItemOptions,
                null,
                () => adapter.IsSelectedItemInRow(row.Index),
                () => adapter.IsSelectedItemInRow(row.Index)));
        }
    }
}
