using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using static SongsOfConquestAccess.Tests.ScannerFixtures;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>Search mode: the category the scanner puts a phrase's matches in, how it orders and
    /// prunes them, and the keys that leave it again.</summary>
    [TestClass]
    public sealed class ScannerSearchModeTests
    {
        [TestMethod]
        public void ExecuteSearchReturnsSearchResultsCategoryWithOriginalCategorySubcategories()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Gold Mine", 3, 0, "pickup:gold"),
                Entry("Buildings", "All", "Gold Mine", 1, 0, "building:gold"),
                Entry("Troop Sources", "All", "Rally point", 2, 0, "troop:rally")));

            ScannerCommandResult result = controller.ExecuteSearch("gold");

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Search Results", result.CategoryLabel);
            Assert.AreEqual("All", result.SubcategoryLabel);
            Assert.AreEqual("Gold Mine", result.Result.Label);
            Assert.AreEqual("building:gold", result.Result.Key);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(2, result.ResultCount);

            ScannerCommandResult pickups = controller.ExecuteMoveSubcategory(1);
            Assert.AreEqual("Search Results", pickups.CategoryLabel);
            Assert.AreEqual("Pickups", pickups.SubcategoryLabel);
            Assert.AreEqual("pickup:gold", pickups.Result.Key);
        }

        [TestMethod]
        public void ExecuteSearchSortsByMatchTierBeforeDistance()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Old gold", 1, 0, "pickup:old-gold"),
                Entry("Pickups", "All", "Gold", 10, 0, "pickup:gold")));

            ScannerCommandResult result = controller.ExecuteSearch("gold");

            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual("pickup:gold", result.Result.Key);
        }

        [TestMethod]
        public void ExecuteSearchDeduplicatesResultsAlreadyInAllAndNamedSubcategories()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "Riches", "Gold", 1, 0, "pickup:gold")));

            ScannerCommandResult result = controller.ExecuteSearch("gold");

            Assert.AreEqual(1, result.ResultCount);
            ScannerCommandResult riches = controller.ExecuteMoveSubcategory(1);
            Assert.AreEqual("Pickups", riches.SubcategoryLabel);
            Assert.AreEqual(1, riches.ResultCount);
        }

        [TestMethod]
        public void ExecuteSearchNoMatchUsesSearchNoResultsText()
        {
            ScannerController controller = Controller(Snapshot(
                Entry("Pickups", "All", "Gold", 1, 0)));

            ScannerCommandResult result = controller.ExecuteSearch("wood");

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
            Assert.AreEqual("No results", result.NoResultsText);
        }

        [TestMethod]
        public void ExecuteSearchNoMatchClearsPreviousSearchResults()
        {
            ScannerSnapshot first = Snapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Gold pile", 2, 0, "pickup:gold-pile"));
            ScannerSnapshot second = Snapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = Controller(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult noResults = controller.ExecuteSearch("amber");
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.NoResults, noResults.Status);
            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreNotEqual("Search Results", result.CategoryLabel);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Wood", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteMoveItemInsideSearchDoesNotRebuildNormalScanner()
        {
            ScannerSnapshot first = Snapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Gold pile", 2, 0, "pickup:gold-pile"));
            ScannerSnapshot second = Snapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"));
            int builds = 0;
            ScannerController controller = Controller(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold pile", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteMoveCategoryExitsSearchAndRebuildsNormalScanner()
        {
            ScannerSnapshot first = Snapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"));
            ScannerSnapshot second = Snapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = Controller(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
        }
    }
}
