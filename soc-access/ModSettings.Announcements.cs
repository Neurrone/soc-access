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
    public static partial class ModSettings
    {
        public static IReadOnlyList<string> GetAnnouncementOrder(AnnouncementGroupDefinition group)
        {
            if (group == null)
            {
                return new string[0];
            }

            AnnouncementGroupConfig config = GetAnnouncementConfig(group);
            if (config == null || config.Order == null)
            {
                return MergeAnnouncementOrder(group, group.DefaultOrderCsv);
            }

            // The merge is a Split, a List, a HashSet and an IndexOf, and this is the order
            // source for every tile read. Key the memo on the saved CSV itself: every write to
            // Order.Value - the two setters, the version migration, an edit to the config file -
            // drops it without a hook to remember.
            string orderCsv = config.Order.Value;
            if (config.MergedOrder == null || !string.Equals(config.MergedOrderSource, orderCsv, StringComparison.Ordinal))
            {
                config.MergedOrderSource = orderCsv;
                config.MergedOrder = MergeAnnouncementOrder(group, orderCsv);
            }

            return config.MergedOrder;
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
            return group == null ? null : Lookup(_announcementGroups, group.Key);
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

        private sealed class AnnouncementGroupConfig
        {
            public ConfigEntry<int> Version { get; set; }
            public ConfigEntry<string> Order { get; set; }
            public string MergedOrderSource { get; set; }
            public IReadOnlyList<string> MergedOrder { get; set; }
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
