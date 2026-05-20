using System;
using System.Globalization;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Localization
{
    internal static class GameText
    {
        public static string Get(
            string key,
            string fallback,
            params object[] args)
        {
            return Get(GetCurrentLocalization(), key, fallback, args);
        }

        public static string Get(
            ILocalizationHandler localization,
            string key,
            string fallback,
            params object[] args)
        {
            if (localization == null || string.IsNullOrWhiteSpace(key))
            {
                return FormatFallback(fallback, args);
            }

            try
            {
                string text = localization.TryGetText(key, fallback ?? string.Empty, args);
                if (string.IsNullOrWhiteSpace(text) || text == key)
                {
                    return FormatFallback(fallback, args);
                }

                return text;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to localize game key " + key + ": " + exception.Message);
                return FormatFallback(fallback, args);
            }
        }

        public static string Plural(
            string key,
            int count,
            string fallback,
            params object[] args)
        {
            return Plural(GetCurrentLocalization(), key, count, fallback, args);
        }

        public static string Plural(
            ILocalizationHandler localization,
            string key,
            int count,
            string fallback,
            params object[] args)
        {
            if (localization == null || string.IsNullOrWhiteSpace(key))
            {
                return FormatFallback(fallback, args);
            }

            try
            {
                string text = localization.TryGetPluralText(key, count, fallback ?? string.Empty, args);
                if (string.IsNullOrWhiteSpace(text) || text == key)
                {
                    return FormatFallback(fallback, args);
                }

                return text;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to localize game plural key " + key + ": " + exception.Message);
                return FormatFallback(fallback, args);
            }
        }

        internal static string FormatFallback(string fallback, params object[] args)
        {
            if (string.IsNullOrEmpty(fallback))
            {
                return string.Empty;
            }

            try
            {
                string text = args == null || args.Length == 0
                    ? fallback
                    : string.Format(CultureInfo.CurrentCulture, fallback, args);
                return text;
            }
            catch (FormatException)
            {
                return fallback;
            }
        }

        private static ILocalizationHandler GetCurrentLocalization()
        {
            return GlobalLocalizationVariables.LocalizationHandler;
        }
    }
}
