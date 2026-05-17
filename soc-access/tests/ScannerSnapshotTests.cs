using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerSnapshotTests
    {
        [TestMethod]
        public void SortByDistanceOrdersResultsByDistanceThenLabelThenPosition()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:zinc", "Zinc", new Vector2Int(1, 0)));
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:apple-north", "Apple", new Vector2Int(0, 2)));
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:apple-east", "Apple", new Vector2Int(2, 0)));
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:berry", "Berry", new Vector2Int(0, 1)));

            snapshot.SortByDistance(Vector2Int.zero);

            ScannerSubcategory subcategory = snapshot.Categories[0].Subcategories[0];
            Assert.AreEqual("Berry", subcategory.Results[0].Label);
            Assert.AreEqual("Zinc", subcategory.Results[1].Label);
            Assert.AreEqual(new Vector2Int(0, 2), subcategory.Results[2].Position);
            Assert.AreEqual(new Vector2Int(2, 0), subcategory.Results[3].Position);
        }

        [TestMethod]
        public void SortByDistanceStoresSortOrigin()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:gold", "Gold", new Vector2Int(1, 0)));

            snapshot.SortByDistance(new Vector2Int(4, 5));

            Assert.IsTrue(snapshot.HasSortOrigin);
            Assert.AreEqual(new Vector2Int(4, 5), snapshot.SortOrigin);
        }

        [TestMethod]
        public void PruneEmptyRemovesEmptySubcategoriesAndCategories()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.GetOrAddCategory("Empty").GetOrAddSubcategory("All");
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:gold", "Gold", new Vector2Int(1, 1)));
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("Empty");

            snapshot.PruneEmpty();

            Assert.AreEqual(1, snapshot.Categories.Count);
            Assert.AreEqual("Pickups", snapshot.Categories[0].Label);
            Assert.AreEqual(1, snapshot.Categories[0].Subcategories.Count);
            Assert.AreEqual("All", snapshot.Categories[0].Subcategories[0].Label);
        }

        [TestMethod]
        public void AddUsesAllSubcategoryWhenSubcategoryIsBlank()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();

            snapshot.Add("Terrain", "", new ScannerResult("terrain:water", "Water", new Vector2Int(2, 3)));

            Assert.AreEqual("Terrain", snapshot.Categories[0].Label);
            Assert.AreEqual("All", snapshot.Categories[0].Subcategories[0].Label);
            Assert.AreEqual("Water", snapshot.Categories[0].Subcategories[0].Results[0].Label);
        }

        [TestMethod]
        public void TryLocateByKeyPrefersHintedSubcategory()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));
            snapshot.Add("Pickups", "Unvisited", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));

            bool found = snapshot.TryLocateByKey("entity:1", 0, 1, allowFallback: true, out ScannerSnapshotLocation location);

            Assert.IsTrue(found);
            Assert.AreEqual(0, location.CategoryIndex);
            Assert.AreEqual(1, location.SubcategoryIndex);
            Assert.AreEqual(0, location.ResultIndex);
        }

        [TestMethod]
        public void TryLocateByKeyCanFallbackOutsideHintedSubcategory()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("Unvisited");

            bool found = snapshot.TryLocateByKey("entity:1", 0, 1, allowFallback: true, out ScannerSnapshotLocation location);

            Assert.IsTrue(found);
            Assert.AreEqual(0, location.CategoryIndex);
            Assert.AreEqual(0, location.SubcategoryIndex);
            Assert.AreEqual(0, location.ResultIndex);
        }

        [TestMethod]
        public void TryLocateByKeyCanStayScopedToHintedSubcategory()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("Unvisited");

            bool found = snapshot.TryLocateByKey("entity:1", 0, 1, allowFallback: false, out ScannerSnapshotLocation location);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, location.CategoryIndex);
            Assert.AreEqual(-1, location.SubcategoryIndex);
            Assert.AreEqual(-1, location.ResultIndex);
        }

        [TestMethod]
        public void PruneByKeyRemovesDuplicateResultsAcrossSubcategoriesAndPreservesContainers()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));
            snapshot.Add("Pickups", "Unvisited", new ScannerResult("entity:1", "Gold", new Vector2Int(1, 0)));
            snapshot.Add("Pickups", "Unvisited", new ScannerResult("entity:2", "Ore", new Vector2Int(2, 0)));

            snapshot.PruneByKey("entity:1");

            Assert.AreEqual(1, snapshot.Categories.Count);
            Assert.AreEqual(2, snapshot.Categories[0].Subcategories.Count);
            Assert.AreEqual("All", snapshot.Categories[0].Subcategories[0].Label);
            Assert.AreEqual(0, snapshot.Categories[0].Subcategories[0].Results.Count);
            Assert.AreEqual("Unvisited", snapshot.Categories[0].Subcategories[1].Label);
            Assert.AreEqual("Ore", snapshot.Categories[0].Subcategories[1].Results[0].Label);
        }
    }
}
