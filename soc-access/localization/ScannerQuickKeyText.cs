using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Localization
{
    /// <summary>
    /// Speaks the name of a key a custom category can be walked from. The
    /// punctuation itself is unspeakable, so the mod names each key rather than
    /// handing a screen reader the character.
    /// </summary>
    public static class ScannerQuickKeyText
    {
        public static string Name(ScannerQuickKey quickKey)
        {
            switch (quickKey)
            {
                case ScannerQuickKey.Comma:
                    return ModText.Get(ModStrings.Scanner.QuickKeyComma);
                case ScannerQuickKey.Period:
                    return ModText.Get(ModStrings.Scanner.QuickKeyPeriod);
                case ScannerQuickKey.Slash:
                    return ModText.Get(ModStrings.Scanner.QuickKeySlash);
                default:
                    return ModText.Get(ModStrings.Scanner.QuickKeyNone);
            }
        }
    }
}
