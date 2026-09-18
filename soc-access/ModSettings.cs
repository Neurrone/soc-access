using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// What the "read usage hints" setting can say. Stored as a string rather than a flag because
    /// the answer has more than two useful values in it - a third, "only where the control has
    /// nothing else to say", is the obvious next one - and a flag would have to be migrated the day
    /// one is added. A value this list does not name reads as <see cref="Always"/> and is left on
    /// disk untouched, so a config written by a later build survives a run of this one.
    /// </summary>
    public static class UsageHintReading
    {
        public const string Always = "always";
        public const string Never = "never";
    }

    /// <summary>
    /// What the "Sort scanner results by" setting can say: the straight line between two tiles, or
    /// what the wielder would pay to walk there. Stored as a string for the same reason the
    /// usage-hint setting is: a value this list does not name reads as <see cref="StraightLine"/>
    /// and is left on disk untouched, so a config written by a later build survives a run of this
    /// one.
    /// </summary>
    public static class ScannerResultOrders
    {
        public const string StraightLine = "StraightLine";
        public const string WalkablePath = "WalkablePath";
    }

    public static partial class ModSettings
    {
        public const int CueVolumeMinimum = 0;
        public const int CueVolumeMaximum = 100;
        public const int CueVolumeDefault = 30;
        /// <summary>Full: the loudness a beacon had before it had a volume setting.</summary>
        public const int BeaconVolumeDefault = 100;
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
        private static ConfigEntry<int> _beaconVolume;
        private static ConfigEntry<bool> _scannerUsesLongDirections;
        private static ConfigEntry<string> _scannerResultOrder;
        private static ConfigEntry<bool> _adventureMapUsesLongRoadDirections;
        private static ConfigEntry<bool> _readLongTooltips;
        private static ConfigEntry<string> _readUsageHints;
        private static readonly Dictionary<string, AnnouncementGroupConfig> _announcementGroups =
            new Dictionary<string, AnnouncementGroupConfig>();
        private static readonly Dictionary<string, AudioCueConfig> _audioCues =
            new Dictionary<string, AudioCueConfig>();
        private static readonly Dictionary<string, ScannerCustomCategoryConfig> _scannerCustomCategories =
            new Dictionary<string, ScannerCustomCategoryConfig>();
        private const string KeybindsSection = "Keybinds";
        private static readonly Dictionary<string, KeybindConfig> _keybinds =
            new Dictionary<string, KeybindConfig>();

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

        /// <summary>Which distance the scanner orders its results by - one of
        /// <see cref="ScannerResultOrders"/>'s values. The directions spoken for a result are the
        /// straight line either way.</summary>
        public static string ScannerResultOrder
        {
            get
            {
                string value = _scannerResultOrder == null ? null : _scannerResultOrder.Value;
                return value == ScannerResultOrders.WalkablePath
                    ? ScannerResultOrders.WalkablePath
                    : ScannerResultOrders.StraightLine;
            }
        }

        public static bool ScannerSortsByWalkablePath
        {
            get { return ScannerResultOrder == ScannerResultOrders.WalkablePath; }
        }

        public static bool AdventureMapUsesLongRoadDirections
        {
            get { return _adventureMapUsesLongRoadDirections != null && _adventureMapUsesLongRoadDirections.Value; }
        }

        /// <summary>Whether a LONG tooltip - the game's wielder and troop dossiers - is read out on
        /// focus. A short tooltip is always read; this setting governs only the long ones, which stay
        /// in the review buffer either way.</summary>
        public static bool ReadLongTooltips
        {
            get { return _readLongTooltips == null || _readLongTooltips.Value; }
        }

        /// <summary>Whether a control's usage hints are spoken on focus - one of
        /// <see cref="UsageHintReading"/>'s values. The hints are in the review buffer whatever this
        /// says.</summary>
        public static string ReadUsageHints
        {
            get
            {
                string value = _readUsageHints == null ? null : _readUsageHints.Value;
                return value == UsageHintReading.Never ? UsageHintReading.Never : UsageHintReading.Always;
            }
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
            _beaconVolume = config.Bind(
                AudioSection,
                "BeaconVolume",
                BeaconVolumeDefault,
                "Playback volume of the bookmark beacon, from 0 to 100.");
            _scannerUsesLongDirections = config.Bind(
                "Scanner",
                "ScannerUsesLongDirections",
                false,
                "Whether spoken directions use the long form (\"3 northeast\") instead of the short form (\"3ne\").");
            _scannerResultOrder = config.Bind(
                "AdventureMap",
                "ScannerResultOrder",
                ScannerResultOrders.StraightLine,
                "Which distance scanner results are ordered by: \"StraightLine\" or \"WalkablePath\".");
            _adventureMapUsesLongRoadDirections = config.Bind(
                "AdventureMap",
                "UseLongRoadDirections",
                false,
                "Whether road directions use the long form (\"east west\") instead of the short form (\"e w\").");
            _readLongTooltips = config.Bind(
                "Tooltips",
                "ReadLongTooltips",
                true,
                "Whether long tooltips like wielder and troop information are automatically read.");
            // A plain string entry, not an AcceptableValueList: the list COERCES a value it does not
            // know back to the default and writes the correction to disk, which would throw away a
            // value a later build wrote. The reader above tolerates it instead.
            _readUsageHints = config.Bind(
                "Speech",
                "ReadUsageHints",
                UsageHintReading.Always,
                "Whether usage hints in buffers are automatically read: \"always\" or \"never\".");
            BindAnnouncementGroups(config);
            BindAudioCues(config);
            BindScannerCustomCategories(config);
            BindKeybinds(config);
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

        public static void SetReadLongTooltips(bool value)
        {
            if (_readLongTooltips == null)
            {
                return;
            }

            _readLongTooltips.Value = value;
            _config?.Save();
        }

        public static void SetReadUsageHints(string value)
        {
            if (_readUsageHints == null)
            {
                return;
            }

            _readUsageHints.Value = value;
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


        public static void SetAdventureMapUsesLongRoadDirections(bool value)
        {
            if (_adventureMapUsesLongRoadDirections == null)
            {
                return;
            }

            _adventureMapUsesLongRoadDirections.Value = value;
            _config?.Save();
        }

        public static void SetScannerResultOrder(string value)
        {
            if (_scannerResultOrder == null)
            {
                return;
            }

            _scannerResultOrder.Value = value;
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


        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
            _readStoryCameraFocusChanges = null;
            _tileCuesEnabled = null;
            _beaconVolume = null;
            _scannerUsesLongDirections = null;
            _scannerResultOrder = null;
            _adventureMapUsesLongRoadDirections = null;
            _readLongTooltips = null;
            _readUsageHints = null;
            _announcementGroups.Clear();
            _audioCues.Clear();
            _scannerCustomCategories.Clear();
            _keybinds.Clear();
        }


        /// <summary>The shape every one of the config lookups has: no key, or a key nothing was
        /// bound under, means no config.</summary>
        private static T Lookup<T>(Dictionary<string, T> configs, string key)
            where T : class
        {
            T config;
            return !string.IsNullOrWhiteSpace(key) && configs.TryGetValue(key, out config) ? config : null;
        }


        private static string ToConfigKeyPrefix(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string[] parts = key.Split('_');
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                result.Append(char.ToUpperInvariant(parts[i][0])).Append(parts[i], 1, parts[i].Length - 1);
            }

            return result.Append('.').ToString();
        }

    }
}
