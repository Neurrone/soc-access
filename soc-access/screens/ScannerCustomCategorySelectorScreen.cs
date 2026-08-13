using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Which subcategories of one taxonomy category a custom category gathers.
    /// The checkboxes read the stored selectors directly, so a toggle is
    /// already saved by the time it is spoken back.
    /// </summary>
    internal sealed class ScannerCustomCategorySelectorScreen : Screen
    {
        private readonly string _taxonomyKey;
        private readonly int _id;
        private readonly ScannerCategoryDefinition _definition;

        public ScannerCustomCategorySelectorScreen(string taxonomyKey, int id, ScannerCategoryDefinition definition)
            : base(new ContainerWidget("scanner-custom-category-selector-screen", GetTitle(definition)))
        {
            _taxonomyKey = taxonomyKey;
            _id = id;
            _definition = definition;
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

        private ContainerWidget BuildRoot()
        {
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle(_definition));
            if (_definition != null)
            {
                for (int i = 0; i < _definition.Subcategories.Count; i++)
                {
                    ScannerSubcategoryDefinition subcategory = _definition.Subcategories[i];
                    root.AddChild(new CheckboxWidget(
                        GetScreenId() + "-" + subcategory.Key,
                        subcategory.Label != null ? subcategory.Label() : subcategory.Key,
                        () => Toggle(subcategory.Key),
                        () => IsSelected(subcategory.Key)));
                }
            }

            root.AddChild(new ButtonWidget(
                GetScreenId() + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Close,
                null,
                () => true));
            return root;
        }

        private void Toggle(string subcategoryKey)
        {
            ModSettings.SetScannerCustomCategorySelector(
                _taxonomyKey,
                _id,
                _definition != null ? _definition.Key : null,
                subcategoryKey,
                !IsSelected(subcategoryKey));
        }

        private bool IsSelected(string subcategoryKey)
        {
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(_taxonomyKey, _id);
            return category != null
                && _definition != null
                && category.HasSelector(_definition.Key, subcategoryKey);
        }

        private string GetScreenId()
        {
            return "scanner-custom-category-selector-" + _id
                + "-" + (_definition != null ? _definition.Key : "unknown");
        }

        private static bool Close()
        {
            return SocAccessPlugin.Instance != null
                && SocAccessPlugin.Instance.ScreenManager != null
                && SocAccessPlugin.Instance.ScreenManager.Pop<ScannerCustomCategorySelectorScreen>(
                    "scanner custom category selectors closed");
        }

        private static string GetTitle(ScannerCategoryDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return definition.Label != null ? definition.Label() : definition.Key;
        }
    }
}
