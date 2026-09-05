namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The single key that walks one custom category on the adventure map.
    /// Stored with the category, so the tokens the codec writes must never
    /// change once shipped.
    /// </summary>
    public enum ScannerQuickKey
    {
        None,
        Comma,
        Period,
        Slash
    }

    public static class ScannerQuickKeys
    {
        private const string CommaToken = "comma";
        private const string PeriodToken = "period";
        private const string SlashToken = "slash";

        /// <summary>
        /// The keys a custom category can hold, in the order a new category
        /// fills them.
        /// </summary>
        public static readonly ScannerQuickKey[] Assignable =
        {
            ScannerQuickKey.Comma,
            ScannerQuickKey.Period,
            ScannerQuickKey.Slash
        };

        public static string ToToken(ScannerQuickKey key)
        {
            switch (key)
            {
                case ScannerQuickKey.Comma:
                    return CommaToken;
                case ScannerQuickKey.Period:
                    return PeriodToken;
                case ScannerQuickKey.Slash:
                    return SlashToken;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// An unrecognised token reads as no key rather than throwing, so a
        /// config written by a newer build still loads on an older one with the
        /// key simply unset.
        /// </summary>
        public static ScannerQuickKey FromToken(string token)
        {
            switch (token)
            {
                case CommaToken:
                    return ScannerQuickKey.Comma;
                case PeriodToken:
                    return ScannerQuickKey.Period;
                case SlashToken:
                    return ScannerQuickKey.Slash;
                default:
                    return ScannerQuickKey.None;
            }
        }
    }
}
