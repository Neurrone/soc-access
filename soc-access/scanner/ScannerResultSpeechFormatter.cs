using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerResultSpeechFormatter
    {
        /// <summary>
        /// The item name is a prefix that says which group was reached, so it
        /// is spoken only when the announcement moves to a different item and
        /// only when the instance is not already saying the same word. An
        /// ungrouped thing therefore reads the same as it always did.
        /// </summary>
        public static string ItemName(ScannerResult result, bool includeItemName)
        {
            if (result == null || !includeItemName)
            {
                return null;
            }

            string item = result.ItemLabel;
            return string.Equals(item, result.InstanceLabel, StringComparison.Ordinal) ? null : item;
        }

        public static string Compose(string item, string content, string direction, string coordinates, string resultPosition)
        {
            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            AnnouncementPart.AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Item, item);
            AnnouncementPart.AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Content, content);
            AnnouncementPart.AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Direction, direction);
            AnnouncementPart.AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Coordinates, coordinates);
            AnnouncementPart.AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.ResultPosition, resultPosition);
            return ConfigurableAnnouncementComposer.Compose(ScannerAnnouncementDefinitions.Result, parts);
        }
    }
}
