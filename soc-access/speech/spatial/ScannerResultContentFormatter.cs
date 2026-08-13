using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Speech.Spatial
{
    /// <summary>
    /// Describes the thing a scanner result points at: what tells it apart from
    /// the others under its item, whose it is, and what state it is in. The item
    /// name is announced separately, and everything else about the tile it
    /// happens to stand on belongs to the cursor announcement.
    /// </summary>
    internal static class ScannerResultContentFormatter
    {
        /// <summary>
        /// <paramref name="tileParts"/> lets a screen add facts about the tile
        /// the thing stands on where those change how the player would act on
        /// it, such as combat reachability. They are ordinary elements, so the
        /// player can reorder or silence them like any other.
        /// </summary>
        public static string Describe(
            AnnouncementGroupDefinition group,
            ScannerResult result,
            IEnumerable<AnnouncementPart> tileParts = null)
        {
            return Describe(
                group,
                result,
                tileParts,
                ModSettings.GetAnnouncementOrder,
                ModSettings.GetAnnouncementElementEnabled,
                ModSettings.GetAnnouncementElementSuffix);
        }

        internal static string Describe(
            AnnouncementGroupDefinition group,
            ScannerResult result,
            IEnumerable<AnnouncementPart> tileParts,
            Func<AnnouncementGroupDefinition, IReadOnlyList<string>> getOrder,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> isEnabled,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> includeSuffix)
        {
            if (result == null)
            {
                return string.Empty;
            }

            List<AnnouncementPart> parts = new List<AnnouncementPart>(BuildParts(result));
            if (tileParts != null)
            {
                parts.AddRange(tileParts);
            }

            return ConfigurableAnnouncementComposer.Compose(
                group,
                parts,
                getOrder,
                isEnabled,
                includeSuffix);
        }

        private static IEnumerable<AnnouncementPart> BuildParts(ScannerResult result)
        {
            // Emitted for every screen; only the groups that declare the
            // element show it, which is what keeps a fact that belongs to one
            // screen out of the others without a screen-specific formatter.
            if (result.Attackable)
            {
                yield return new AnnouncementPart(
                    ScannerAnnouncementDefinitions.ContentKeys.Attackable,
                    ModText.Get(ModStrings.Scanner.Attackable));
            }

            if (!string.IsNullOrWhiteSpace(result.InstanceLabel))
            {
                yield return new AnnouncementPart(
                    ScannerAnnouncementDefinitions.ContentKeys.Name,
                    result.InstanceLabel);
            }

            string owner = DescribeOwner(result);
            if (!string.IsNullOrWhiteSpace(owner))
            {
                yield return new AnnouncementPart(
                    ScannerAnnouncementDefinitions.ContentKeys.Owner,
                    owner);
            }

            string status = DescribeStatus(result);
            if (!string.IsNullOrWhiteSpace(status))
            {
                yield return new AnnouncementPart(
                    ScannerAnnouncementDefinitions.ContentKeys.Status,
                    status);
            }
        }

        private static string DescribeOwner(ScannerResult result)
        {
            switch (result.Relationship)
            {
                case ScannerResultRelationship.Neutral:
                    return ModText.Get(ModStrings.Scanner.Neutral);
                case ScannerResultRelationship.Friendly:
                    return ModText.Get(ModStrings.Scanner.Friendly);
                case ScannerResultRelationship.Enemy:
                    return ModText.Get(ModStrings.Scanner.Enemy);
                default:
                    return string.Empty;
            }
        }

        private static string DescribeStatus(ScannerResult result)
        {
            List<string> parts = new List<string>();
            if (result.Unvisited)
            {
                parts.Add(ModText.Get(ModStrings.Scanner.Unvisited));
            }

            if (result.NotVisible)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Unseen));
            }

            return parts.Count == 0 ? string.Empty : ModText.JoinListWithCommas(parts);
        }
    }
}
