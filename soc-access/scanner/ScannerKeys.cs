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

        public const string Roads = "roads";
        public const string DirtRoads = "dirt_roads";
        public const string CobblestoneRoads = "cobblestone_roads";
        public const string Walls = "walls";
        public const string Grass = "grass";
        public const string Sand = "sand";
        public const string Dirt = "dirt";
        public const string Bridges = "bridges";
        public const string Water = "water";
        public const string ShallowWater = "shallow_water";
        public const string DeepWater = "deep_water";
        public const string WaterEdge = "water_edge";
        public const string AridTrees = "arid_trees";
        public const string TemperateTrees = "temperate_trees";
        public const string Mountains = "mountains";
        public const string Deforestation = "deforestation";
        public const string Farmland = "farmland";
        public const string Impassable = "impassable";

        public const string FriendlyGates = "friendly_gates";
        public const string EnemyGates = "enemy_gates";
        public const string Attackable = "attackable";
        public const string Dangerous = "dangerous";
        public const string ElevatedGroundOne = "elevated_ground_1";
        public const string ElevatedGroundTwo = "elevated_ground_2";
        public const string ElevatedGroundThree = "elevated_ground_3";
        public const string ImpassableTerrain = "impassable_terrain";
        public const string Blocked = "blocked";
    }
}
