using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CodexScreen : Screen
    {
        private readonly CodexMenuAdapter _adapter;

        public CodexScreen(CodexMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
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

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            if (focusAfterRefresh)
            {
                if (RootWidget == null || !RootWidget.SetFocusByIndex(focusedIndex))
                {
                    RootWidget?.Focus();
                }
            }
            else
            {
                RootWidget?.SetFocusByIndexSilently(focusedIndex);
            }
        }

        private static ContainerWidget BuildRoot(CodexMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("codex-screen", "Tutorials and Codex");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTabMenu(adapter));
            root.AddChild(BuildArticleMenu(adapter));
            root.AddChild(new CodexContentWidget(
                "codex-content",
                adapter.GetContentItems,
                null,
                adapter.ScrollContentItemIntoView));

            root.AddChild(new ButtonWidget(
                "codex-reset-tutorials",
                "Reset tutorials",
                adapter.ResetTutorials,
                null,
                adapter.IsTutorialSettingsVisible,
                adapter.IsTutorialSettingsVisible));

            root.AddChild(new CheckboxWidget(
                "codex-show-tutorials",
                adapter.TutorialsToggleLabel,
                adapter.ToggleTutorials,
                adapter.IsTutorialsChecked,
                adapter.IsTutorialSettingsVisible));

            root.AddChild(new ButtonWidget(
                "codex-close",
                "Close",
                adapter.Close,
                null,
                () => true));

            return root;
        }

        private static MenuWidget BuildTabMenu(CodexMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("codex-tabs", "Categories");
            IReadOnlyList<CodexMenuAdapter.TabItem> tabs = SafeGet("codex tabs", adapter.GetTabs);
            string activeId = null;
            for (int i = 0; i < tabs.Count; i++)
            {
                CodexMenuAdapter.TabItem tab = tabs[i];
                CodexMenuAdapter.TabItem captured = tab;
                if (captured.IsActive)
                {
                    activeId = captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    () => captured.IsActive ? "selected" : string.Empty,
                    () => adapter.FocusTab(captured.Index),
                    () => adapter.FocusTab(captured.Index),
                    () => true));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static MenuWidget BuildArticleMenu(CodexMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("codex-articles", "Articles");
            IReadOnlyList<CodexMenuAdapter.ArticleItem> articles = SafeGet("codex articles", adapter.GetArticles);
            string selectedId = null;
            if (articles.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "codex-articles-none",
                    () => "No articles",
                    null,
                    () => false,
                    null,
                    () => true));
                return menu;
            }

            for (int i = 0; i < articles.Count; i++)
            {
                CodexMenuAdapter.ArticleItem article = articles[i];
                CodexMenuAdapter.ArticleItem captured = article;
                if (captured.IsSelected)
                {
                    selectedId = captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    null,
                    () => adapter.ActivateArticle(captured),
                    () => adapter.FocusArticle(captured),
                    () => true));
            }

            menu.SetFocusedItemById(selectedId);
            return menu;
        }

        private static IReadOnlyList<T> SafeGet<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception ex)
            {
                SoqAccessPlugin.Instance?.LogWarning("CodexScreen failed to build " + section + ": " + ex);
                return new T[0];
            }
        }
    }
}
