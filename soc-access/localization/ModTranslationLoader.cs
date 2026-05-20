using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Localization
{
    internal static class ModTranslationLoader
    {
        private const string TranslationsDirectoryName = "translations";
        private const string ConfigDirectoryName = "SongsOfConquestAccess";

        private static readonly object LockObject = new object();
        private static string _loadedLanguageCode;
        private static PoTranslationCatalog _loadedCatalog;

        public static void LoadCurrentLanguage(ILocalizationHandler localization)
        {
            string languageCode = GetLanguageCode(localization);

            lock (LockObject)
            {
                _loadedLanguageCode = languageCode;
                _loadedCatalog = null;

                if (string.IsNullOrEmpty(languageCode))
                {
                    SocAccessPlugin.Instance?.LogInfo("No game language is available for mod translations");
                    return;
                }

                if (IsEnglish(languageCode))
                {
                    SocAccessPlugin.Instance?.LogInfo("Using English mod string fallbacks");
                    return;
                }

                string path = ResolvePoPath(localization, languageCode);
                if (string.IsNullOrEmpty(path))
                {
                    SocAccessPlugin.Instance?.LogInfo(
                        "No mod translation file found for language '" + languageCode + "' in " + string.Join(", ", GetTranslationsDirectories()));
                    return;
                }

                try
                {
                    _loadedCatalog = PoTranslationCatalog.Load(path);
                    SocAccessPlugin.Instance?.LogInfo("Loaded mod translations from " + Path.GetFileName(path));
                }
                catch (Exception exception)
                {
                    SocAccessPlugin.Instance?.LogWarning(
                        "Failed to load mod translations from " + path + ": " + exception.Message);
                }
            }
        }

        public static PoTranslationCatalog GetCatalog(ILocalizationHandler localization)
        {
            string languageCode = GetLanguageCode(localization);
            lock (LockObject)
            {
                if (string.IsNullOrEmpty(languageCode)
                    || string.IsNullOrEmpty(_loadedLanguageCode)
                    || !languageCode.Equals(_loadedLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return _loadedCatalog;
            }
        }

        public static void Reset()
        {
            lock (LockObject)
            {
                _loadedLanguageCode = null;
                _loadedCatalog = null;
            }
        }

        private static string ResolvePoPath(ILocalizationHandler localization, string languageCode)
        {
            string[] directories = GetTranslationsDirectories();
            string[] candidates = GetLanguageCandidates(localization, languageCode);
            for (int directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++)
            {
                string directory = directories[directoryIndex];
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    continue;
                }

                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    string candidate = candidates[candidateIndex];
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    string path = Path.Combine(directory, candidate + ".po");
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        private static string[] GetLanguageCandidates(ILocalizationHandler localization, string languageCode)
        {
            List<string> candidates = new List<string>();
            AddCandidate(candidates, languageCode);
            AddCandidate(candidates, languageCode.ToLowerInvariant());
            AddBaseLanguageCandidate(candidates, languageCode);

            if (localization != null && localization.CurrentLanguage != null)
            {
                AddCandidate(candidates, localization.CurrentLanguage.I2LanguageName);
                AddCandidate(candidates, localization.CurrentLanguage.Language.ToString());
            }

            return candidates.ToArray();
        }

        private static void AddBaseLanguageCandidate(List<string> candidates, string languageCode)
        {
            int dash = languageCode.IndexOf('-');
            int underscore = languageCode.IndexOf('_');
            int separator = dash >= 0 && underscore >= 0
                ? Math.Min(dash, underscore)
                : Math.Max(dash, underscore);
            if (separator > 0)
            {
                AddCandidate(candidates, languageCode.Substring(0, separator));
            }
        }

        private static void AddCandidate(List<string> candidates, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            string normalized = candidate.Trim();
            if (!candidates.Contains(normalized))
            {
                candidates.Add(normalized);
            }
        }

        private static string[] GetTranslationsDirectories()
        {
            List<string> directories = new List<string>();
            AddDirectory(directories, GetConfigTranslationsDirectory());
            AddDirectory(directories, GetAssemblyTranslationsDirectory());
            return directories.ToArray();
        }

        private static string GetConfigTranslationsDirectory()
        {
            try
            {
                if (!string.IsNullOrEmpty(Paths.ConfigPath))
                {
                    return Path.Combine(Paths.ConfigPath, ConfigDirectoryName, TranslationsDirectoryName);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string GetAssemblyTranslationsDirectory()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = string.IsNullOrEmpty(assemblyPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(assemblyPath);
            return Path.Combine(assemblyDirectory, TranslationsDirectoryName);
        }

        private static void AddDirectory(List<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!directories.Contains(directory))
            {
                directories.Add(directory);
            }
        }

        private static string GetLanguageCode(ILocalizationHandler localization)
        {
            if (localization == null || localization.CurrentLanguage == null)
            {
                return null;
            }

            return localization.CurrentLanguage.LanguageCode;
        }

        private static bool IsEnglish(string languageCode)
        {
            return languageCode.Equals("en", StringComparison.OrdinalIgnoreCase)
                || languageCode.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
                || languageCode.StartsWith("en_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
