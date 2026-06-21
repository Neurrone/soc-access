using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess
{
    internal static class ModSettings
    {
        private static ConfigFile _config;
        private static ConfigEntry<bool> _readEnemyInfluence;
        private static ConfigEntry<bool> _readStoryCameraFocusChanges;
        private static ConfigEntry<bool> _scannerPlaysDirectionalBeep;
        private static readonly Dictionary<string, AnnouncementGroupConfig> _announcementGroups =
            new Dictionary<string, AnnouncementGroupConfig>();

        public static bool ReadEnemyInfluence
        {
            get { return _readEnemyInfluence == null || _readEnemyInfluence.Value; }
        }

        public static bool ReadStoryCameraFocusChanges
        {
            get { return _readStoryCameraFocusChanges == null || _readStoryCameraFocusChanges.Value; }
        }

        public static bool ScannerPlaysDirectionalBeep
        {
            get { return _scannerPlaysDirectionalBeep != null && _scannerPlaysDirectionalBeep.Value; }
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
            _scannerPlaysDirectionalBeep = config.Bind(
                "Scanner",
                "ScannerPlaysDirectionalBeep",
                false,
                "Whether scanner result navigation should play a directional beep.");
            BindAnnouncementGroups(config);
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

        public static void SetScannerPlaysDirectionalBeep(bool value)
        {
            if (_scannerPlaysDirectionalBeep == null)
            {
                return;
            }

            _scannerPlaysDirectionalBeep.Value = value;
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
            return MergeOrder(group, orderCsv);
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

        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
            _readStoryCameraFocusChanges = null;
            _scannerPlaysDirectionalBeep = null;
            _announcementGroups.Clear();
        }

        private static void BindAnnouncementGroups(ConfigFile config)
        {
            _announcementGroups.Clear();
            IReadOnlyList<AnnouncementGroupDefinition> groups = AnnouncementDefinitions.All;
            for (int i = 0; i < groups.Count; i++)
            {
                AnnouncementGroupDefinition group = groups[i];
                AnnouncementGroupConfig groupConfig = new AnnouncementGroupConfig();
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
            }
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

        private static IReadOnlyList<string> MergeOrder(AnnouncementGroupDefinition group, string orderCsv)
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
            public ConfigEntry<string> Order { get; set; }
            public Dictionary<string, AnnouncementElementConfig> Elements { get; private set; } =
                new Dictionary<string, AnnouncementElementConfig>();
        }

        private sealed class AnnouncementElementConfig
        {
            public ConfigEntry<bool> Enabled { get; set; }
            public ConfigEntry<bool> Suffix { get; set; }
        }
    }
}
