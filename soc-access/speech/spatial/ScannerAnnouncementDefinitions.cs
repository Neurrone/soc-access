using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class ScannerAnnouncementDefinitions
    {
        public static readonly AnnouncementGroupDefinition Result = new AnnouncementGroupDefinition(
            "scanner_result",
            "Scanner Result Announcements",
            ModStrings.Screens.ScannerResultAnnouncements,
            new AnnouncementElementDefinition(ResultKeys.Content, ModStrings.Screens.AnnouncementContent),
            new AnnouncementElementDefinition(ResultKeys.Direction, ModStrings.Screens.AnnouncementDirection),
            new AnnouncementElementDefinition(ResultKeys.Coordinates, ModStrings.Screens.AnnouncementCoordinates),
            new AnnouncementElementDefinition(ResultKeys.ResultPosition, ModStrings.Screens.AnnouncementResultPosition));

        public static readonly AnnouncementGroupDefinition[] All =
        {
            Result
        };

        public static class ResultKeys
        {
            public const string Content = "content";
            public const string Direction = "direction";
            public const string Coordinates = "coordinates";
            public const string ResultPosition = "result_position";
        }
    }
}
