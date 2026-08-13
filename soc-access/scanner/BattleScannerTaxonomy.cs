using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Combat and troop placement are the same map, so they share one taxonomy.
    /// The category set is the union of both screens' needs; each screen
    /// populates only what applies and the empty-scope skip hides the rest.
    /// </summary>
    internal static class BattleScannerTaxonomy
    {
        public static readonly ScannerTaxonomy Instance = new ScannerTaxonomy(
            Sides(ScannerCategoryKeys.Troops, ModStrings.Scanner.Troops),
            Sides(ScannerCategoryKeys.SpawnPoints, ModStrings.Scanner.SpawnPoints),
            new ScannerCategoryDefinition(
                ScannerCategoryKeys.Entities,
                () => ModText.Get(ModStrings.Scanner.Entities),
                All(),
                Subcategory(ScannerSubcategoryKeys.FriendlyGates, ModStrings.Scanner.FriendlyGates),
                Subcategory(ScannerSubcategoryKeys.EnemyGates, ModStrings.Scanner.EnemyGates),
                Subcategory(ScannerSubcategoryKeys.Attackable, ModStrings.Scanner.Attackable),
                Subcategory(ScannerSubcategoryKeys.Dangerous, ModStrings.Scanner.Dangerous)),
            new ScannerCategoryDefinition(
                ScannerCategoryKeys.Terrain,
                () => ModText.Get(ModStrings.Scanner.Terrain),
                All()),
            new ScannerCategoryDefinition(
                ScannerCategoryKeys.Obstacles,
                () => ModText.Get(ModStrings.Scanner.Obstacles),
                All()));

        private static ScannerCategoryDefinition Sides(string key, ModString label)
        {
            return new ScannerCategoryDefinition(
                key,
                () => ModText.Get(label),
                All(),
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
