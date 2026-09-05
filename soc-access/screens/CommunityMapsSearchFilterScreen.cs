using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class CommunityMapsSearchFilterScreen : Screen
    {
        private readonly CommunityMapsSearchFilterAdapter _adapter;

        public CommunityMapsSearchFilterScreen(CommunityMapsSearchFilterAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsSearchFilterAdapter adapter = CommunityMapsSearchFilterAdapter.TryCreate();
            return adapter != null && adapter.IsPresent()
                ? new CommunityMapsSearchFilterScreen(adapter)
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

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key || base.HasClaimed(actionKey);
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

            FocusState focusState = CaptureFocusState();
            RootWidget = BuildRoot(_adapter);
            RestoreFocusState(focusState);
        }

        private FocusState CaptureFocusState()
        {
            Widget focused = UIManager.CurrentWidget;
            return focused != null
                ? new FocusState(focused.Parent != null ? focused.Parent.Id : focused.Id, focused.Id)
                : null;
        }

        private void RestoreFocusState(FocusState state)
        {
            if (state == null || RootWidget == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.ParentId))
            {
                MenuWidget menu = RootWidget.GetChildById(state.ParentId) as MenuWidget;
                if (menu != null && menu.SetFocusedItemById(state.WidgetId))
                {
                    RootWidget.SetFocusedChildById(state.ParentId);
                    return;
                }
            }

            RootWidget.SetFocusedChildById(state.WidgetId);
        }

        private static ContainerWidget BuildRoot(CommunityMapsSearchFilterAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-search-filter", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TmpInputFieldWidget(
                "community-maps-search-filter-keyword",
                adapter.SearchFieldLabel,
                () => adapter.SearchField,
                activateAfterEndOfFrame: true));

            IReadOnlyList<CommunityMapsSearchFilterAdapter.CategoryItem> categories = adapter.GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                root.AddChild(BuildCategoryMenu(categories[i]));
            }

            IReadOnlyList<CommunityMapsSearchFilterAdapter.ActionItem> actions = adapter.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                CommunityMapsSearchFilterAdapter.ActionItem action = actions[i];
                CommunityMapsSearchFilterAdapter.ActionItem captured = action;
                root.AddChild(new ButtonWidget(
                    "community-maps-search-filter-action-" + captured.Id,
                    () => captured.Label,
                    captured.Activate,
                    captured.Focus,
                    () => captured.IsEnabled,
                    () => true));
            }

            return root;
        }

        private static MenuWidget BuildCategoryMenu(CommunityMapsSearchFilterAdapter.CategoryItem category)
        {
            MenuWidget menu = new MenuWidget(
                "community-maps-search-filter-category-" + category.Index,
                category.Label,
                () => category.Tags.Count > 0);

            IReadOnlyList<CommunityMapsSearchFilterAdapter.TagItem> tags = category.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                CommunityMapsSearchFilterAdapter.TagItem tag = tags[i];
                CommunityMapsSearchFilterAdapter.TagItem captured = tag;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-search-filter-category-" + category.Index + "-tag-" + captured.Index,
                    () => captured.Label,
                    () => captured.IsSelected
                        ? ModText.Get(ModStrings.UI.StatusChecked)
                        : ModText.Get(ModStrings.UI.StatusUnchecked),
                    () => ToggleTag(captured),
                    captured.Focus,
                    () => true));
            }

            return menu;
        }

        private static bool ToggleTag(CommunityMapsSearchFilterAdapter.TagItem tag)
        {
            if (tag == null || !tag.Toggle())
            {
                return false;
            }

            string status = tag.IsSelected
                ? ModText.Get(ModStrings.UI.StatusChecked)
                : ModText.Get(ModStrings.UI.StatusUnchecked);
            SpeechPipeline.Output(new SpeechRequest(status, interrupt: false));
            return true;
        }

        private sealed class FocusState
        {
            public FocusState(string parentId, string widgetId)
            {
                ParentId = parentId ?? string.Empty;
                WidgetId = widgetId ?? string.Empty;
            }

            public string ParentId { get; private set; }

            public string WidgetId { get; private set; }
        }
    }
}
