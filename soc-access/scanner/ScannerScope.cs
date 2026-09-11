using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The two subcategory definitions a taxonomy writes over and over: one named scope, and the
    /// All scope nearly every category opens with. A taxonomy pulls them in with
    /// <c>using static SongsOfConquestAccess.Scanner.ScannerScope;</c> and writes them unqualified.
    /// </summary>
    public static class ScannerScope
    {
        /// <summary>The catch-all scope: everything the category holds, in one list.</summary>
        public static ScannerSubcategoryDefinition All()
        {
            return Subcategory(ScannerSubcategoryKeys.All, ModStrings.Scanner.All);
        }

        public static ScannerSubcategoryDefinition Subcategory(string key, ModString label)
        {
            return new ScannerSubcategoryDefinition(key, () => ModText.Get(label));
        }
    }
}
