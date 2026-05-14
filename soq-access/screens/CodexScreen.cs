using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CodexScreen : Screen
    {
        private static readonly PropertyInfo ContainerProperty = AccessTools.Property(typeof(MonoInstallerBase), "Container");
        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(MonoInstallerBase), "_container");

        private readonly CodexMenuAdapter _adapter;

        public CodexScreen(CodexMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CodexMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CodexMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CodexMenuAdapter adapter = new CodexMenuAdapter(ResolveCodexMenu(installers[i]));
                if (adapter.IsPresent())
                {
                    return new CodexScreen(adapter);
                }
            }

            return null;
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

        private static ContainerWidget BuildRoot(CodexMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("codex-screen", "Tutorials and Codex");
            if (adapter == null)
            {
                return root;
            }

            adapter.EnsureFocusedCategory();
            root.AddChild(BuildTabMenu(adapter));
            root.AddChild(BuildCategoryMenu(adapter));
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
            MenuWidget menu = new MenuWidget("codex-tabs", "Tabs");
            IReadOnlyList<CodexMenuAdapter.TabItem> tabs = SafeGet("codex tabs", adapter.GetTabs);
            string activeId = null;
            for (int i = 0; i < tabs.Count; i++)
            {
                CodexMenuAdapter.TabItem tab = tabs[i];
                CodexMenuAdapter.TabItem captured = tab;
                if (captured.IsActive)
                {
                    activeId = BuildTabId(captured);
                }

                menu.AddItem(new MenuItemWidget(
                    BuildTabId(captured),
                    () => captured.Label,
                    () => captured.IsActive ? "selected" : string.Empty,
                    () => adapter.FocusTab(captured.Index),
                    () => adapter.FocusTab(captured.Index),
                    () => true));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static MenuWidget BuildCategoryMenu(CodexMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("codex-categories", "Categories");
            IReadOnlyList<CodexMenuAdapter.ArticleGroupItem> groups = SafeGet("codex categories", adapter.GetArticleGroups);
            if (groups.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "codex-categories-none",
                    () => "No categories",
                    null,
                    () => false,
                    null,
                    () => true));
                return menu;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                CodexMenuAdapter.ArticleGroupItem group = groups[i];
                CodexMenuAdapter.ArticleGroupItem captured = group;
                menu.AddItem(new MenuItemWidget(
                    BuildCategoryId(captured),
                    () => captured.Label,
                    () => captured.Index == adapter.FocusedCategoryIndex ? "selected" : string.Empty,
                    () =>
                    {
                        adapter.FocusCategory(captured.Index);
                        return true;
                    },
                    () => adapter.FocusCategory(captured.Index),
                    () => true));
            }

            menu.SetFocusedItemById(BuildCategoryId(adapter.FocusedCategoryIndex));
            return menu;
        }

        private static MenuWidget BuildArticleMenu(CodexMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("codex-articles", "Articles");
            IReadOnlyList<CodexMenuAdapter.ArticleGroupItem> groups = SafeGet("codex articles", adapter.GetArticleGroups);
            string selectedId = null;
            if (groups.Count == 0)
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

            bool addedArticle = false;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                CodexMenuAdapter.ArticleGroupItem group = groups[groupIndex];
                if (group == null || group.Articles == null)
                {
                    continue;
                }

                for (int articleIndex = 0; articleIndex < group.Articles.Count; articleIndex++)
                {
                    CodexMenuAdapter.ArticleItem article = group.Articles[articleIndex];
                    CodexMenuAdapter.ArticleItem captured = article;
                    if (captured.IsSelected)
                    {
                        selectedId = BuildArticleId(captured);
                    }

                    menu.AddItem(new MenuItemWidget(
                        BuildArticleId(captured),
                        () => captured.Label,
                        null,
                        () => adapter.ActivateArticle(captured),
                        () => adapter.FocusArticle(captured),
                        () => captured.CategoryIndex == adapter.FocusedCategoryIndex));
                    addedArticle = true;
                }
            }

            if (!addedArticle)
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

            menu.SetFocusedItemById(selectedId);
            return menu;
        }

        private static string BuildTabId(CodexMenuAdapter.TabItem tab)
        {
            return "codex-tab-" + (tab != null ? tab.Index : 0);
        }

        private static string BuildCategoryId(CodexMenuAdapter.ArticleGroupItem group)
        {
            return BuildCategoryId(group != null ? group.Index : 0);
        }

        private static string BuildCategoryId(int index)
        {
            return "codex-category-" + index;
        }

        private static string BuildArticleId(CodexMenuAdapter.ArticleItem article)
        {
            if (article == null)
            {
                return "codex-article-0-0";
            }

            return "codex-article-" + article.CategoryIndex + "-" + article.ArticleIndex;
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

        private static CodexMenu ResolveCodexMenu(CodexMenuInstaller installer)
        {
            DiContainer container = GetContainer(installer);
            if (container == null)
            {
                return null;
            }

            return container.HasBinding<CodexMenu>()
                ? container.Resolve<CodexMenu>()
                : null;
        }

        private static DiContainer GetContainer(CodexMenuInstaller installer)
        {
            if (installer == null)
            {
                return null;
            }

            if (ContainerProperty != null)
            {
                return ContainerProperty.GetValue(installer, null) as DiContainer;
            }

            return ContainerField != null ? ContainerField.GetValue(installer) as DiContainer : null;
        }
    }
}
