using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class AdventureMapAnnouncementDefinitions
    {
        public static readonly AnnouncementGroupDefinition Tile = new AnnouncementGroupDefinition(
            "adventure_map_tile",
            "Adventure Map Tile Announcements",
            ModStrings.Screens.TileAnnouncements,
            new AnnouncementElementDefinition(TileKeys.ExplorationState, ModStrings.Screens.AnnouncementExplorationState),
            new AnnouncementElementDefinition(TileKeys.Wielder, ModStrings.Screens.AnnouncementWielder),
            new AnnouncementElementDefinition(TileKeys.MapEntity, ModStrings.Screens.AnnouncementMapEntity),
            new AnnouncementElementDefinition(TileKeys.InteractionPoint, ModStrings.Screens.AnnouncementInteractionPoint),
            new AnnouncementElementDefinition(TileKeys.ReachabilityOrRoutePreview, ModStrings.Screens.AnnouncementReachabilityOrRoutePreview),
            new AnnouncementElementDefinition(TileKeys.ZoneOfControl, ModStrings.Screens.AnnouncementZoneOfControl),
            new AnnouncementElementDefinition(TileKeys.Terrain, ModStrings.Screens.AnnouncementTerrain),
            new AnnouncementElementDefinition(TileKeys.Coordinates, ModStrings.Screens.AnnouncementCoordinates),
            new AnnouncementElementDefinition(TileKeys.MovementCost, ModStrings.Screens.AnnouncementMovementCost, defaultEnabled: false));

        public static readonly AnnouncementGroupDefinition ScannerContent = new AnnouncementGroupDefinition(
            "adventure_map_scanner_content",
            "Adventure Map Scanner Content Announcements",
            ModStrings.Screens.ScannerContentAnnouncements,
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Name, ModStrings.Screens.AnnouncementName),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Owner, ModStrings.Screens.AnnouncementOwner),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Status, ModStrings.Screens.AnnouncementStatus),
            new AnnouncementElementDefinition(TileKeys.ReachabilityOrRoutePreview, ModStrings.Screens.AnnouncementReachabilityOrRoutePreview),
            new AnnouncementElementDefinition(TileKeys.MovementCost, ModStrings.Screens.AnnouncementMovementCost, defaultEnabled: false, defaultSuffix: false))
            .WithVersion(2);

        public static readonly AnnouncementGroupDefinition Wielder = new AnnouncementGroupDefinition(
            "adventure_map_wielder",
            "Adventure Map Wielder Announcements",
            ModStrings.Screens.WielderAnnouncements,
            new AnnouncementElementDefinition(WielderKeys.Name, ModStrings.Screens.AnnouncementName),
            new AnnouncementElementDefinition(WielderKeys.Affiliation, ModStrings.Screens.AnnouncementAffiliation),
            new AnnouncementElementDefinition(WielderKeys.Selected, ModStrings.Screens.AnnouncementSelected),
            new AnnouncementElementDefinition(WielderKeys.Movement, ModStrings.Screens.AnnouncementMovement),
            new AnnouncementElementDefinition(WielderKeys.Destination, ModStrings.Screens.AnnouncementDestination),
            new AnnouncementElementDefinition(WielderKeys.ThisTurnDestination, ModStrings.Screens.AnnouncementThisTurnDestination));

        public static readonly AnnouncementGroupDefinition MapEntity = new AnnouncementGroupDefinition(
            "adventure_map_entity",
            "Adventure Map Entity Announcements",
            ModStrings.Screens.MapEntityAnnouncements,
            new AnnouncementElementDefinition(MapEntityKeys.Name, ModStrings.Screens.AnnouncementName),
            new AnnouncementElementDefinition(MapEntityKeys.Visited, ModStrings.Screens.AnnouncementVisited),
            new AnnouncementElementDefinition(MapEntityKeys.Affiliation, ModStrings.Screens.AnnouncementAffiliation));

        public static readonly AnnouncementGroupDefinition[] All =
        {
            Tile,
            ScannerContent,
            Wielder,
            MapEntity
        };

        public static class TileKeys
        {
            public const string ExplorationState = "exploration_state";
            public const string Wielder = "wielder";
            public const string MapEntity = "map_entity";
            public const string InteractionPoint = "interaction_point";
            public const string ReachabilityOrRoutePreview = "reachability_or_route_preview";
            public const string ZoneOfControl = "zone_of_control";
            public const string Terrain = "terrain";
            public const string Coordinates = "coordinates";
            public const string MovementCost = "movement_cost";
        }

        public static class WielderKeys
        {
            public const string Name = "name";
            public const string Affiliation = "affiliation";
            public const string Selected = "selected";
            public const string Movement = "movement";
            public const string Destination = "destination";
            public const string ThisTurnDestination = "this_turn_destination";
        }

        public static class MapEntityKeys
        {
            public const string Name = "name";
            public const string Visited = "visited";
            public const string Affiliation = "affiliation";
        }
    }
}
