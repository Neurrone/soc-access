using System.Collections.Generic;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The custom categories defined for one scanner context, with a way to add
    /// another. Each one opens its own editor.
    /// </summary>
    public sealed class ScannerCustomCategoriesScreen : Screen
    {
        private readonly ScannerTaxonomy _taxonomy;
        private readonly ModString _contextLabel;

        public ScannerCustomCategoriesScreen(ScannerTaxonomy taxonomy, ModString contextLabel)
            : base(new ContainerWidget("scanner-custom-categories-screen", GetTitle(contextLabel)))
        {
            _taxonomy = taxonomy;
            _contextLabel = contextLabel;
            RootWidget = BuildRoot();
        }

        public override bool IsPresent()
        {
            return true;
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
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle(_contextLabel));
            IReadOnlyList<ScannerCustomCategory> categories = GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                ScannerCustomCategory category = categories[i];
                root.AddChild(new ButtonWidget(
                    GetScreenId() + "-category-" + category.Id,
                    category.Name,
                    () => OpenEditor(category.Id),
                    null,
                    () => true));
            }

            root.AddChild(new ButtonWidget(
                GetScreenId() + "-add",
                ModText.Get(ModStrings.Screens.AddCustomCategory),
                AddCategory,
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
        /// The list changes from the editor screen, which deletes and renames,
        /// so it is rebuilt whenever this screen comes back to the front rather
        /// than only at construction.
        /// </summary>
        private void Rebuild()
        {
            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot();
            RootWidget.SetFocusByIndexSilently(focusedIndex);
        }

        private bool AddCategory()
        {
            ScannerCustomCategory category = ModSettings.AddScannerCustomCategory(
                GetTaxonomyKey(),
                position => ModText.Get(ModStrings.Screens.CustomCategoryDefaultName, position));
            if (category == null)
            {
                return false;
            }

            Rebuild();
            return OpenEditor(category.Id);
        }

        private bool OpenEditor(int id)
        {
            if (SocAccessMod.Instance == null || SocAccessMod.Instance.ScreenManager == null)
            {
                return false;
            }

            SocAccessMod.Instance.ScreenManager.Push(
                new ScannerCustomCategoryScreen(_taxonomy, id),
                "scanner custom category editor opened");
            return true;
        }

        private IReadOnlyList<ScannerCustomCategory> GetCategories()
        {
            return ModSettings.GetScannerCustomCategories(GetTaxonomyKey());
        }

        private string GetTaxonomyKey()
        {
            return _taxonomy != null ? _taxonomy.Key : null;
        }

        private string GetScreenId()
        {
            return "scanner-custom-categories-" + (_taxonomy != null ? _taxonomy.Key : "unknown");
        }

        private static bool Close()
        {
            return SocAccessMod.Instance != null
                && SocAccessMod.Instance.ScreenManager != null
                && SocAccessMod.Instance.ScreenManager.Pop<ScannerCustomCategoriesScreen>(
                    "scanner custom categories screen closed");
        }

        private static string GetTitle(ModString contextLabel)
        {
            return ModText.Get(ModStrings.Screens.CustomCategories, ModText.Get(contextLabel));
        }
    }
}
