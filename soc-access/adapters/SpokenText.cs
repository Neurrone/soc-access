using System.Text.RegularExpressions;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The game's own localized text, ready to be spoken: what
    /// <see cref="GameText.Get(ILocalizationHandler, string, string, object[])"/> answers, split on
    /// its line breaks and stripped of the tags the game's renderer draws.</summary>
    public static class SpokenText
    {
        public static string Get(
            ILocalizationHandler localization,
            string key,
            string fallback,
            params object[] args)
        {
            return SpokenLines.Clean(GameText.Get(localization, key, fallback, args));
        }

        /// <summary>As above, through the localization handler the game currently holds.</summary>
        public static string Get(string key, string fallback, params object[] args)
        {
            return SpokenLines.Clean(GameText.Get(key, fallback, args));
        }

        /// <summary>A drawn amount with its minus sign joined back onto the digits: the game spaces
        /// the sign away from the number in its cost labels ("- 30"), which a screen reader says as a
        /// dash and then a number rather than as a loss.</summary>
        public static string JoinMinusSign(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text, @"-\s+(\d)", "-$1");
        }
    }
}
