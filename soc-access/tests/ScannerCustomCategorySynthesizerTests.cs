using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerCustomCategorySynthesizerTests
    {
        [TestMethod]
        public void ASelectorHoldsExactlyTheSubcategoryItNames()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            ScannerCustomCategory definition = new ScannerCustomCategory(1, "Scouting");
            definition.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true);

            ScannerCustomCategorySynthesizer.Apply(snapshot, new[] { definition });

            ScannerCategory custom = snapshot.Categories[0];
            Assert.AreEqual("Scouting", custom.Label);
            Assert.AreEqual(2, custom.Subcategories.Count);
            Assert.AreEqual("Pickups, Unvisited", custom.Subcategories[1].Label);
            CollectionAssert.AreEquivalent(
                new[] { "pickup:chest" },
                Keys(custom.Subcategories[1]));
        }

        [TestMethod]
        public void CustomCategoriesLeadTheCycle()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            string firstRealCategory = snapshot.Categories[0].Key;

            ScannerCustomCategorySynthesizer.Apply(
                snapshot,
                new[] { new ScannerCustomCategory(1, "First"), new ScannerCustomCategory(2, "Second") });

            Assert.AreEqual("First", snapshot.Categories[0].Label);
            Assert.AreEqual("Second", snapshot.Categories[1].Label);
            Assert.IsTrue(snapshot.Categories[0].IsCustom);
            Assert.AreEqual(firstRealCategory, snapshot.Categories[2].Key);
        }

        [TestMethod]
        public void AKeywordCatchesResultsByItemAndInstanceNameAsWellAsLabel()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            ScannerCustomCategory definition = new ScannerCustomCategory(1, "Ground");
            definition.AddKeyword("grass");

            ScannerCustomCategorySynthesizer.Apply(snapshot, new[] { definition });

            ScannerCategory custom = snapshot.Categories[0];
            Assert.AreEqual("grass", custom.Subcategories[1].Label);
            CollectionAssert.AreEquivalent(
                new[] { "terrain:grass:1", "wielder:grasshopper" },
                Keys(custom.Subcategories[1]));
        }

        [TestMethod]
        public void TheAllSubcategoryHearsAResultOnceHoweverManyWaysItWasCaught()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            ScannerCustomCategory definition = new ScannerCustomCategory(1, "Everything");
            definition.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.All, selected: true);
            definition.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true);
            definition.AddKeyword("chest");

            ScannerCustomCategorySynthesizer.Apply(snapshot, new[] { definition });

            ScannerCategory custom = snapshot.Categories[0];
            CollectionAssert.AreEquivalent(
                new[] { "pickup:chest", "pickup:crate" },
                Keys(custom.Subcategories[0]));
        }

        [TestMethod]
        public void ASelectorTheTaxonomyNoLongerHasIsSkipped()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            ScannerCustomCategory definition = new ScannerCustomCategory(1, "Stale");
            definition.SetSelector("retired_category", ScannerSubcategoryKeys.All, selected: true);

            ScannerCustomCategorySynthesizer.Apply(snapshot, new[] { definition });

            ScannerCategory custom = snapshot.Categories[0];
            Assert.AreEqual(1, custom.Subcategories.Count);
            Assert.IsFalse(custom.Subcategories[0].HasResults);
        }

        [TestMethod]
        public void SearchAndLookAroundIgnoreTheCopiesInCustomCategories()
        {
            ScannerSnapshot snapshot = BuildAdventureSnapshot();
            ScannerCustomCategory definition = new ScannerCustomCategory(1, "Scouting");
            definition.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.All, selected: true);
            ScannerCustomCategorySynthesizer.Apply(snapshot, new[] { definition });

            ScannerSnapshot search = ScannerSearch.Build(snapshot, "chest", Vector2Int.zero);

            ScannerCategory results = search.Categories[0];
            Assert.AreEqual(2, results.Subcategories.Count);
            Assert.AreEqual(ScannerSubcategoryKeys.All, results.Subcategories[0].Key);
            Assert.AreEqual(ScannerCategoryKeys.Pickups, results.Subcategories[1].Key);
        }

        private static ScannerSnapshot BuildAdventureSnapshot()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(AdventureScannerTaxonomy.Instance);
            snapshot.Add(
                ScannerCategoryKeys.Pickups,
                ScannerSubcategoryKeys.Unvisited,
                new ScannerResult("pickup:chest", "Chest", new Vector2Int(1, 0)));
            snapshot.Add(
                ScannerCategoryKeys.Pickups,
                ScannerSubcategoryKeys.All,
                new ScannerResult("pickup:chest", "Chest", new Vector2Int(1, 0)));
            snapshot.Add(
                ScannerCategoryKeys.Pickups,
                ScannerSubcategoryKeys.All,
                new ScannerResult("pickup:crate", "Crate", new Vector2Int(2, 0)));
            snapshot.Add(
                ScannerCategoryKeys.Wielders,
                ScannerSubcategoryKeys.All,
                new ScannerResult("wielder:grasshopper", "Grasshopper", new Vector2Int(3, 0)));
            snapshot.Add(
                ScannerCategoryKeys.Terrain,
                ScannerSubcategoryKeys.OpenGround,
                new ScannerResult("terrain:grass:1", "Grass", new Vector2Int(4, 0))
                {
                    ItemLabel = "Grass",
                    InstanceLabel = "12 tiles"
                });
            return snapshot;
        }

        private static string[] Keys(ScannerSubcategory subcategory)
        {
            List<string> keys = new List<string>();
            foreach (ScannerResult result in subcategory.AllResults)
            {
                keys.Add(result.Key);
            }

            return keys.ToArray();
        }
    }
}
