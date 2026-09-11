using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// How a resource entry of a treasury strip is said: the resource's name, the amount the game
    /// draws beside it, and the income it draws under it where there is one.
    ///
    /// Two strips are drawn with the same three facts - the kingdom HUD's own, and the one a row of
    /// the player list opens - and they are read the same way, so the wording lives here rather than
    /// once per screen.
    /// </summary>
    public static class ResourceStrip
    {
        /// <param name="name">The resource's localized name.</param>
        /// <param name="amount">The amount as the game draws it, empty where it draws none.</param>
        /// <param name="income">The income as the game draws it, empty where it draws none.</param>
        public static string Label(string name, string amount, string income)
        {
            string label = string.IsNullOrWhiteSpace(amount)
                ? name
                : ModText.Get(ModStrings.Common.ResourceAmount, name, amount);
            return string.IsNullOrWhiteSpace(income)
                ? label
                : ModText.Get(ModStrings.Common.ListSeparator, label, income);
        }
    }
}
