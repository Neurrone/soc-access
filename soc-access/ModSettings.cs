using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess
{
    public static class ModSettings
    {
        public const int CueVolumeMinimum = 0;
        public const int CueVolumeMaximum = 100;
        public const int CueVolumeDefault = 30;
        public const int CuePitchSemitonesMinimum = -12;
        public const int CuePitchSemitonesMaximum = 12;
        public const int CuePitchSemitonesDefault = 0;
        public const int CueDurationScaleMinimum = 50;
        public const int CueDurationScaleMaximum = 200;
        public const int CueDurationScaleDefault = 100;

        private const string AudioSection = "Audio";

        private static ConfigFile _config;
        private static ConfigEntry<bool> _readEnemyInfluence;
        private static ConfigEntry<bool> _readStoryCameraFocusChanges;
        private static ConfigEntry<bool> _tileCuesEnabled;
        private static ConfigEntry<bool> _scannerUsesLongDirections;
        private static ConfigEntry<bool> _adventureMapUsesLongRoadDirections;
        private static readonly Dictionary<string, AnnouncementGroupConfig> _announcementGroups =
            new Dictionary<string, AnnouncementGroupConfig>();
        private static readonly Dictionary<string, AudioCueConfig> _audioCues =
            new Dictionary<string, AudioCueConfig>();
        private static readonly Dictionary<string, ScannerCustomCategoryConfig> _scannerCustomCategories =
            new Dictionary<string, ScannerCustomCategoryConfig>();

        public static bool ReadEnemyInfluence
        {
            get { return _readEnemyInfluence == null || _readEnemyInfluence.Value; }
        }

        public static bool ReadStoryCameraFocusChanges
        {
            get { return _readStoryCameraFocusChanges == null || _readStoryCameraFocusChanges.Value; }
        }

        public static bool TileCuesEnabled
        {
            get { return _tileCuesEnabled == null || _tileCuesEnabled.Value; }
        }

        public static bool ScannerUsesLongDirections
        {
            get { return _scannerUsesLongDirections != null && _scannerUsesLongDirections.Value; }
        }

        public static bool AdventureMapUsesLongRoadDirections
        {
            get { return _adventureMapUsesLongRoadDirections != null && _adventureMapUsesLongRoadDirections.Value; }
        }

        public static void Bind(ConfigFile config)
        {
            _config = config;
            _readEnemyInfluence = config.Bind(
                "Combat",
                "ReadEnemyInfluence",
                true,
                "Whether combat tile speech should include enemy influence information.");
            _readStoryCameraFocusChanges = config.Bind(
                "Story",
                "ReadStoryCameraFocusChanges",
                true,
                "Whether story camera focus change events should be read.");
            _tileCuesEnabled = config.Bind(
                AudioSection,
                "TileCuesEnabled",
                true,
                "Whether cursor movement plays synthesised tile sound cues.");
            _scannerUsesLongDirections = config.Bind(
                "Scanner",
                "ScannerUsesLongDirections",
                false,
                "Whether spoken directions use the long form (\"3 northeast\") instead of the short form (\"3ne\").");
            _adventureMapUsesLongRoadDirections = config.Bind(
                "AdventureMap",
                "UseLongRoadDirections",
                false,
                "Whether road directions use the long form (\"east west\") instead of the short form (\"e w\").");
            BindAnnouncementGroups(config);
            BindAudioCues(config);
            BindScannerCustomCategories(config);
        }

        public static void SetReadEnemyInfluence(bool value)
        {
            if (_readEnemyInfluence == null)
            {
                return;
            }

            _readEnemyInfluence.Value = value;
            _config?.Save();
        }

        public static void SetReadStoryCameraFocusChanges(bool value)
        {
            if (_readStoryCameraFocusChanges == null)
            {
                return;
            }

            _readStoryCameraFocusChanges.Value = value;
            _config?.Save();
        }

        public static void SetTileCuesEnabled(bool value)
        {
            if (_tileCuesEnabled == null)
            {
                return;
            }

            _tileCuesEnabled.Value = value;
            _config?.Save();
        }

        public static bool GetCueEnabled(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config == null || config.Enabled == null || config.Enabled.Value;
        }

        public static int GetCueVolume(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.Volume != null ? config.Volume.Value : CueVolumeDefault;
        }

        public static int GetCuePitchSemitones(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.PitchSemitones != null
                ? config.PitchSemitones.Value
                : CuePitchSemitonesDefault;
        }

        public static int GetCueDurationScale(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.DurationScale != null
                ? config.DurationScale.Value
                : CueDurationScaleDefault;
        }

        public static void SetCueEnabled(string cueKey, bool value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.Enabled == null)
            {
                return;
            }

            config.Enabled.Value = value;
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCueVolume(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.Volume == null)
            {
                return;
            }

            config.Volume.Value = Clamp(value, CueVolumeMinimum, CueVolumeMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCuePitchSemitones(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.PitchSemitones == null)
            {
                return;
            }

            config.PitchSemitones.Value = Clamp(value, CuePitchSemitonesMinimum, CuePitchSemitonesMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCueDurationScale(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.DurationScale == null)
            {
                return;
            }

            config.DurationScale.Value = Clamp(value, CueDurationScaleMinimum, CueDurationScaleMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        public static void ResetCue(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null)
            {
                return;
            }

            if (config.Enabled != null)
            {
                config.Enabled.Value = true;
            }

            if (config.Volume != null)
            {
                config.Volume.Value = CueVolumeDefault;
            }

            if (config.PitchSemitones != null)
            {
                config.PitchSemitones.Value = CuePitchSemitonesDefault;
            }

            if (config.DurationScale != null)
            {
                config.DurationScale.Value = CueDurationScaleDefault;
            }

            SaveAndInvalidateCue(cueKey);
        }

        public static void SetAdventureMapUsesLongRoadDirections(bool value)
        {
            if (_adventureMapUsesLongRoadDirections == null)
            {
                return;
            }

            _adventureMapUsesLongRoadDirections.Value = value;
            _config?.Save();
        }

        public static void SetScannerUsesLongDirections(bool value)
        {
            if (_scannerUsesLongDirections == null)
            {
                return;
            }

            _scannerUsesLongDirections.Value = value;
            _config?.Save();
        }

        public static IReadOnlyList<string> GetAnnouncementOrder(AnnouncementGroupDefinition group)
        {
            if (group == null)
            {
                return new string[0];
            }

            AnnouncementGroupConfig config = GetAnnouncementConfig(group);
            string orderCsv = config != null && config.Order != null
                ? config.Order.Value
                : group.DefaultOrderCsv;
            return MergeAnnouncementOrder(group, orderCsv);
        }

        public static bool GetAnnouncementElementEnabled(AnnouncementGroupDefinition group, AnnouncementElementDefinition element)
        {
            AnnouncementElementConfig config = GetAnnouncementElementConfig(group, element);
            return config != null && config.Enabled != null ? config.Enabled.Value : element != null && element.DefaultEnabled;
        }

        public static bool GetAnnouncementElementSuffix(AnnouncementGroupDefinition group, AnnouncementElementDefinition element)
        {
            AnnouncementElementConfig config = GetAnnouncementElementConfig(group, element);
            return config != null && config.Suffix != null ? config.Suffix.Value : element != null && element.DefaultSuffix;
        }

        public static void SetAnnouncementElementEnabled(AnnouncementGroupDefinition group, AnnouncementElementDefinition element, bool value)
        {
            AnnouncementElementConfig config = GetAnnouncementElementConfig(group, element);
            if (config == null || config.Enabled == null)
            {
                return;
            }

            config.Enabled.Value = value;
            _config?.Save();
        }

        public static void SetAnnouncementElementSuffix(AnnouncementGroupDefinition group, AnnouncementElementDefinition element, bool value)
        {
            AnnouncementElementConfig config = GetAnnouncementElementConfig(group, element);
            if (config == null || config.Suffix == null)
            {
                return;
            }

            config.Suffix.Value = value;
            _config?.Save();
        }

        public static bool MoveAnnouncementElement(AnnouncementGroupDefinition group, string key, int delta)
        {
            if (group == null || string.IsNullOrWhiteSpace(key) || delta == 0)
            {
                return false;
            }

            List<string> order = GetAnnouncementOrder(group).ToList();
            int index = order.IndexOf(key);
            int targetIndex = index + delta;
            if (index < 0 || targetIndex < 0 || targetIndex >= order.Count)
            {
                return false;
            }

            order.RemoveAt(index);
            order.Insert(targetIndex, key);
            SetAnnouncementOrder(group, order);
            return true;
        }

        public static void ResetAnnouncementElement(AnnouncementGroupDefinition group, AnnouncementElementDefinition element)
        {
            AnnouncementElementConfig config = GetAnnouncementElementConfig(group, element);
            if (config == null || element == null)
            {
                return;
            }

            if (config.Enabled != null)
            {
                config.Enabled.Value = element.DefaultEnabled;
            }

            if (config.Suffix != null)
            {
                config.Suffix.Value = element.DefaultSuffix;
            }

            _config?.Save();
        }

        public static void ResetAnnouncementGroup(AnnouncementGroupDefinition group)
        {
            AnnouncementGroupConfig config = GetAnnouncementConfig(group);
            if (config == null || group == null)
            {
                return;
            }

            if (config.Order != null)
            {
                config.Order.Value = group.DefaultOrderCsv;
            }

            for (int i = 0; i < group.Elements.Count; i++)
            {
                ResetAnnouncementElement(group, group.Elements[i]);
            }

            _config?.Save();
        }

        public static IReadOnlyList<ScannerCustomCategory> GetScannerCustomCategories(string taxonomyKey)
        {
            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            return list != null ? list.Categories : new ScannerCustomCategory[0];
        }

        public static ScannerCustomCategory GetScannerCustomCategory(string taxonomyKey, int id)
        {
            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            return list != null ? list.Get(id) : null;
        }

        /// <summary>
        /// The caller supplies the starting name because the wording is
        /// localized accessibility text, and it is stored rather than derived so
        /// it stays put when an earlier category is deleted.
        /// </summary>
        public static ScannerCustomCategory AddScannerCustomCategory(string taxonomyKey, Func<int, string> nameForPosition)
        {
            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            if (list == null)
            {
                return null;
            }

            ScannerCustomCategory category = list.Add(nameForPosition);
            if (SupportsScannerQuickKeys(taxonomyKey))
            {
                category.SetQuickKey(list.FirstFreeQuickKey());
            }

            SaveScannerCustomCategories(taxonomyKey, list);
            return category;
        }

        /// <summary>
        /// Only the adventure map walks a custom category from a single key, so
        /// only its categories are given one. Battle keeps those keys for the
        /// acting and enemy troop cycles.
        /// </summary>
        public static bool SupportsScannerQuickKeys(string taxonomyKey)
        {
            return taxonomyKey == ScannerTaxonomyKeys.Adventure;
        }

        public static ScannerCustomCategory GetScannerCustomCategoryByQuickKey(string taxonomyKey, ScannerQuickKey quickKey)
        {
            if (!SupportsScannerQuickKeys(taxonomyKey))
            {
                return null;
            }

            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            return list != null ? list.GetByQuickKey(quickKey) : null;
        }

        public static bool SetScannerCustomCategoryQuickKey(string taxonomyKey, int id, ScannerQuickKey quickKey)
        {
            if (!SupportsScannerQuickKeys(taxonomyKey))
            {
                return false;
            }

            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            if (list == null || !list.SetQuickKey(id, quickKey))
            {
                return false;
            }

            SaveScannerCustomCategories(taxonomyKey, list);
            return true;
        }

        public static bool RemoveScannerCustomCategory(string taxonomyKey, int id)
        {
            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            if (list == null || !list.Remove(id))
            {
                return false;
            }

            SaveScannerCustomCategories(taxonomyKey, list);
            return true;
        }

        public static bool RenameScannerCustomCategory(string taxonomyKey, int id, string name)
        {
            return MutateScannerCustomCategory(taxonomyKey, id, category => category.Rename(name));
        }

        public static bool SetScannerCustomCategorySelector(
            string taxonomyKey,
            int id,
            string categoryKey,
            string subcategoryKey,
            bool selected)
        {
            return MutateScannerCustomCategory(
                taxonomyKey,
                id,
                category => category.SetSelector(categoryKey, subcategoryKey, selected));
        }

        public static bool AddScannerCustomCategoryKeyword(string taxonomyKey, int id, string keyword)
        {
            return MutateScannerCustomCategory(taxonomyKey, id, category => category.AddKeyword(keyword));
        }

        public static bool RemoveScannerCustomCategoryKeyword(string taxonomyKey, int id, string keyword)
        {
            return MutateScannerCustomCategory(taxonomyKey, id, category => category.RemoveKeyword(keyword));
        }

        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
            _readStoryCameraFocusChanges = null;
            _tileCuesEnabled = null;
            _scannerUsesLongDirections = null;
            _adventureMapUsesLongRoadDirections = null;
            _announcementGroups.Clear();
            _audioCues.Clear();
            _scannerCustomCategories.Clear();
        }

        private static void BindAudioCues(ConfigFile config)
        {
            _audioCues.Clear();
            IReadOnlyList<CueDefinition> cues = CueLibrary.AllCues;
            for (int i = 0; i < cues.Count; i++)
            {
                string cueKey = cues[i].Key;
                string prefix = ToConfigKeyPrefix(cueKey);
                _audioCues[cueKey] = new AudioCueConfig
                {
                    Enabled = config.Bind(
                        AudioSection,
                        prefix + "Enabled",
                        true,
                        "Whether this sound cue is played."),
                    Volume = config.Bind(
                        AudioSection,
                        prefix + "Volume",
                        CueVolumeDefault,
                        "Playback volume of this sound cue, from 0 to 100."),
                    PitchSemitones = config.Bind(
                        AudioSection,
                        prefix + "PitchSemitones",
                        CuePitchSemitonesDefault,
                        "Pitch offset of this sound cue in semitones, from -12 to 12."),
                    DurationScale = config.Bind(
                        AudioSection,
                        prefix + "DurationScale",
                        CueDurationScaleDefault,
                        "Length of this sound cue as a percentage of its default, from 50 to 200.")
                };
            }
        }

        private static AudioCueConfig GetAudioCueConfig(string cueKey)
        {
            if (string.IsNullOrWhiteSpace(cueKey))
            {
                return null;
            }

            AudioCueConfig config;
            return _audioCues.TryGetValue(cueKey, out config) ? config : null;
        }

        private static void SaveAndInvalidateCue(string cueKey)
        {
            _config?.Save();
            SynthCuePlayer.InvalidateCache(cueKey);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private static void BindScannerCustomCategories(ConfigFile config)
        {
            _scannerCustomCategories.Clear();
            for (int i = 0; i < ScannerTaxonomyKeys.All.Length; i++)
            {
                string taxonomyKey = ScannerTaxonomyKeys.All[i];
                _scannerCustomCategories[taxonomyKey] = new ScannerCustomCategoryConfig
                {
                    Entry = config.Bind(
                        "Scanner",
                        ToConfigKeyPrefix(taxonomyKey) + "CustomCategories",
                        string.Empty,
                        "Player-defined scanner categories for this context. Edited through the mod settings screen.")
                };
            }
        }

        /// <summary>
        /// Decoded once per context and kept, because these are player settings
        /// rather than game state: nothing outside this class can change them
        /// between reads.
        /// </summary>
        private static ScannerCustomCategoryList GetScannerCustomCategoryList(string taxonomyKey)
        {
            ScannerCustomCategoryConfig config = GetScannerCustomCategoryConfig(taxonomyKey);
            if (config == null)
            {
                return null;
            }

            if (config.List == null)
            {
                config.List = ScannerCustomCategoryCodec.Decode(config.Entry != null ? config.Entry.Value : null);
            }

            return config.List;
        }

        private static ScannerCustomCategoryConfig GetScannerCustomCategoryConfig(string taxonomyKey)
        {
            if (string.IsNullOrWhiteSpace(taxonomyKey))
            {
                return null;
            }

            ScannerCustomCategoryConfig config;
            return _scannerCustomCategories.TryGetValue(taxonomyKey, out config) ? config : null;
        }

        private static bool MutateScannerCustomCategory(
            string taxonomyKey,
            int id,
            Func<ScannerCustomCategory, bool> mutate)
        {
            ScannerCustomCategoryList list = GetScannerCustomCategoryList(taxonomyKey);
            ScannerCustomCategory category = list != null ? list.Get(id) : null;
            if (category == null || mutate == null || !mutate(category))
            {
                return false;
            }

            SaveScannerCustomCategories(taxonomyKey, list);
            return true;
        }

        private static void SaveScannerCustomCategories(string taxonomyKey, ScannerCustomCategoryList list)
        {
            ScannerCustomCategoryConfig config = GetScannerCustomCategoryConfig(taxonomyKey);
            if (config == null || config.Entry == null)
            {
                return;
            }

            config.Entry.Value = ScannerCustomCategoryCodec.Encode(list);
            _config?.Save();
        }

        private static void BindAnnouncementGroups(ConfigFile config)
        {
            _announcementGroups.Clear();
            IReadOnlyList<AnnouncementGroupDefinition> groups = AnnouncementDefinitions.All;
            for (int i = 0; i < groups.Count; i++)
            {
                AnnouncementGroupDefinition group = groups[i];
                AnnouncementGroupConfig groupConfig = new AnnouncementGroupConfig();
                // Configs written before versioning existed have no Version key, and
                // Bind returns the default for a missing key. The default must be 1,
                // not group.Version, or those configs would be treated as already
                // migrated and DiscardOutdatedAnnouncementSettings would never fire.
                groupConfig.Version = config.Bind(
                    group.ConfigSection,
                    "Version",
                    1,
                    "Layout version of this announcement group. Saved settings are reset when it changes.");
                groupConfig.Order = config.Bind(
                    group.ConfigSection,
                    "Order",
                    group.DefaultOrderCsv,
                    "Comma-separated order of announcement element keys.");

                for (int elementIndex = 0; elementIndex < group.Elements.Count; elementIndex++)
                {
                    AnnouncementElementDefinition element = group.Elements[elementIndex];
                    string prefix = ToConfigKeyPrefix(element.Key);
                    groupConfig.Elements[element.Key] = new AnnouncementElementConfig
                    {
                        Enabled = config.Bind(
                            group.ConfigSection,
                            prefix + "Enabled",
                            element.DefaultEnabled,
                            "Whether this announcement element is included."),
                        Suffix = config.Bind(
                            group.ConfigSection,
                            prefix + "Suffix",
                            element.DefaultSuffix,
                            "Whether this announcement element includes a comma suffix before the next element.")
                    };
                }

                _announcementGroups[group.Key] = groupConfig;
                DiscardOutdatedAnnouncementSettings(group, groupConfig);
            }
        }

        /// <summary>
        /// A group whose element set has been redesigned cannot honour the old
        /// saved settings: every stored key is retired, so the saved order would
        /// contribute nothing and the player would be left with whatever the
        /// merge happened to produce. Throw the saved values away and re-stamp
        /// the version instead.
        /// </summary>
        private static void DiscardOutdatedAnnouncementSettings(
            AnnouncementGroupDefinition group,
            AnnouncementGroupConfig groupConfig)
        {
            if (groupConfig.Version == null || groupConfig.Version.Value == group.Version)
            {
                return;
            }

            if (groupConfig.Order != null)
            {
                groupConfig.Order.Value = group.DefaultOrderCsv;
            }

            for (int i = 0; i < group.Elements.Count; i++)
            {
                AnnouncementElementDefinition element = group.Elements[i];
                AnnouncementElementConfig elementConfig;
                if (!groupConfig.Elements.TryGetValue(element.Key, out elementConfig))
                {
                    continue;
                }

                if (elementConfig.Enabled != null)
                {
                    elementConfig.Enabled.Value = element.DefaultEnabled;
                }

                if (elementConfig.Suffix != null)
                {
                    elementConfig.Suffix.Value = element.DefaultSuffix;
                }
            }

            groupConfig.Version.Value = group.Version;
        }

        private static AnnouncementGroupConfig GetAnnouncementConfig(AnnouncementGroupDefinition group)
        {
            if (group == null)
            {
                return null;
            }

            AnnouncementGroupConfig config;
            return _announcementGroups.TryGetValue(group.Key, out config) ? config : null;
        }

        private static AnnouncementElementConfig GetAnnouncementElementConfig(
            AnnouncementGroupDefinition group,
            AnnouncementElementDefinition element)
        {
            AnnouncementGroupConfig groupConfig = GetAnnouncementConfig(group);
            if (groupConfig == null || element == null)
            {
                return null;
            }

            AnnouncementElementConfig elementConfig;
            return groupConfig.Elements.TryGetValue(element.Key, out elementConfig) ? elementConfig : null;
        }

        private static void SetAnnouncementOrder(AnnouncementGroupDefinition group, IReadOnlyList<string> order)
        {
            AnnouncementGroupConfig config = GetAnnouncementConfig(group);
            if (config == null || config.Order == null)
            {
                return;
            }

            config.Order.Value = string.Join(",", order.ToArray());
            _config?.Save();
        }

        public static IReadOnlyList<string> MergeAnnouncementOrder(AnnouncementGroupDefinition group, string orderCsv)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(orderCsv))
            {
                string[] keys = orderCsv.Split(',');
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i].Trim();
                    if (group.GetElement(key) != null && seen.Add(key))
                    {
                        result.Add(key);
                    }
                }
            }

            for (int i = 0; i < group.Elements.Count; i++)
            {
                string key = group.Elements[i].Key;
                if (seen.Contains(key))
                {
                    continue;
                }

                if (ShouldAppendMissingAnnouncementElement(group, key))
                {
                    result.Add(key);
                    seen.Add(key);
                    continue;
                }

                int insertAt = 0;
                for (int previous = i - 1; previous >= 0; previous--)
                {
                    string neighbor = group.Elements[previous].Key;
                    if (seen.Contains(neighbor))
                    {
                        insertAt = result.IndexOf(neighbor) + 1;
                        break;
                    }
                }

                result.Insert(insertAt, key);
                seen.Add(key);
            }

            return result;
        }

        private static bool ShouldAppendMissingAnnouncementElement(AnnouncementGroupDefinition group, string key)
        {
            if (group == null || key != Speech.Spatial.AdventureMapAnnouncementDefinitions.TileKeys.MovementCost)
            {
                return false;
            }

            return group == Speech.Spatial.AdventureMapAnnouncementDefinitions.Tile
                || group == Speech.Spatial.AdventureMapAnnouncementDefinitions.ScannerContent;
        }

        private static string ToConfigKeyPrefix(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string[] parts = key.Split('_');
            string result = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                result += char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return result + ".";
        }

        private sealed class AnnouncementGroupConfig
        {
            public ConfigEntry<int> Version { get; set; }
            public ConfigEntry<string> Order { get; set; }
            public Dictionary<string, AnnouncementElementConfig> Elements { get; private set; } =
                new Dictionary<string, AnnouncementElementConfig>();
        }

        private sealed class AnnouncementElementConfig
        {
            public ConfigEntry<bool> Enabled { get; set; }
            public ConfigEntry<bool> Suffix { get; set; }
        }

        private sealed class AudioCueConfig
        {
            public ConfigEntry<bool> Enabled { get; set; }
            public ConfigEntry<int> Volume { get; set; }
            public ConfigEntry<int> PitchSemitones { get; set; }
            public ConfigEntry<int> DurationScale { get; set; }
        }

        private sealed class ScannerCustomCategoryConfig
        {
            public ConfigEntry<string> Entry { get; set; }
            public ScannerCustomCategoryList List { get; set; }
        }
    }
}
