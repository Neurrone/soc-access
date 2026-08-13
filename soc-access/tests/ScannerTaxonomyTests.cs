using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The taxonomies are declarative tables that decide cycling order and feed
    /// persisted result keys and custom category selectors. These tests pin the
    /// exact key order so an accidental reorder, rename or drop fails here
    /// rather than silently changing what the scanner reads.
    /// </summary>
    [TestClass]
    public sealed class ScannerTaxonomyTests
    {
        [TestMethod]
        public void AdventureTaxonomyDeclaresExpectedCategoryOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "pickups",
                    "resource_generators",
                    "beacons",
                    "wielders",
                    "settlements_and_build_sites",
                    "troop_sources",
                    "buildings",
                    "objectives",
                    "obstacles",
                    "artifact_markets",
                    "teleport",
                    "terrain",
                    "unexplored",
                    "revealed"
                },
                CategoryKeys(AdventureScannerTaxonomy.Instance));
        }

        [TestMethod]
        public void AdventureTaxonomyDeclaresExpectedSubcategoryOrder()
        {
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "pickups",
                "unvisited", "all", "knowledge", "power", "riches");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "resource_generators",
                "all", "neutral", "friendly", "enemy");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "beacons", "all");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "wielders", "all");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "settlements_and_build_sites",
                "all", "neutral", "friendly", "enemy");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "troop_sources",
                "all", "neutral", "friendly", "enemy");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "buildings",
                "all", "neutral", "friendly", "enemy");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "objectives", "all");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "obstacles", "all");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "artifact_markets", "all");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "teleport", "all");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "terrain",
                "roads", "dirt_roads", "cobblestone_roads", "walls", "grass", "sand", "dirt", "bridges",
                "water", "shallow_water", "deep_water", "water_edge", "arid_trees", "temperate_trees",
                "mountains", "deforestation", "farmland", "impassable");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "unexplored", "all");
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "revealed", "all");
        }

        [TestMethod]
        public void BattleTaxonomyDeclaresExpectedCategoryOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "troops", "spawn_points", "entities", "terrain", "obstacles" },
                CategoryKeys(BattleScannerTaxonomy.Instance));
        }

        [TestMethod]
        public void BattleTaxonomyDeclaresExpectedSubcategoryOrder()
        {
            AssertSubcategories(BattleScannerTaxonomy.Instance, "troops", "all", "friendly", "enemy");
            AssertSubcategories(BattleScannerTaxonomy.Instance, "spawn_points", "all", "friendly", "enemy");
            AssertSubcategories(
                BattleScannerTaxonomy.Instance,
                "entities",
                "all", "friendly_gates", "enemy_gates", "attackable", "dangerous");
            AssertSubcategories(
                BattleScannerTaxonomy.Instance,
                "terrain",
                "elevated_ground_1", "elevated_ground_2", "elevated_ground_3", "impassable_terrain");
            AssertSubcategories(BattleScannerTaxonomy.Instance, "obstacles", "blocked");
        }

        [TestMethod]
        public void OnlyRevealedPreservesResultOrder()
        {
            List<string> preserved = new List<string>();
            AddPreservedCategoryKeys(AdventureScannerTaxonomy.Instance, preserved);
            AddPreservedCategoryKeys(BattleScannerTaxonomy.Instance, preserved);

            CollectionAssert.AreEqual(new[] { "revealed" }, preserved);
        }

        [TestMethod]
        public void InitializedSnapshotMirrorsTaxonomyOrder()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(AdventureScannerTaxonomy.Instance);

            List<string> categories = new List<string>();
            for (int i = 0; i < snapshot.Categories.Count; i++)
            {
                categories.Add(snapshot.Categories[i].Key);
            }

            CollectionAssert.AreEqual(CategoryKeys(AdventureScannerTaxonomy.Instance), categories);
            Assert.IsTrue(snapshot.IsEmpty);
        }

        [TestMethod]
        public void InitializedSnapshotCopiesPreservedResultOrder()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(AdventureScannerTaxonomy.Instance);

            for (int i = 0; i < snapshot.Categories.Count; i++)
            {
                ScannerCategory category = snapshot.Categories[i];
                Assert.AreEqual(
                    category.Key == ScannerCategoryKeys.Revealed,
                    category.PreserveResultOrder,
                    category.Key);
            }
        }

        private static void AssertSubcategories(ScannerTaxonomy taxonomy, string categoryKey, params string[] expected)
        {
            ScannerCategoryDefinition category = taxonomy.GetCategory(categoryKey);
            Assert.IsNotNull(category, categoryKey);

            List<string> actual = new List<string>();
            for (int i = 0; i < category.Subcategories.Count; i++)
            {
                actual.Add(category.Subcategories[i].Key);
            }

            CollectionAssert.AreEqual(expected, actual, categoryKey);
        }

        private static List<string> CategoryKeys(ScannerTaxonomy taxonomy)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < taxonomy.Categories.Count; i++)
            {
                keys.Add(taxonomy.Categories[i].Key);
            }

            return keys;
        }

        private static void AddPreservedCategoryKeys(ScannerTaxonomy taxonomy, List<string> keys)
        {
            for (int i = 0; i < taxonomy.Categories.Count; i++)
            {
                if (taxonomy.Categories[i].PreserveResultOrder && !keys.Contains(taxonomy.Categories[i].Key))
                {
                    keys.Add(taxonomy.Categories[i].Key);
                }
            }
        }
    }
}
