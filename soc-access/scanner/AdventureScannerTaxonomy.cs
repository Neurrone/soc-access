using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    internal static class AdventureScannerTaxonomy
    {
        public static readonly ScannerTaxonomy Instance = new ScannerTaxonomy(
            new ScannerCategoryDefinition(
                ScannerCategoryKeys.Pickups,
                () => ModText.Get(ModStrings.Scanner.Pickups),
                Subcategory(ScannerSubcategoryKeys.Unvisited, ModStrings.Scanner.Unvisited),
                All(),
                Subcategory(ScannerSubcategoryKeys.Knowledge, ModStrings.Scanner.Knowledge),
                Subcategory(ScannerSubcategoryKeys.Power, ModStrings.Scanner.Power),
                Subcategory(ScannerSubcategoryKeys.Riches, ModStrings.Scanner.Riches)),
            Relationship(ScannerCategoryKeys.ResourceGenerators, ModStrings.Scanner.ResourceGenerators),
            OnlyAll(ScannerCategoryKeys.Beacons, ModStrings.Scanner.Beacons),
            OnlyAll(ScannerCategoryKeys.Wielders, ModStrings.Scanner.Wielders),
            Relationship(ScannerCategoryKeys.SettlementsAndBuildSites, ModStrings.Scanner.SettlementsAndBuildSites),
            Relationship(ScannerCategoryKeys.TroopSources, ModStrings.Scanner.TroopSources),
            Relationship(ScannerCategoryKeys.Buildings, ModStrings.Scanner.Buildings),
            OnlyAll(ScannerCategoryKeys.Objectives, ModStrings.Scanner.Objectives),
            OnlyAll(ScannerCategoryKeys.Obstacles, ModStrings.Scanner.Obstacles),
            OnlyAll(ScannerCategoryKeys.ArtifactMarkets, ModStrings.Scanner.ArtifactMarkets),
            OnlyAll(ScannerCategoryKeys.Teleport, ModStrings.Scanner.Teleport),
            new ScannerCategoryDefinition(
                ScannerCategoryKeys.Terrain,
                () => ModText.Get(ModStrings.Scanner.Terrain),
                Subcategory(ScannerSubcategoryKeys.Roads, ModStrings.Scanner.Roads),
                Subcategory(ScannerSubcategoryKeys.DirtRoads, ModStrings.Scanner.DirtRoads),
                Subcategory(ScannerSubcategoryKeys.CobblestoneRoads, ModStrings.Scanner.CobblestoneRoads),
                Subcategory(ScannerSubcategoryKeys.Walls, ModStrings.Scanner.Walls),
                Subcategory(ScannerSubcategoryKeys.Grass, ModStrings.Scanner.Grass),
                Subcategory(ScannerSubcategoryKeys.Sand, ModStrings.Scanner.Sand),
                Subcategory(ScannerSubcategoryKeys.Dirt, ModStrings.Scanner.Dirt),
                Subcategory(ScannerSubcategoryKeys.Bridges, ModStrings.Scanner.Bridges),
                Subcategory(ScannerSubcategoryKeys.Water, ModStrings.Scanner.Water),
                Subcategory(ScannerSubcategoryKeys.ShallowWater, ModStrings.Scanner.ShallowWater),
                Subcategory(ScannerSubcategoryKeys.DeepWater, ModStrings.Scanner.DeepWater),
                Subcategory(ScannerSubcategoryKeys.WaterEdge, ModStrings.Scanner.WaterEdge),
                Subcategory(ScannerSubcategoryKeys.AridTrees, ModStrings.Scanner.AridTrees),
                Subcategory(ScannerSubcategoryKeys.TemperateTrees, ModStrings.Scanner.TemperateTrees),
                Subcategory(ScannerSubcategoryKeys.Mountains, ModStrings.Scanner.Mountains),
                Subcategory(ScannerSubcategoryKeys.Deforestation, ModStrings.Scanner.Deforestation),
                Subcategory(ScannerSubcategoryKeys.Farmland, ModStrings.Scanner.Farmland),
                Subcategory(ScannerSubcategoryKeys.Impassable, ModStrings.Scanner.Impassable)),
            OnlyAll(ScannerCategoryKeys.Unexplored, ModStrings.Scanner.Unexplored),
            OnlyAll(ScannerCategoryKeys.Revealed, ModStrings.Scanner.Revealed).WithPreservedResultOrder().WithFlatItems());

        private static ScannerCategoryDefinition OnlyAll(string key, ModString label)
        {
            return new ScannerCategoryDefinition(key, () => ModText.Get(label), All());
        }

        private static ScannerCategoryDefinition Relationship(string key, ModString label)
        {
            return new ScannerCategoryDefinition(
                key,
                () => ModText.Get(label),
                All(),
                Subcategory(ScannerSubcategoryKeys.Neutral, ModStrings.Scanner.Neutral),
                Subcategory(ScannerSubcategoryKeys.Friendly, ModStrings.Scanner.Friendly),
                Subcategory(ScannerSubcategoryKeys.Enemy, ModStrings.Scanner.Enemy));
        }

        private static ScannerSubcategoryDefinition All()
        {
            return Subcategory(ScannerSubcategoryKeys.All, ModStrings.Scanner.All);
        }

        private static ScannerSubcategoryDefinition Subcategory(string key, ModString label)
        {
            return new ScannerSubcategoryDefinition(key, () => ModText.Get(label));
        }
    }
}
