using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class CombatAnnouncementDefinitions
    {
        public static readonly AnnouncementGroupDefinition Tile = new AnnouncementGroupDefinition(
            "combat_tile",
            "Combat Tile Announcements",
            ModStrings.Screens.TileAnnouncements,
            new AnnouncementElementDefinition(TileKeys.SelectedForSpellcast, ModStrings.Screens.AnnouncementSelectedForSpellcast),
            new AnnouncementElementDefinition(TileKeys.Reachable, ModStrings.Screens.AnnouncementReachable),
            new AnnouncementElementDefinition(TileKeys.Occupant, ModStrings.Screens.AnnouncementOccupant),
            new AnnouncementElementDefinition(TileKeys.ImpassableOrBlocked, ModStrings.Screens.AnnouncementImpassableOrBlocked),
            new AnnouncementElementDefinition(TileKeys.TileEffects, ModStrings.Screens.AnnouncementTileEffects),
            new AnnouncementElementDefinition(TileKeys.Elevation, ModStrings.Screens.AnnouncementElevation),
            new AnnouncementElementDefinition(TileKeys.DecorativeFeatures, ModStrings.Screens.AnnouncementDecorativeFeatures),
            new AnnouncementElementDefinition(TileKeys.Coordinates, ModStrings.Screens.AnnouncementCoordinates),
            new AnnouncementElementDefinition(TileKeys.Influence, ModStrings.Screens.AnnouncementInfluence));

        public static readonly AnnouncementGroupDefinition Troop = new AnnouncementGroupDefinition(
            "combat_troop",
            "Combat Troop Announcements",
            ModStrings.Screens.TroopAnnouncements,
            new AnnouncementElementDefinition(TroopKeys.Acting, ModStrings.Screens.AnnouncementActing),
            new AnnouncementElementDefinition(TroopKeys.Attackable, ModStrings.Screens.AnnouncementAttackable),
            new AnnouncementElementDefinition(TroopKeys.StackSize, ModStrings.Screens.AnnouncementStackSize, defaultSuffix: false),
            new AnnouncementElementDefinition(TroopKeys.Affiliation, ModStrings.Screens.AnnouncementAffiliation, defaultSuffix: false),
            new AnnouncementElementDefinition(TroopKeys.TroopName, ModStrings.Screens.AnnouncementTroopName),
            new AnnouncementElementDefinition(TroopKeys.Health, ModStrings.Screens.AnnouncementHealth),
            new AnnouncementElementDefinition(TroopKeys.FacingDirectionForBeamAttacks, ModStrings.Screens.AnnouncementFacingDirectionForBeamAttacks));

        public static readonly AnnouncementGroupDefinition Entity = new AnnouncementGroupDefinition(
            "combat_entity",
            "Combat Entity Announcements",
            ModStrings.Screens.EntityAnnouncements,
            new AnnouncementElementDefinition(EntityKeys.Attackable, ModStrings.Screens.AnnouncementAttackable),
            new AnnouncementElementDefinition(EntityKeys.EntityName, ModStrings.Screens.AnnouncementEntityName),
            new AnnouncementElementDefinition(EntityKeys.Health, ModStrings.Screens.AnnouncementHealth));

        public static readonly AnnouncementGroupDefinition ScannerContent = new AnnouncementGroupDefinition(
            "combat_scanner_content",
            "Combat Scanner Content Announcements",
            ModStrings.Screens.ScannerContentAnnouncements,
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Name, ModStrings.Screens.AnnouncementName),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Owner, ModStrings.Screens.AnnouncementOwner),
            new AnnouncementElementDefinition(ScannerAnnouncementDefinitions.ContentKeys.Status, ModStrings.Screens.AnnouncementStatus, defaultSuffix: false))
            .WithVersion(2);

        public static readonly AnnouncementGroupDefinition[] All =
        {
            Tile,
            Troop,
            Entity,
            ScannerContent
        };

        public static class TileKeys
        {
            public const string SelectedForSpellcast = "selected_for_spellcast";
            public const string Reachable = "reachable";
            public const string Occupant = "occupant";
            public const string ImpassableOrBlocked = "impassable_or_blocked";
            public const string TileEffects = "tile_effects";
            public const string Elevation = "elevation";
            public const string DecorativeFeatures = "decorative_features";
            public const string Coordinates = "coordinates";
            public const string Influence = "influence";
        }

        public static class TroopKeys
        {
            public const string Acting = "acting";
            public const string Attackable = "attackable";
            public const string StackSize = "stack_size";
            public const string Affiliation = "affiliation";
            public const string TroopName = "troop_name";
            public const string Health = "health";
            public const string FacingDirectionForBeamAttacks = "facing_direction_for_beam_attacks";
        }

        public static class EntityKeys
        {
            public const string Attackable = "attackable";
            public const string EntityName = "entity_name";
            public const string Health = "health";
        }
    }
}
