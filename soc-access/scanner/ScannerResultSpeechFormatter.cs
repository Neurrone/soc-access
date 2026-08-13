using System.Collections.Generic;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerResultSpeechFormatter
    {
        public static string Compose(string item, string content, string direction, string coordinates, string resultPosition)
        {
            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Item, item);
            AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Content, content);
            AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Direction, direction);
            AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.Coordinates, coordinates);
            AddIfPresent(parts, ScannerAnnouncementDefinitions.ResultKeys.ResultPosition, resultPosition);
            return ConfigurableAnnouncementComposer.Compose(ScannerAnnouncementDefinitions.Result, parts);
        }

        private static void AddIfPresent(List<AnnouncementPart> parts, string key, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(new AnnouncementPart(key, text));
            }
        }
    }
}
