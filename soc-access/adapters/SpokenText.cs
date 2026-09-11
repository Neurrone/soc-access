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
    }
}
