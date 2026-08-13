namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Stable identifiers for scanner categories. These are persisted in result
    /// keys and in custom category selectors, so they must never change once
    /// shipped.
    /// </summary>
    internal static class ScannerCategoryKeys
    {
        public const string Pickups = "pickups";
        public const string ResourceGenerators = "resource_generators";
        public const string Beacons = "beacons";
        public const string Wielders = "wielders";
        public const string SettlementsAndBuildSites = "settlements_and_build_sites";
        public const string TroopSources = "troop_sources";
        public const string Buildings = "buildings";
        public const string Objectives = "objectives";
        public const string Obstacles = "obstacles";
        public const string ArtifactMarkets = "artifact_markets";
        public const string Teleport = "teleport";
        public const string Terrain = "terrain";
        public const string Unexplored = "unexplored";
        public const string Revealed = "revealed";
        public const string Troops = "troops";
        public const string SpawnPoints = "spawn_points";
        public const string Entities = "entities";
        public const string SearchResults = "search_results";
        public const string LookAround = "look_around";
    }

    /// <summary>
    /// Stable identifiers for the scanner taxonomies themselves. Custom
    /// categories are stored against these, so they must never change once
    /// shipped.
    /// </summary>
    internal static class ScannerTaxonomyKeys
    {
        public const string Adventure = "adventure";
        public const string Battle = "battle";

        public static readonly string[] All = { Adventure, Battle };
    }

    /// <summary>
    /// Stable identifiers for scanner subcategories. Shared across taxonomies
    /// where the meaning is the same.
    /// </summary>
    internal static class ScannerSubcategoryKeys
    {
        public const string All = "all";
        public const string Unvisited = "unvisited";
        public const string Knowledge = "knowledge";
        public const string Power = "power";
        public const string Riches = "riches";
        public const string Neutral = "neutral";
        public const string Friendly = "friendly";
        public const string Enemy = "enemy";

        public const string RoadsAndCrossings = "roads_and_crossings";
        public const string OpenGround = "open_ground";
        public const string RoughGround = "rough_ground";
        public const string Barriers = "barriers";

        public const string FriendlyGates = "friendly_gates";
        public const string EnemyGates = "enemy_gates";
        public const string Attackable = "attackable";
        public const string Dangerous = "dangerous";
    }

    /// <summary>
    /// Stable identifiers for the items the mod names itself, rather than
    /// taking from an entity. Anything the game already names groups by that
    /// name and needs no key here.
    /// </summary>
    internal static class ScannerItemKeys
    {
        public const string ElevatedGround = "elevated_ground_";
        public const string ImpassableTerrain = "impassable_terrain";
        public const string Blocked = "blocked";
    }
}
