using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// One custom category: its name, which taxonomy subcategories it gathers,
    /// and which keywords it matches. Subcategories are picked one source
    /// category at a time, because the full taxonomy is far too long a list to
    /// walk through as a single run of checkboxes.
    /// </summary>
    internal sealed class ScannerCustomCategoryScreen : Screen
    {
        private readonly ScannerTaxonomy _taxonomy;
        private readonly int _id;

        public ScannerCustomCategoryScreen(ScannerTaxonomy taxonomy, int id)
            : base(new ContainerWidget("scanner-custom-category-screen", string.Empty))
        {
            _taxonomy = taxonomy;
            _id = id;
            RootWidget = BuildRoot();
        }

        public override bool IsPresent()
        {
            return GetCategory() != null;
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return Close();
            }

            return base.OnActionJustPressed(action);
        }

        public override void OnFocus()
        {
            Rebuild();
            base.OnFocus();
        }

        private ContainerWidget BuildRoot()
        {
            ScannerCustomCategory category = GetCategory();
            ContainerWidget root = new ContainerWidget(
                GetScreenId(),
                category != null ? category.Name : string.Empty);
            if (category == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                GetScreenId() + "-rename",
                ModText.Get(ModStrings.Screens.RenameCustomCategory),
                Rename,
                null,
                () => true));

            IReadOnlyList<ScannerCategoryDefinition> definitions = _taxonomy != null
                ? _taxonomy.Categories
                : new ScannerCategoryDefinition[0];
            for (int i = 0; i < definitions.Count; i++)
            {
                ScannerCategoryDefinition definition = definitions[i];
                root.AddChild(new ButtonWidget(
                    GetScreenId() + "-source-" + definition.Key,
                    DescribeSource(definition),
                    () => OpenSelectors(definition),
                    null,
                    () => true));
            }

            for (int i = 0; i < category.Keywords.Count; i++)
            {
                string keyword = category.Keywords[i];
                root.AddChild(new ButtonWidget(
                    GetScreenId() + "-keyword-" + i,
                    ModText.Get(ModStrings.Screens.RemoveKeyword, keyword),
                    () => RemoveKeyword(keyword),
                    null,
                    () => true));
            }

            root.AddChild(new ButtonWidget(
                GetScreenId() + "-add-keyword",
                ModText.Get(ModStrings.Screens.AddKeyword),
                AddKeyword,
                null,
                () => true));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-delete",
                ModText.Get(ModStrings.Screens.DeleteCustomCategory),
                Delete,
                null,
                () => true));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Close,
                null,
                () => true));
            return root;
        }

        /// <summary>
        /// Renaming and keyword edits both land here from a popup that closes
        /// after this screen was built, and the selector screen changes the
        /// counts, so the whole screen is rebuilt whenever it comes back to the
        /// front.
        /// </summary>
        private void Rebuild()
        {
            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot();
            RootWidget.SetFocusByIndexSilently(focusedIndex);
        }

        /// <summary>
        /// Says how much of a source category this custom category takes, so
        /// the player can see what is picked without opening every one.
        /// </summary>
        private string DescribeSource(ScannerCategoryDefinition definition)
        {
            string label = definition.Label != null ? definition.Label() : definition.Key;
            int count = CountSelected(definition);
            return ModText.Get(
                ModStrings.Common.ListSeparator,
                label,
                ModText.Plural(ModStrings.Screens.SelectedSubcategoryCount, count, count));
        }

        private int CountSelected(ScannerCategoryDefinition definition)
        {
            ScannerCustomCategory category = GetCategory();
            if (category == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < definition.Subcategories.Count; i++)
            {
                if (category.HasSelector(definition.Key, definition.Subcategories[i].Key))
                {
                    count++;
                }
            }

            return count;
        }

        private bool OpenSelectors(ScannerCategoryDefinition definition)
        {
            if (SocAccessPlugin.Instance == null || SocAccessPlugin.Instance.ScreenManager == null)
            {
                return false;
            }

            SocAccessPlugin.Instance.ScreenManager.Push(
                new ScannerCustomCategorySelectorScreen(GetTaxonomyKey(), _id, definition),
                "scanner custom category selectors opened");
            return true;
        }

        private bool Rename()
        {
            ScannerCustomCategory category = GetCategory();
            if (category == null)
            {
                return false;
            }

            return Prompt(
                ModText.Get(ModStrings.Screens.RenameCustomCategory),
                category.Name,
                name =>
                {
                    ModSettings.RenameScannerCustomCategory(GetTaxonomyKey(), _id, name);
                    Rebuild();
                    UIManager.RequestFocus(RootWidget);
                });
        }

        private bool AddKeyword()
        {
            return Prompt(
                ModText.Get(ModStrings.Screens.AddKeyword),
                string.Empty,
                keyword =>
                {
                    string trimmed = (keyword ?? string.Empty).Trim();
                    // A refused keyword that was not blank was already there,
                    // and swallowing that silently reads as a dead keypress.
                    if (!ModSettings.AddScannerCustomCategoryKeyword(GetTaxonomyKey(), _id, trimmed)
                        && trimmed.Length > 0)
                    {
                        Speak(ModText.Get(ModStrings.Screens.KeywordAlreadyAdded));
                    }

                    Rebuild();
                    UIManager.RequestFocus(RootWidget);
                });
        }

        private bool RemoveKeyword(string keyword)
        {
            ModSettings.RemoveScannerCustomCategoryKeyword(GetTaxonomyKey(), _id, keyword);
            Rebuild();
            UIManager.RequestFocus(RootWidget);
            return true;
        }

        private bool Delete()
        {
            ScannerCustomCategory category = GetCategory();
            string name = category != null ? category.Name : string.Empty;
            if (!ModSettings.RemoveScannerCustomCategory(GetTaxonomyKey(), _id))
            {
                return false;
            }

            Speak(ModText.Get(ModStrings.Screens.CustomCategoryDeleted, name));
            return Close();
        }

        private static bool Prompt(string title, string prepopulate, Action<string> onConfirm)
        {
            if (NativeTextPrompt.Ask(title, prepopulate, title, onConfirm))
            {
                return true;
            }

            Speak(ModText.Get(ModStrings.Screens.TextEntryUnavailable));
            return true;
        }

        private static void Speak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        private ScannerCustomCategory GetCategory()
        {
            return ModSettings.GetScannerCustomCategory(GetTaxonomyKey(), _id);
        }

        private string GetTaxonomyKey()
        {
            return _taxonomy != null ? _taxonomy.Key : null;
        }

        private string GetScreenId()
        {
            return "scanner-custom-category-" + (_taxonomy != null ? _taxonomy.Key : "unknown") + "-" + _id;
        }

        private static bool Close()
        {
            return SocAccessPlugin.Instance != null
                && SocAccessPlugin.Instance.ScreenManager != null
                && SocAccessPlugin.Instance.ScreenManager.Pop<ScannerCustomCategoryScreen>(
                    "scanner custom category editor closed");
        }
    }
}
