using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Which single key walks one custom category on the adventure map. Every
    /// key says who holds it, because picking one that is taken moves it, and
    /// the player deserves to know what they are about to take it from.
    /// </summary>
    public sealed class ScannerCustomCategoryKeyScreen : Screen
    {
        private readonly string _taxonomyKey;
        private readonly int _id;

        public ScannerCustomCategoryKeyScreen(string taxonomyKey, int id)
            : base(new ContainerWidget("scanner-custom-category-key-screen", string.Empty))
        {
            _taxonomyKey = taxonomyKey;
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

        private ContainerWidget BuildRoot()
        {
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle());
            for (int i = 0; i < ScannerQuickKeys.Assignable.Length; i++)
            {
                AddOption(root, ScannerQuickKeys.Assignable[i]);
            }

            AddOption(root, ScannerQuickKey.None);
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Close,
                null,
                () => true));
            return root;
        }

        private void AddOption(ContainerWidget root, ScannerQuickKey quickKey)
        {
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-" + ScannerQuickKeys.ToToken(quickKey),
                () => DescribeOption(quickKey),
                () => Choose(quickKey),
                null,
                () => true));
        }

        /// <summary>
        /// Resolved on every read rather than baked into the label, so choosing
        /// a key re-reads the whole list correctly without rebuilding it.
        /// </summary>
        private string DescribeOption(ScannerQuickKey quickKey)
        {
            string name = ScannerQuickKeyText.Name(quickKey);
            ScannerCustomCategory category = GetCategory();
            if (category != null && category.QuickKey == quickKey)
            {
                return ModText.Get(ModStrings.Screens.CustomCategoryKeyCurrent, name);
            }

            ScannerCustomCategory holder = ModSettings.GetScannerCustomCategoryByQuickKey(_taxonomyKey, quickKey);
            return holder != null
                ? ModText.Get(ModStrings.Screens.CustomCategoryKeyHeldBy, name, holder.Name)
                : name;
        }

        private bool Choose(ScannerQuickKey quickKey)
        {
            ModSettings.SetScannerCustomCategoryQuickKey(_taxonomyKey, _id, quickKey);
            return Close();
        }

        private ScannerCustomCategory GetCategory()
        {
            return ModSettings.GetScannerCustomCategory(_taxonomyKey, _id);
        }

        private string GetTitle()
        {
            ScannerCustomCategory category = GetCategory();
            return ModText.Get(
                ModStrings.Screens.CustomCategoryKeyTitle,
                category != null ? category.Name : string.Empty);
        }

        private string GetScreenId()
        {
            return "scanner-custom-category-key-" + _id;
        }

        private static bool Close()
        {
            return SocAccessMod.Instance != null
                && SocAccessMod.Instance.ScreenManager != null
                && SocAccessMod.Instance.ScreenManager.Pop<ScannerCustomCategoryKeyScreen>(
                    "scanner custom category key screen closed");
        }
    }
}
