using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;
using static SongsOfConquestAccess.Tests.ScannerFixtures;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>Look Around mode: the sweep of what lies within a radius of the cursor, the order it
    /// is read in, and the keys that leave it again.</summary>
    [TestClass]
    public sealed class ScannerLookAroundModeTests
    {
        [TestMethod]
        public void ExecuteLookAroundFiltersByGameCircleAndExcludesOrigin()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Wielders", "All", "Here", 0, 0, "commander:here"),
                Entry("Pickups", "All", "Inside", 3, 4, "pickup:inside"),
                Entry("Pickups", "All", "Outside", 4, 4, "pickup:outside")));

            ScannerCommandResult result = controller.ExecuteLookAround(5);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Look around", result.CategoryLabel);
            Assert.AreEqual("All", result.SubcategoryLabel);
            Assert.AreEqual("Inside", result.Result.Label);
            Assert.AreEqual(1, result.ResultCount);
        }

        [TestMethod]
        public void ExecuteLookAroundExcludesGroupedResults()
        {
            ScannerSnapshot snapshot = Snapshot(
                Entry("Terrain", "Roads", "Road tiles", 0, 1, "terrain:road"),
                Entry("Obstacles", "All", "5 blocked tiles", 1, 0, "blocked:area"),
                Entry("Pickups", "All", "Wood", 0, 2, "pickup:wood"));
            snapshot.Categories[0].Subcategories[0].Items[0].Instances[0].Kind = ScannerResultKind.TerrainGroup;
            snapshot.Categories[1].Subcategories[0].Items[0].Instances[0].Kind = ScannerResultKind.AreaGroup;
            ScannerController controller = Controller(snapshot);

            ScannerCommandResult result = controller.ExecuteLookAround(15);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Wood", result.Result.Label);
            Assert.AreEqual(1, result.ResultCount);
        }

        [TestMethod]
        public void ExecuteLookAroundOrdersClockwiseFromNorthThenDistance()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Stone", 0, 10, "pickup:stone"),
                Entry("Pickups", "All", "Wood", 0, 5, "pickup:wood"),
                Entry("Buildings", "All", "Gold mine", 1, 9, "building:gold"),
                Entry("Wielders", "All", "Dead commander", 2, 8, "commander:dead"),
                Entry("Pickups", "All", "East", 5, 0, "pickup:east")));

            ScannerCommandResult first = controller.ExecuteLookAround(15);
            ScannerCommandResult second = controller.ExecuteMoveItem(1);
            ScannerCommandResult third = controller.ExecuteMoveItem(1);
            ScannerCommandResult fourth = controller.ExecuteMoveItem(1);
            ScannerCommandResult fifth = controller.ExecuteMoveItem(1);

            Assert.AreEqual("Wood", first.Result.Label);
            Assert.AreEqual("Stone", second.Result.Label);
            Assert.AreEqual("Gold mine", third.Result.Label);
            Assert.AreEqual("Dead commander", fourth.Result.Label);
            Assert.AreEqual("East", fifth.Result.Label);
            Assert.AreEqual("Wood", controller.ExecuteMoveItem(1).Result.Label);
            Assert.AreEqual(5, first.ResultCount);
        }

        [TestMethod]
        public void ExecuteLookAroundCreatesCategoryWithOriginalCategorySubcategories()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Gold", 0, 5, "pickup:gold"),
                Entry("Buildings", "All", "Mill", 5, 0, "building:mill")));

            ScannerCommandResult result = controller.ExecuteLookAround(15);
            ScannerCommandResult pickups = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual("Look around", result.CategoryLabel);
            Assert.AreEqual("All", result.SubcategoryLabel);
            Assert.AreEqual(2, result.ResultCount);
            Assert.AreEqual("Pickups", pickups.SubcategoryLabel);
            Assert.AreEqual("Gold", pickups.Result.Label);
        }

        [TestMethod]
        public void ExecuteMoveItemInsideLookAroundDoesNotRebuildNormalScanner()
        {
            ScannerSnapshot first = Snapshot(
                Entry("Pickups", "All", "Wood", 0, 5, "pickup:wood"),
                Entry("Pickups", "All", "Gold", 5, 0, "pickup:gold"));
            ScannerSnapshot second = Snapshot(
                Entry("Pickups", "All", "Stone", 0, 5, "pickup:stone"));
            int builds = 0;
            ScannerController controller = Controller(_ => builds++ == 0 ? first : second);

            controller.ExecuteLookAround(15);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteLookAroundPrunesStaleResults()
        {
            ScannerController controller = Controller(
                _ => Snapshot(
                    Entry("Pickups", "All", "Gone", 0, 1, "pickup:gone"),
                    Entry("Pickups", "All", "Wood", 0, 2, "pickup:wood")),
                () => Vector2Int.zero,
                candidate => candidate.Key != "pickup:gone");

            ScannerCommandResult result = controller.ExecuteLookAround(15);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Wood", result.Result.Label);
            Assert.AreEqual(1, result.ResultCount);
        }

        [TestMethod]
        public void ExecuteMoveCategoryExitsLookAroundAndRebuildsNormalScanner()
        {
            ScannerSnapshot first = Snapshot(
                Entry("Pickups", "All", "Gold", 0, 5, "pickup:gold"));
            ScannerSnapshot second = Snapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = Controller(_ => builds++ == 0 ? first : second);

            controller.ExecuteLookAround(15);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
        }
    }
}
