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
                    "special_sites",
                    "wielders",
                    "settlements_and_build_sites",
                    "troop_sources",
                    "buildings",
                    "obstacles",
                    "terrain",
                    "exploration"
                },
                CategoryKeys(AdventureScannerTaxonomy.Instance));
        }

        [TestMethod]
        public void AdventureTaxonomyDeclaresExpectedSubcategoryOrder()
        {
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "pickups",
                "all", "unvisited", "knowledge", "power", "riches");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "resource_generators",
                "all", "neutral", "friendly", "enemy");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "special_sites",
                "all", "beacons", "objectives", "artifact_markets", "teleport");
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
            AssertSubcategories(AdventureScannerTaxonomy.Instance, "obstacles", "all");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "terrain",
                "roads_and_crossings", "open_ground", "barriers");
            AssertSubcategories(
                AdventureScannerTaxonomy.Instance,
                "exploration",
                "unexplored", "revealed");
        }

        [TestMethod]
        public void BattleTaxonomyDeclaresExpectedCategoryOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "troops", "spawn_points", "entities", "terrain" },
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
            AssertSubcategories(BattleScannerTaxonomy.Instance, "terrain", "all");
        }

        [TestMethod]
        public void OnlyRevealedPreservesResultOrder()
        {
            List<string> preserved = new List<string>();
            AddSubcategoryKeysWhere(AdventureScannerTaxonomy.Instance, preserved, s => s.PreserveResultOrder);
            AddSubcategoryKeysWhere(BattleScannerTaxonomy.Instance, preserved, s => s.PreserveResultOrder);

            CollectionAssert.AreEqual(new[] { "exploration/revealed" }, preserved);
        }

        [TestMethod]
        public void OnlyRevealedKeepsResultsUngrouped()
        {
            List<string> flat = new List<string>();
            AddSubcategoryKeysWhere(AdventureScannerTaxonomy.Instance, flat, s => s.FlatItems);
            AddSubcategoryKeysWhere(BattleScannerTaxonomy.Instance, flat, s => s.FlatItems);

            CollectionAssert.AreEqual(new[] { "exploration/revealed" }, flat);
        }

        /// <summary>
        /// The flags ride on the subcategory rather than the category, so a
        /// category can hold one scope whose order is the information beside
        /// others that sort by distance like everything else.
        /// </summary>
        [TestMethod]
        public void InitializedSnapshotCopiesSubcategoryOrderingFlags()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(AdventureScannerTaxonomy.Instance);

            ScannerSubcategory revealed = snapshot
                .GetOrAddCategory(ScannerCategoryKeys.Exploration)
                .GetOrAddSubcategory(ScannerSubcategoryKeys.Revealed);
            ScannerSubcategory unexplored = snapshot
                .GetOrAddCategory(ScannerCategoryKeys.Exploration)
                .GetOrAddSubcategory(ScannerSubcategoryKeys.Unexplored);

            Assert.IsTrue(revealed.PreserveResultOrder);
            Assert.IsTrue(revealed.FlatItems);
            Assert.IsFalse(unexplored.PreserveResultOrder);
            Assert.IsFalse(unexplored.FlatItems);
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

        private static void AddSubcategoryKeysWhere(
            ScannerTaxonomy taxonomy,
            List<string> keys,
            System.Func<ScannerSubcategoryDefinition, bool> predicate)
        {
            for (int i = 0; i < taxonomy.Categories.Count; i++)
            {
                ScannerCategoryDefinition category = taxonomy.Categories[i];
                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    ScannerSubcategoryDefinition subcategory = category.Subcategories[j];
                    string key = category.Key + "/" + subcategory.Key;
                    if (predicate(subcategory) && !keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
            }
        }
    }
}
