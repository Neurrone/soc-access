using System;
using System.Collections.Generic;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Localization
{
    public static class ModText
    {
        public static string Get(ModString text, params object[] args)
        {
            return Get(GetCurrentLocalization(), text, args);
        }

        public static string Get(
            ILocalizationHandler localization,
            ModString text,
            params object[] args)
        {
            return Get(localization, text.Key, text.Text, args);
        }

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
            string text;
            if (TryGetTranslatedText(localization, key, out text))
            {
                return GameText.FormatFallback(text, args);
            }

            return GameText.FormatFallback(fallback, args);
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
            ModPluralString text,
            int count,
            params object[] args)
        {
            return Plural(GetCurrentLocalization(), text, count, args);
        }

        public static string Plural(
            ILocalizationHandler localization,
            ModPluralString text,
            int count,
            params object[] args)
        {
            return Plural(localization, text.Key, count, text.GetFallback(count), args);
        }

        public static string Plural(
            ILocalizationHandler localization,
            string key,
            int count,
            string fallback,
            params object[] args)
        {
            string text;
            if (TryGetTranslatedPluralText(localization, key, count, out text))
            {
                return GameText.FormatFallback(text, args);
            }

            return GameText.FormatFallback(fallback, args);
        }

        public static string JoinList(IReadOnlyList<string> parts)
        {
            return JoinList(GetCurrentLocalization(), parts);
        }

        public static string JoinList(ILocalizationHandler localization, IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return string.Empty;
            }

            List<string> nonEmptyParts = new List<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    nonEmptyParts.Add(parts[i]);
                }
            }

            if (nonEmptyParts.Count == 0)
            {
                return string.Empty;
            }

            if (nonEmptyParts.Count == 1)
            {
                return nonEmptyParts[0];
            }

            if (nonEmptyParts.Count == 2)
            {
                return Get(localization, ModStrings.Common.ListPair, nonEmptyParts[0], nonEmptyParts[1]);
            }

            string text = Get(localization, ModStrings.Common.ListSeparator, nonEmptyParts[0], nonEmptyParts[1]);
            for (int i = 2; i < nonEmptyParts.Count - 1; i++)
            {
                text = Get(localization, ModStrings.Common.ListSeparator, text, nonEmptyParts[i]);
            }

            return Get(localization, ModStrings.Common.ListFinal, text, nonEmptyParts[nonEmptyParts.Count - 1]);
        }

        public static string JoinListWithCommas(IReadOnlyList<string> parts)
        {
            return JoinListWithCommas(GetCurrentLocalization(), parts);
        }

        public static string JoinListWithCommas(ILocalizationHandler localization, IReadOnlyList<string> parts)
        {
            return JoinList(localization, ModStrings.Common.ListSeparator, parts);
        }

        /// <summary>
        /// Runs the parts together with the given two-placeholder separator, folding from the
        /// left. Every language sees the same pair of neighbouring parts, so a locale that puts
        /// them the other way round only has to say so once in its own separator.
        /// </summary>
        public static string JoinList(ModString separator, IReadOnlyList<string> parts)
        {
            return JoinList(GetCurrentLocalization(), separator, parts);
        }

        public static string JoinList(ILocalizationHandler localization, ModString separator, IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return string.Empty;
            }

            List<string> nonEmptyParts = new List<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    nonEmptyParts.Add(parts[i]);
                }
            }

            if (nonEmptyParts.Count == 0)
            {
                return string.Empty;
            }

            string text = nonEmptyParts[0];
            for (int i = 1; i < nonEmptyParts.Count; i++)
            {
                text = Get(localization, separator, text, nonEmptyParts[i]);
            }

            return text;
        }

        public static string FormatPossessiveName(string name, ModString emptyFallback)
        {
            return FormatPossessiveName(GetCurrentLocalization(), name, emptyFallback);
        }

        public static string FormatPossessiveName(ILocalizationHandler localization, string name, ModString emptyFallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Get(localization, emptyFallback);
            }

            string languageCode = localization != null && localization.CurrentLanguage != null
                ? localization.CurrentLanguage.LanguageCode
                : null;
            if ((string.IsNullOrWhiteSpace(languageCode) || languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                && (name.EndsWith("s", StringComparison.Ordinal) || name.EndsWith("S", StringComparison.Ordinal)))
            {
                return name + "'";
            }

            return Get(localization, ModStrings.Common.NamePossessive, name);
        }

        private static bool TryGetTranslatedText(ILocalizationHandler localization, string key, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            PoTranslationCatalog catalog = ModTranslationLoader.GetCatalog(localization);
            return catalog != null && catalog.TryGetText(key, out text);
        }

        private static bool TryGetTranslatedPluralText(
            ILocalizationHandler localization,
            string key,
            int count,
            out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            PoTranslationCatalog catalog = ModTranslationLoader.GetCatalog(localization);
            if (catalog == null)
            {
                return false;
            }

            string languageCode = localization != null && localization.CurrentLanguage != null
                ? localization.CurrentLanguage.LanguageCode
                : null;
            int pluralForm = string.IsNullOrEmpty(languageCode)
                ? 0
                : PluralFormUtility.GetPluralForm(languageCode, count);

            if (catalog.TryGetText(key + "_" + pluralForm, out text))
            {
                return true;
            }

            return catalog.TryGetText(key, out text);
        }

        private static ILocalizationHandler GetCurrentLocalization()
        {
            return GlobalLocalizationVariables.LocalizationHandler;
        }
    }
}
