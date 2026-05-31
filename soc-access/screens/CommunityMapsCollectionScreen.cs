using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CommunityMapsCollectionScreen : Screen
    {
        private const string SearchInputId = "community-maps-collection-keyword";
        private const int ItemsMenuIndex = 7;

        private readonly CommunityMapsCollectionAdapter _adapter;
        private bool _refreshAfterSearchInput;

        public CommunityMapsCollectionScreen(CommunityMapsCollectionAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsCollectionAdapter adapter = CommunityMapsCollectionAdapter.TryCreate();
            return adapter != null && adapter.IsPresent()
                ? new CommunityMapsCollectionScreen(adapter)
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

        public override void Update()
        {
            base.Update();
            if (_refreshAfterSearchInput && !IsSearchInputFocused())
            {
                Refresh();
            }
        }

        public bool IsSearchInputFocused()
        {
            Widget focused = UIManager.CurrentWidget;
            return focused != null && focused.Id == SearchInputId;
        }

        public void DeferRefreshUntilSearchInputUnfocused()
        {
            _refreshAfterSearchInput = true;
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            _refreshAfterSearchInput = false;
            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int itemFocusedIndex = GetFocusedItemIndex();
            RootWidget = BuildRoot(_adapter);
            RestoreItemFocus(itemFocusedIndex);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private int GetFocusedItemIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ItemsMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreItemFocus(int itemFocusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ItemsMenuIndex) as MenuWidget : null;
            menu?.SetFocusByIndexSilently(itemFocusedIndex);
        }

        private static ContainerWidget BuildRoot(CommunityMapsCollectionAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-collection", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTabs(adapter));
            root.AddChild(new ButtonWidget(
                "community-maps-collection-search-filter",
                () => adapter.SearchFilterLabel,
                adapter.OpenSearchFilter,
                null,
                () => adapter.HasSearchFilter,
                () => adapter.HasSearchFilter));
            root.AddChild(new ButtonWidget(
                "community-maps-collection-downloads",
                () => adapter.DownloadsLabel,
                adapter.OpenDownloadsMenu,
                null,
                () => adapter.HasDownloadsMenu,
                () => adapter.HasDownloadsMenu));
            root.AddChild(new TmpInputFieldWidget(
                "community-maps-collection-keyword",
                adapter.SearchFieldLabel,
                () => adapter.SearchField));
            AddButton(root, "community-maps-collection-", adapter.CheckForUpdatesAction);
            root.AddChild(BuildDropdown(adapter.FilterDropdown));
            root.AddChild(BuildDropdown(adapter.SortDropdown));
            root.AddChild(BuildItemsMenu(adapter));
            root.AddChild(new CheckboxWidget(
                "community-maps-collection-selected-enabled",
                () => adapter.SelectedItemLabel,
                () => adapter.ToggleSelectedItemEnabled(),
                adapter.IsSelectedItemEnabled,
                adapter.IsSelectedItemToggleVisible,
                adapter.IsSelectedItemToggleEnabled,
                null));
            root.AddChild(new ButtonWidget(
                "community-maps-collection-selected-unsubscribe",
                () => adapter.UnsubscribeLabel,
                adapter.UnsubscribeSelectedItem,
                null,
                adapter.IsUnsubscribeEnabled,
                adapter.IsUnsubscribeVisible));
            root.AddChild(new ButtonWidget(
                "community-maps-collection-selected-options",
                () => adapter.MoreOptionsLabel,
                adapter.OpenSelectedItemOptions,
                null,
                adapter.IsMoreOptionsEnabled,
                adapter.IsMoreOptionsVisible));
            root.AddChild(new ButtonWidget(
                "community-maps-collection-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                null,
                () => true));

            return root;
        }

        private static MenuWidget BuildTabs(CommunityMapsCollectionAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("community-maps-collection-tabs", ModText.Get(ModStrings.Screens.Tabs));
            IReadOnlyList<CommunityMapsCollectionAdapter.TabItem> tabs = adapter.GetTabs();
            string selectedId = null;
            for (int i = 0; i < tabs.Count; i++)
            {
                CommunityMapsCollectionAdapter.TabItem tab = tabs[i];
                CommunityMapsCollectionAdapter.TabItem captured = tab;
                if (captured.IsSelected)
                {
                    selectedId = "community-maps-collection-tab-" + captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    "community-maps-collection-tab-" + captured.Id,
                    () => captured.Label,
                    () => captured.IsSelected ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    captured.Select,
                    () => captured.Select(),
                    () => true));
            }

            menu.SetFocusedItemById(selectedId);
            return menu;
        }

        private static MenuWidget BuildDropdown(CommunityMapsCollectionAdapter.DropdownItem dropdown)
        {
            MenuWidget menu = new MenuWidget(
                "community-maps-collection-" + dropdown.Id,
                dropdown.Label,
                () => dropdown.IsVisible,
                () => dropdown.Focus(),
                null);
            IReadOnlyList<string> options = dropdown.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-collection-" + dropdown.Id + "-option-" + index,
                    () => options[index],
                    () => dropdown.Value == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => dropdown.SetValue(index),
                    () => dropdown.Focus(),
                    () => true));
            }

            menu.SetFocusByIndexSilently(dropdown.Value);
            return menu;
        }

        private static MenuWidget BuildItemsMenu(CommunityMapsCollectionAdapter adapter)
        {
            IReadOnlyList<CommunityMapsCollectionAdapter.CollectionItem> items = adapter.GetItems();
            MenuWidget menu = new MenuWidget(
                "community-maps-collection-items",
                adapter.ItemsLabel,
                () => items.Count > 0);
            for (int i = 0; i < items.Count; i++)
            {
                CommunityMapsCollectionAdapter.CollectionItem item = items[i];
                CommunityMapsCollectionAdapter.CollectionItem captured = item;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-collection-item-" + captured.Index,
                    () => captured.Label,
                    () => captured.Status,
                    () => adapter.ActivateItem(captured),
                    () => adapter.FocusItem(captured),
                    () => captured.IsVisible));
            }

            return menu;
        }

        private static void AddButton(
            ContainerWidget root,
            string idPrefix,
            CommunityMapsCollectionAdapter.ButtonAction action)
        {
            root.AddChild(new ButtonWidget(
                idPrefix + action.Id,
                () => action.Label,
                action.Activate,
                () => action.Focus(),
                action.IsEnabled,
                action.IsVisible));
        }
    }
}
