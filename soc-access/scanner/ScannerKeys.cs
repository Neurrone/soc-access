using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>Result keys for the things a scanner identifies by the tile they sit on.</summary>
    public static class ScannerTileKeys
    {
        public static string For(string prefix, Vector2Int point)
        {
            return prefix + ":" + point.x + ":" + point.y;
        }
    }

    /// <summary>
    /// Stable identifiers for scanner categories. These are persisted in result
    /// keys and in custom category selectors, so they must never change once
    /// shipped.
    /// </summary>
    public static class ScannerCategoryKeys
    {
        public const string Pickups = "pickups";
        public const string ResourceGenerators = "resource_generators";
        public const string SpecialSites = "special_sites";
        public const string Wielders = "wielders";
        public const string SettlementsAndBuildSites = "settlements_and_build_sites";
        public const string TroopSources = "troop_sources";
        public const string Buildings = "buildings";
        public const string Obstacles = "obstacles";
        public const string Terrain = "terrain";
        public const string Exploration = "exploration";
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
    public static class ScannerTaxonomyKeys
    {
        public const string Adventure = "adventure";
        public const string Battle = "battle";

        public static readonly string[] All = { Adventure, Battle };
    }

    /// <summary>
    /// Stable identifiers for scanner subcategories. Shared across taxonomies
    /// where the meaning is the same.
    /// </summary>
    public static class ScannerSubcategoryKeys
    {
        public const string All = "all";
        public const string Unvisited = "unvisited";
        public const string Knowledge = "knowledge";
        public const string Power = "power";
        public const string Riches = "riches";
        public const string Neutral = "neutral";
        public const string Friendly = "friendly";
        public const string Enemy = "enemy";

        public const string Beacons = "beacons";
        public const string Objectives = "objectives";
        public const string ArtifactMarkets = "artifact_markets";
        public const string Merchants = "merchants";
        public const string Teleport = "teleport";

        public const string RoadsAndCrossings = "roads_and_crossings";
        public const string OpenGround = "open_ground";
        public const string Barriers = "barriers";

        public const string Unexplored = "unexplored";
        public const string Revealed = "revealed";

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
    public static class ScannerItemKeys
    {
        public const string Road = "road";
        public const string DirtRoad = "dirt-road";
        public const string CobblestoneRoad = "cobblestone-road";
        public const string Bridge = "bridge";

        public const string Grass = "grass";
        public const string Sand = "sand";
        public const string Dirt = "dirt";
        public const string Farmland = "farmland";
        public const string AridTrees = "arid-trees";
        public const string TemperateTrees = "temperate-trees";
        public const string Deforestation = "deforestation";
        public const string DeadBodies = "dead-bodies";
        public const string DeadSoldiers = "dead-soldiers";
        public const string Bones = "bones";
        public const string DragonBones = "dragon-bones";
        public const string Structures = "structures";
        public const string Campfire = "campfire";
        public const string Excavation = "excavation";
        public const string HuntingCamp = "hunting-camp";
        public const string FishingSpot = "fishing-spot";

        public const string Mountain = "mountain";
        public const string Wall = "wall";
        public const string Obstruction = "obstruction";
        public const string Water = "water";
        public const string ShallowWater = "shallow-water";
        public const string DeepWater = "deep-water";
        public const string WaterEdge = "water-edge";
        public const string FaeyForest = "faey-forest";
        public const string BirchForest = "birch-forest";
        public const string Magnolia = "magnolia";
        public const string Bamboo = "bamboo";
        public const string Palisade = "palisade";
        public const string FortifiedGate = "fortified-gate";
        public const string Barricade = "barricade";
        public const string Tombstones = "tombstones";
        public const string Ruins = "ruins";
        public const string WinterDecorations = "winter-decorations";
        public const string MidsummerDecorations = "midsummer-decorations";
        public const string Torch = "torch";

        public const string GuardedGround = "guarded-ground";
        public const string Unexplored = "unexplored";
        public const string ZoneOfControl = "zone_of_control";
    }
}
