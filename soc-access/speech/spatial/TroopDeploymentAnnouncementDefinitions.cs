using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public static class TroopDeploymentAnnouncementDefinitions
    {
        public static readonly AnnouncementGroupDefinition Tile = new AnnouncementGroupDefinition(
            "troop_deployment_tile",
            "Troop Deployment Tile Announcements",
            ModStrings.Screens.TileAnnouncements,
            new AnnouncementElementDefinition(TileKeys.Troop, ModStrings.Screens.AnnouncementTroop),
            new AnnouncementElementDefinition(TileKeys.SpawnPoint, ModStrings.Screens.AnnouncementSpawnPoint),
            new AnnouncementElementDefinition(TileKeys.Impassable, ModStrings.Screens.AnnouncementImpassable),
            new AnnouncementElementDefinition(TileKeys.Elevation, ModStrings.Screens.AnnouncementElevation),
            new AnnouncementElementDefinition(TileKeys.Coordinates, ModStrings.Screens.AnnouncementCoordinates));

        public static readonly AnnouncementGroupDefinition ScannerContent = new AnnouncementGroupDefinition(
            "troop_deployment_scanner_content",
            "Troop Deployment Scanner Content Announcements",
            ModStrings.Screens.ScannerContentAnnouncements,
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Name, ModStrings.Screens.AnnouncementName),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Owner, ModStrings.Screens.AnnouncementOwner),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Status, ModStrings.Screens.AnnouncementStatus),
            new AnnouncementElementDefinition(TileKeys.Elevation, ModStrings.Screens.AnnouncementElevation, defaultSuffix: false))
            .WithVersion(2);

        public static readonly AnnouncementGroupDefinition[] All =
        {
            Tile,
            ScannerContent
        };

        public static class TileKeys
        {
            public const string Troop = "troop";
            public const string SpawnPoint = "spawn_point";
            public const string Impassable = "impassable";
            public const string Elevation = "elevation";
            public const string Coordinates = "coordinates";
        }
    }
}
