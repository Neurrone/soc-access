using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class ScannerAnnouncementDefinitions
    {
        public static readonly AnnouncementGroupDefinition Result = new AnnouncementGroupDefinition(
            "scanner_result",
            "Scanner Result Announcements",
            ModStrings.Screens.ScannerResultAnnouncements,
            new AnnouncementElementDefinition(ResultKeys.Item, ModStrings.Screens.AnnouncementItem),
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
            public const string Item = "item";
            public const string Content = "content";
            public const string Direction = "direction";
            public const string Coordinates = "coordinates";
            public const string ResultPosition = "result_position";
        }

        /// <summary>
        /// The parts of the scanned thing itself. Shared by the adventure map,
        /// combat and troop deployment content groups, which describe the same
        /// three facts and only differ in what the player is allowed to reorder
        /// per screen.
        /// </summary>
        public static class ContentKeys
        {
            public const string Name = "name";
            public const string Owner = "owner";
            public const string Status = "status";
            public const string Attackable = "attackable";
        }
    }
}
