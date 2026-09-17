using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Battlefields
{
    /// <summary>What was written about one battlefield layout, as the table holds it: the game
    /// authors none of this, so the three fields are raw text and the wording around them - the
    /// labels, the order, what is said when there is nothing - belongs to the screen that reads
    /// them.</summary>
    public sealed class BattlefieldDescription
    {
        public BattlefieldDescription(string terrain, string attacker, string defender)
        {
            Terrain = terrain;
            Attacker = attacker;
            Defender = defender;
        }

        public string Terrain { get; private set; }

        public string Attacker { get; private set; }

        public string Defender { get; private set; }
    }

    /// <summary>One language's whole table, keyed by "&lt;LevelType&gt;/&lt;PathName&gt;" - the
    /// folder the dump loaded the layout from and the path it loaded it by.
    ///
    /// The runtime's key is built from <c>MapFormat.Metadata</c>, and its <c>Type</c> is whatever the
    /// level editor's author set it to - nothing stamps it on load - so a campaign battlefield saved
    /// as "Adventure" asks for "Adventure/Arleon1GoldMine" while the table holds
    /// "BattleCampaign/Arleon1GoldMine" (found 2026-09-17 on the first campaign mission's gold mine
    /// fight, where the page read no description at all). Path names are unique across every
    /// layout the game ships (82 of 82 in the dump), so a key that misses is matched by its path
    /// alone.</summary>
    public sealed class BattlefieldDescriptionTable
    {
        private readonly Dictionary<string, BattlefieldDescription> _layouts;
        private readonly Dictionary<string, BattlefieldDescription> _byPath;

        public BattlefieldDescriptionTable(string language, Dictionary<string, BattlefieldDescription> layouts)
        {
            Language = language;
            _layouts = layouts ?? new Dictionary<string, BattlefieldDescription>(StringComparer.Ordinal);
            _byPath = new Dictionary<string, BattlefieldDescription>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BattlefieldDescription> layout in _layouts)
            {
                string path = PathOf(layout.Key);
                if (path != null && !_byPath.ContainsKey(path))
                {
                    _byPath.Add(path, layout.Value);
                }
            }
        }

        /// <summary>The path half of a "&lt;LevelType&gt;/&lt;PathName&gt;" key; null for a key with
        /// no type in front.</summary>
        private static string PathOf(string key)
        {
            int slash = string.IsNullOrEmpty(key) ? -1 : key.LastIndexOf('/');
            return slash < 0 || slash == key.Length - 1 ? null : key.Substring(slash + 1);
        }

        public string Language { get; private set; }

        public int Count
        {
            get { return _layouts.Count; }
        }

        public bool TryGet(string key, out BattlefieldDescription description)
        {
            description = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_layouts.TryGetValue(key, out description))
            {
                return true;
            }

            string path = PathOf(key);
            return path != null && _byPath.TryGetValue(path, out description);
        }
    }

    /// <summary>
    /// THE AUTHORED DESCRIPTIONS OF THE BATTLEFIELD LAYOUTS, read from the table
    /// <c>build-battlefields.ps1</c> built: <c>battlefields\&lt;languageCode&gt;.json</c> beside the
    /// mod's config, then beside the assembly, exactly where <c>ModTranslationLoader</c> looks for a
    /// <c>.po</c>. A language with no table of its own falls back to English rather than falling
    /// silent, since a described layout is worth hearing in a second language.
    ///
    /// The loaded table is cached on the language code READ FROM THE GAME each time it is asked
    /// (AGENTS.md, Screen Resolution: never on a hook), so a language change re-reads and a hot
    /// reload starts with nothing cached. A missing or malformed file is cached as a miss and says
    /// so in the log once, not once per frame.
    ///
    /// Newtonsoft rather than <c>DataContractJsonSerializer</c>, which
    /// <c>bookmarks/AdventureBookmarkStore.cs</c> uses: the table is an OBJECT whose keys are the
    /// layout keys, and the data-contract serializer writes and reads a dictionary as an array of
    /// key/value pairs instead, which this shape cannot be expressed in.
    /// </summary>
    public static class BattlefieldDescriptions
    {
        private const string BattlefieldsDirectoryName = "battlefields";
        private const string ConfigDirectoryName = "SongsOfConquestAccess";
        private const string FallbackLanguage = "en";

        private static readonly object LockObject = new object();
        private static string _loadedLanguageCode;
        private static BattlefieldDescriptionTable _loadedTable;
        private static bool _loaded;

        /// <summary>The description of the layout <paramref name="key"/> names, in the game's
        /// language where there is one and in English otherwise.</summary>
        public static bool TryGet(string key, out BattlefieldDescription description)
        {
            description = null;
            BattlefieldDescriptionTable table = Current();
            return table != null && table.TryGet(key, out description);
        }

        /// <summary>The table the game's current language reads from, loaded on the first ask and
        /// kept until the language changes or the mod is unloaded.</summary>
        public static BattlefieldDescriptionTable Current()
        {
            string languageCode = GetLanguageCode();
            lock (LockObject)
            {
                if (_loaded && string.Equals(_loadedLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
                {
                    return _loadedTable;
                }

                _loadedLanguageCode = languageCode;
                _loadedTable = Load(GetBattlefieldsDirectories(), languageCode);
                _loaded = true;
                return _loadedTable;
            }
        }

        public static void Reset()
        {
            lock (LockObject)
            {
                _loadedLanguageCode = null;
                _loadedTable = null;
                _loaded = false;
            }
        }

        /// <summary>The table for a language code, from the first directory that has one, falling
        /// back to English. Null when neither file is there or the one that is cannot be read.</summary>
        public static BattlefieldDescriptionTable Load(IEnumerable<string> directories, string languageCode)
        {
            string path = ResolvePath(directories, languageCode);
            if (path == null)
            {
                SocAccessMod.Instance?.LogInfo(
                    "No battlefield descriptions found for language '" + (languageCode ?? string.Empty) + "'");
                return null;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning(
                    "Failed to read battlefield descriptions from " + path + ": " + exception.Message);
                return null;
            }

            BattlefieldDescriptionTable table = Parse(json);
            if (table == null)
            {
                SocAccessMod.Instance?.LogWarning("Failed to parse battlefield descriptions from " + path);
                return null;
            }

            SocAccessMod.Instance?.LogInfo(
                "Loaded " + table.Count + " battlefield descriptions from " + Path.GetFileName(path));
            return table;
        }

        /// <summary>One table's JSON, or null where it is not a table: a malformed file answers
        /// nothing rather than throwing at whatever asked for a description.</summary>
        public static BattlefieldDescriptionTable Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            TableFile file;
            try
            {
                file = JsonConvert.DeserializeObject<TableFile>(json);
            }
            catch (Exception)
            {
                return null;
            }

            if (file == null || file.Layouts == null)
            {
                return null;
            }

            Dictionary<string, BattlefieldDescription> layouts =
                new Dictionary<string, BattlefieldDescription>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, LayoutFile> entry in file.Layouts)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
                {
                    continue;
                }

                layouts[entry.Key] = new BattlefieldDescription(
                    entry.Value.Terrain, entry.Value.Attacker, entry.Value.Defender);
            }

            return new BattlefieldDescriptionTable(file.Language, layouts);
        }

        private static string ResolvePath(IEnumerable<string> directories, string languageCode)
        {
            if (directories == null)
            {
                return null;
            }

            List<string> candidates = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(languageCode)
                && !languageCode.Trim().Equals(FallbackLanguage, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(languageCode.Trim());
            }

            candidates.Add(FallbackLanguage);

            foreach (string candidate in candidates)
            {
                foreach (string directory in directories)
                {
                    if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    {
                        continue;
                    }

                    string path = Path.Combine(directory, candidate + ".json");
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>The config folder first and the folder beside the assembly second, as
        /// <c>ModTranslationLoader.GetTranslationsDirectories</c> does.</summary>
        private static string[] GetBattlefieldsDirectories()
        {
            List<string> directories = new List<string>(2);
            AddDirectory(directories, GetConfigDirectory());
            AddDirectory(directories, GetAssemblyDirectory());
            return directories.ToArray();
        }

        private static string GetConfigDirectory()
        {
            try
            {
                if (!string.IsNullOrEmpty(Paths.ConfigPath))
                {
                    return Path.Combine(Paths.ConfigPath, ConfigDirectoryName, BattlefieldsDirectoryName);
                }
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning(
                    "Failed to resolve the config battlefields directory: " + exception.Message);
            }

            return null;
        }

        private static string GetAssemblyDirectory()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = string.IsNullOrEmpty(assemblyPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(assemblyPath);
            return Path.Combine(assemblyDirectory, BattlefieldsDirectoryName);
        }

        private static void AddDirectory(List<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || directories.Contains(directory))
            {
                return;
            }

            directories.Add(directory);
        }

        private static string GetLanguageCode()
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            return localization != null && localization.CurrentLanguage != null
                ? localization.CurrentLanguage.LanguageCode
                : null;
        }

        private sealed class TableFile
        {
            [JsonProperty("version")]
            public int Version { get; set; }

            [JsonProperty("language")]
            public string Language { get; set; }

            [JsonProperty("layouts")]
            public Dictionary<string, LayoutFile> Layouts { get; set; }
        }

        private sealed class LayoutFile
        {
            [JsonProperty("terrain")]
            public string Terrain { get; set; }

            [JsonProperty("attacker")]
            public string Attacker { get; set; }

            [JsonProperty("defender")]
            public string Defender { get; set; }
        }
    }
}
