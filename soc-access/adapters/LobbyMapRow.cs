using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>What the lobby's two map tables - the map select page and the challenge page - each
    /// hand their preview panel about the row the menu has selected. The two menus are different game
    /// types with different entry types, so this is the only thing their rows have in common.
    /// </summary>
    public interface ILobbyMapRow
    {
        /// <summary>The map's name as the game wrote it.</summary>
        string Name { get; }

        /// <summary>The dossier the preview panel draws for this map.</summary>
        string Description { get; }

        /// <summary>The game's own name for each of the map's win conditions.</summary>
        IReadOnlyList<string> WinConditionLabels { get; }
    }

    /// <summary>What a row of either lobby map table reads off the game: the map's win conditions
    /// out of its metadata, and the tooltips the game hangs on the icons it draws them as. Both
    /// tables draw the same icons from the same metadata, so one copy answers for both.
    /// </summary>
    public static class LobbyMapRow
    {
        /// <summary>The game's name for each win condition the map declares, in the map's own order,
        /// skipping any the localization tables have no name for.</summary>
        public static IReadOnlyList<string> WinConditionLabels(
            MapFormat.AdventureMapMetadata metadata,
            ILocalizationHandler localization)
        {
            if (metadata == null || metadata.WinConditions == null)
            {
                return new string[0];
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < metadata.WinConditions.Length; i++)
            {
                AdventureWinCondition condition = metadata.WinConditions[i];
                AddIfNotEmpty(parts, LocalizedText(localization, "GameModes/" + condition + "/Name", condition.ToString()));
            }

            return parts;
        }

        /// <summary>One tooltip per drawn win-condition icon, lined up with
        /// <see cref="WinConditionLabels"/>; a null entry is an icon the game drew without one.
        /// </summary>
        public static IReadOnlyList<Tooltip> WinConditionTooltips(
            IReadOnlyList<string> labels,
            UIImage[] icons,
            ILocalizationHandler localization)
        {
            int count = labels != null ? labels.Count : 0;
            List<Tooltip> tooltips = new List<Tooltip>(count);
            for (int i = 0; i < count; i++)
            {
                UIImage icon = icons != null && i < icons.Length ? icons[i] : null;
                tooltips.Add(ComponentTooltip(icon, localization));
            }

            return tooltips;
        }

        /// <summary>Every drawn win-condition icon's tooltip as one tooltip, aimed at the first icon
        /// that has one, or null while none of them does.</summary>
        public static Tooltip WinConditionTooltip(UIImage[] icons, ILocalizationHandler localization)
        {
            if (icons == null || icons.Length == 0)
            {
                return null;
            }

            List<Component> components = new List<Component>();
            for (int i = 0; i < icons.Length; i++)
            {
                UIImage icon = icons[i];
                if (HasTooltip(icon, localization))
                {
                    components.Add(icon);
                }
            }

            if (components.Count == 0)
            {
                return null;
            }

            return new Tooltip(
                () => CombinedTooltipLines(components, localization),
                VisualTooltipMetadata.ForComponent(components[0]));
        }

        public static Tooltip ComponentTooltip(Component component, ILocalizationHandler localization)
        {
            return HasTooltip(component, localization) ? Tooltip.ForComponent(component, localization) : null;
        }

        /// <summary>A tooltip's existence can only be answered by capturing it, so this is the one
        /// question the callers ask before they keep a component.</summary>
        public static bool HasTooltip(Component component, ILocalizationHandler localization)
        {
            return GameObjects.IsLive(component)
                && NativeTooltipUtility.GetTooltipLinesForComponent(component, localization).Count > 0;
        }

        public static IReadOnlyList<string> CombinedTooltipLines(
            IReadOnlyList<Component> components,
            ILocalizationHandler localization)
        {
            List<string> lines = new List<string>();
            if (components == null)
            {
                return lines;
            }

            for (int i = 0; i < components.Count; i++)
            {
                IReadOnlyList<string> componentLines = NativeTooltipUtility.GetTooltipLinesForComponent(components[i], localization);
                for (int j = 0; j < componentLines.Count; j++)
                {
                    AddIfNotDuplicate(lines, componentLines[j]);
                }
            }

            return lines;
        }

        public static string LocalizedText(ILocalizationHandler localization, string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return SpokenText.Get(localization, key, fallback);
        }

        public static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        /// <summary>Keeps the line the game wrote, whitespace and all, but compares it trimmed: two
        /// icons whose tooltips differ only in indentation are one line, not two.</summary>
        public static void AddIfNotDuplicate(List<string> parts, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = value.Trim();
            for (int i = 0; i < parts.Count; i++)
            {
                if (string.Equals(parts[i]?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            parts.Add(value);
        }
    }
}
