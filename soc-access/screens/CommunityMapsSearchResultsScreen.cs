using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class CommunityMapsSearchResultsScreen : Screen
    {
        private const int ResultsMenuIndex = 4;

        private CommunityMapsSearchResultsAdapter _adapter;

        public CommunityMapsSearchResultsScreen(CommunityMapsSearchResultsAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public CommunityMapsSearchResultsAdapter Adapter
        {
            get { return _adapter; }
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsSearchResultsAdapter adapter = CommunityMapsSearchResultsAdapter.TryCreate();
            return adapter != null && adapter.IsPresent()
                ? new CommunityMapsSearchResultsScreen(adapter)
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

                return _adapter != null && _adapter.Back();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            Refresh(_adapter);
        }

        public void Refresh(CommunityMapsSearchResultsAdapter adapter)
        {
            if (adapter == null || !adapter.IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int resultFocusedIndex = GetFocusedResultIndex();
            _adapter = adapter;
            RootWidget = BuildRoot(_adapter);
            RestoreResultFocus(resultFocusedIndex);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private int GetFocusedResultIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ResultsMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreResultFocus(int resultFocusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ResultsMenuIndex) as MenuWidget : null;
            menu?.SetFocusByIndexSilently(resultFocusedIndex);
        }

        private static ContainerWidget BuildRoot(CommunityMapsSearchResultsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-search-results", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            string summaryText = adapter.SummaryText;
            bool hasSummaryText = !string.IsNullOrWhiteSpace(summaryText);
            string footerText = adapter.FooterText;
            bool hasFooterText = !string.IsNullOrWhiteSpace(footerText);
            string refineFilterLabel = adapter.RefineFilterLabel;
            bool hasRefineFilter = adapter.HasRefineFilter;
            string subscribeLabel = adapter.SubscribeLabel;
            bool hasSubscribeAction = adapter.HasSubscribeAction;
            string moreOptionsLabel = adapter.MoreOptionsLabel;
            bool hasMoreOptionsAction = adapter.HasMoreOptionsAction;

            root.AddChild(new TextWidget(
                "community-maps-search-results-summary",
                () => summaryText,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => hasSummaryText));
            root.AddChild(new ButtonWidget(
                "community-maps-search-results-refine-filter",
                () => refineFilterLabel,
                adapter.OpenRefineFilter,
                null,
                () => hasRefineFilter,
                () => hasRefineFilter));
            root.AddChild(BuildSortMenu(adapter.Sort));
            root.AddChild(new TextWidget(
                "community-maps-search-results-footer",
                () => footerText,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => hasFooterText));
            root.AddChild(BuildResultsMenu(adapter));
            root.AddChild(new ButtonWidget(
                "community-maps-search-results-subscribe",
                () => subscribeLabel,
                adapter.SubscribeSelected,
                null,
                () => hasSubscribeAction,
                () => hasSubscribeAction));
            root.AddChild(new ButtonWidget(
                "community-maps-search-results-more-options",
                () => moreOptionsLabel,
                adapter.OpenSelectedOptions,
                null,
                () => hasMoreOptionsAction,
                () => hasMoreOptionsAction));
            root.AddChild(new ButtonWidget(
                "community-maps-search-results-back",
                () => adapter.BackLabel,
                adapter.Back,
                null,
                () => true));

            return root;
        }

        private static MenuWidget BuildSortMenu(CommunityMapsSearchResultsAdapter.SortDropdown dropdown)
        {
            MenuWidget menu = new MenuWidget(
                "community-maps-search-results-sort",
                dropdown.Label,
                () => dropdown.IsVisible,
                dropdown.Focus,
                null);
            IReadOnlyList<string> options = dropdown.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-search-results-sort-option-" + index,
                    () => options[index],
                    () => dropdown.Value == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => dropdown.SetValue(index),
                    dropdown.Focus,
                    () => true));
            }

            menu.SetFocusByIndexSilently(dropdown.Value);
            return menu;
        }

        private static MenuWidget BuildResultsMenu(CommunityMapsSearchResultsAdapter adapter)
        {
            IReadOnlyList<CommunityMapsSearchResultsAdapter.ResultItem> results = adapter.Results;
            MenuWidget menu = new MenuWidget(
                "community-maps-search-results-list",
                string.Empty,
                () => results.Count > 0);
            for (int i = 0; i < results.Count; i++)
            {
                CommunityMapsSearchResultsAdapter.ResultItem result = results[i];
                CommunityMapsSearchResultsAdapter.ResultItem captured = result;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-search-results-item-" + captured.Id,
                    () => captured.Label,
                    null,
                    () => adapter.ActivateResult(captured),
                    () => adapter.FocusResult(captured),
                    () => true));
            }

            return menu;
        }
    }
}
