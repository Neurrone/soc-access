using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerControllerTests
    {
        [TestMethod]
        public void ExecuteRefreshReturnsSemanticResult()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            ScannerCommandResult result = controller.ExecuteRefresh();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("All", result.SubcategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(2, result.ResultCount);
            Assert.IsTrue(result.IncludePath);
        }

        [TestMethod]
        public void ExecuteMoveResultWrapsForwardWithinSubcategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteRefresh();
            controller.ExecuteMoveResult(1);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(2, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultBuildsSnapshotWhenNoneExists()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(2, result.ResultCount);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultWrapsBackwardWithinSubcategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteMoveResult(-1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.AreEqual(2, result.ResultIndex);
            Assert.AreEqual(2, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveCategoryWrapsAcrossNonEmptyCategories()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Terrain", "Roads", "Road", 2, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult terrain = controller.ExecuteMoveCategory(1);
            ScannerCommandResult pickups = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", terrain.CategoryLabel);
            Assert.AreEqual("Roads", terrain.SubcategoryLabel);
            Assert.AreEqual("Road", terrain.Result.Label);
            Assert.IsFalse(terrain.Wrapped);
            Assert.AreEqual("Pickups", pickups.CategoryLabel);
            Assert.AreEqual("All", pickups.SubcategoryLabel);
            Assert.AreEqual("Gold", pickups.Result.Label);
            Assert.IsTrue(pickups.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveSubcategorySkipsEmptyAndWraps()
        {
            ScannerSnapshot snapshot = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "Knowledge", "Ancient amber", 2, 0));
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("Empty");
            ScannerController controller = CreateController(snapshot);

            controller.ExecuteRefresh();
            ScannerCommandResult knowledge = controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult all = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual("Knowledge", knowledge.SubcategoryLabel);
            Assert.AreEqual("Ancient amber", knowledge.Result.Label);
            Assert.IsFalse(knowledge.Wrapped);
            Assert.AreEqual("All", all.SubcategoryLabel);
            Assert.AreEqual("Gold", all.Result.Label);
            Assert.IsTrue(all.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveSubcategoryWithSingleNonEmptySubcategoryReannouncesIt()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Terrain", "Roads", "Road", 1, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Roads", result.SubcategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultWrapsSingleResultAfterRebuildWithInitializedEmptyCategories()
        {
            ScannerController controller = CreateController(_ => BuildSnapshotWithEmptyCategoryBeforeTerrain());

            controller.ExecuteRefresh();
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Roads", result.SubcategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteJumpToCurrentLocatesTerrainAfterRebuildWithInitializedEmptyCategories()
        {
            Vector2Int jumpedTo = Vector2Int.zero;
            ScannerController controller = CreateController(
                _ => BuildSnapshotWithEmptyCategoryBeforeTerrain(),
                () => Vector2Int.zero,
                point =>
                {
                    jumpedTo = point;
                    return true;
                });

            controller.ExecuteRefresh();
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteJumpToCurrent();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual(new Vector2Int(2, 0), jumpedTo);
        }

        [TestMethod]
        public void ExecuteMoveCategoryWrapsForwardFromTerrainWithInitializedEmptyCategories()
        {
            ScannerController controller = CreateController(_ => BuildSnapshotWithEmptyCategoryBeforeTerrain());

            controller.ExecuteRefresh();
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultRebuildsAndPreservesCurrentIdentityBeforeMoving()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "A", 1, 0, "entity:a"),
                Entry("Pickups", "All", "B", 3, 0, "entity:b"),
                Entry("Pickups", "All", "C", 5, 0, "entity:c"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "A", 1, 0, "entity:a"),
                Entry("Pickups", "All", "New", 2, 0, "entity:new"),
                Entry("Pickups", "All", "B", 3, 0, "entity:b"),
                Entry("Pickups", "All", "C", 5, 0, "entity:c"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ < 2 ? first : second);

            controller.ExecuteRefresh();
            controller.ExecuteMoveResult(1);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("C", result.Result.Label);
            Assert.AreEqual("entity:c", result.Result.Key);
            Assert.AreEqual(4, result.ResultIndex);
            Assert.AreEqual(4, result.ResultCount);
        }

        [TestMethod]
        public void ExecuteMoveResultDoesNotFallbackToAllWhenCurrentKeyLeavesSubcategory()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Ore", 2, 0, "entity:ore"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Ore", 2, 0, "entity:ore"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteRefresh();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Unvisited", result.SubcategoryLabel);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.AreEqual("entity:ore", result.Result.Key);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultDoesNotMarkPruneRecoveryAsWrapped()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Ore", 2, 0, "entity:ore"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Ore", 2, 0, "entity:ore"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ < 2 ? first : second);

            controller.ExecuteRefresh();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Unvisited", result.SubcategoryLabel);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.AreEqual("entity:ore", result.Result.Key);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteSpeakOrientationRebuildsAndDoesNotFallbackWhenCurrentKeyLeavesSubcategory()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Gold", 1, 0, "entity:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ < 2 ? first : second);

            controller.ExecuteRefresh();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteSpeakOrientation();

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
        }

        [TestMethod]
        public void ExecuteJumpToCurrentRebuildsBeforeJumping()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 3, 0, "entity:gold"));
            int builds = 0;
            Vector2Int jumpedTo = Vector2Int.zero;
            ScannerController controller = CreateController(
                _ => builds++ == 0 ? first : second,
                () => Vector2Int.zero,
                point =>
                {
                    jumpedTo = point;
                    return true;
                });

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteJumpToCurrent();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual(new Vector2Int(3, 0), jumpedTo);
            Assert.AreEqual(new Vector2Int(3, 0), result.Result.Position);
        }

        [TestMethod]
        public void ExecuteMoveCategoryRebuildsFromLiveCursorAndReanchorsSortOrigin()
        {
            ScannerSnapshot snapshot = BuildSnapshot(
                Entry("Pickups", "All", "NearOrigin", 0, 0, "pickup:near"),
                Entry("Pickups", "All", "NearCursor", 10, 0, "pickup:far"),
                Entry("Terrain", "Roads", "Road", 5, 0, "terrain:road"));
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(_ => snapshot, () => cursor, (ScannerResult candidate) => true);

            controller.ExecuteRefresh();
            cursor = new Vector2Int(10, 0);
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("NearCursor", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveCategoryWrapsSingleNonEmptyCategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultDoesNotMarkNormalMovementAsWrapped()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveResultWrapsSingleResultBackward()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0)));

            controller.ExecuteRefresh();
            ScannerCommandResult result = controller.ExecuteMoveResult(-1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteSearchReturnsSearchResultsCategoryWithOriginalCategorySubcategories()
        {
            ScannerController controller = CreateController(BuildSnapshot(
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
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Old gold", 1, 0, "pickup:old-gold"),
                Entry("Pickups", "All", "Gold", 10, 0, "pickup:gold")));

            ScannerCommandResult result = controller.ExecuteSearch("gold");

            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual("pickup:gold", result.Result.Key);
        }

        [TestMethod]
        public void ExecuteSearchDeduplicatesResultsAlreadyInAllAndNamedSubcategories()
        {
            ScannerController controller = CreateController(BuildSnapshot(
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
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0)));

            ScannerCommandResult result = controller.ExecuteSearch("wood");

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
            Assert.AreEqual("No results", result.NoResultsText);
        }

        [TestMethod]
        public void ExecuteSearchNoMatchClearsPreviousSearchResults()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Gold pile", 2, 0, "pickup:gold-pile"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult noResults = controller.ExecuteSearch("amber");
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.NoResults, noResults.Status);
            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreNotEqual("Search Results", result.CategoryLabel);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Wood", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteMoveResultInsideSearchDoesNotRebuildNormalScanner()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Gold pile", 2, 0, "pickup:gold-pile"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold pile", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteMoveCategoryExitsSearchAndRebuildsNormalScanner()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteLookAroundFiltersByGameCircleAndExcludesOrigin()
        {
            ScannerController controller = CreateController(BuildSnapshot(
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
            ScannerSnapshot snapshot = BuildSnapshot(
                Entry("Terrain", "Roads", "Road tiles", 0, 1, "terrain:road"),
                Entry("Obstacles", "All", "5 blocked tiles", 1, 0, "blocked:area"),
                Entry("Pickups", "All", "Wood", 0, 2, "pickup:wood"));
            snapshot.Categories[0].Subcategories[0].Results[0].Kind = ScannerResultKind.TerrainGroup;
            snapshot.Categories[1].Subcategories[0].Results[0].Kind = ScannerResultKind.AreaGroup;
            ScannerController controller = CreateController(snapshot);

            ScannerCommandResult result = controller.ExecuteLookAround(15);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Wood", result.Result.Label);
            Assert.AreEqual(1, result.ResultCount);
        }

        [TestMethod]
        public void ExecuteLookAroundOrdersClockwiseFromNorthThenDistance()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Stone", 0, 10, "pickup:stone"),
                Entry("Pickups", "All", "Wood", 0, 5, "pickup:wood"),
                Entry("Buildings", "All", "Gold mine", 1, 9, "building:gold"),
                Entry("Wielders", "All", "Dead commander", 2, 8, "commander:dead"),
                Entry("Pickups", "All", "East", 5, 0, "pickup:east")));

            ScannerCommandResult first = controller.ExecuteLookAround(15);
            ScannerCommandResult second = controller.ExecuteMoveResult(1);
            ScannerCommandResult third = controller.ExecuteMoveResult(1);
            ScannerCommandResult fourth = controller.ExecuteMoveResult(1);
            ScannerCommandResult fifth = controller.ExecuteMoveResult(1);

            Assert.AreEqual("Wood", first.Result.Label);
            Assert.AreEqual("Stone", second.Result.Label);
            Assert.AreEqual("Gold mine", third.Result.Label);
            Assert.AreEqual("Dead commander", fourth.Result.Label);
            Assert.AreEqual("East", fifth.Result.Label);
            Assert.AreEqual(5, first.ResultCount);
        }

        [TestMethod]
        public void ExecuteLookAroundCreatesCategoryWithOriginalCategorySubcategories()
        {
            ScannerController controller = CreateController(BuildSnapshot(
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
        public void ExecuteMoveResultInsideLookAroundDoesNotRebuildNormalScanner()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 0, 5, "pickup:wood"),
                Entry("Pickups", "All", "Gold", 5, 0, "pickup:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Stone", 0, 5, "pickup:stone"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteLookAround(15);
            ScannerCommandResult result = controller.ExecuteMoveResult(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
        }

        [TestMethod]
        public void ExecuteLookAroundPrunesStaleResults()
        {
            ScannerController controller = CreateController(
                _ => BuildSnapshot(
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
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 0, 5, "pickup:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"),
                Entry("Terrain", "Roads", "Road", 2, 0, "terrain:road"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteLookAround(15);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
        }

        private static ScannerController CreateController(ScannerSnapshot snapshot)
        {
            return CreateController(_ => snapshot);
        }

        private static ScannerController CreateController(System.Func<Vector2Int, ScannerSnapshot> snapshotBuilder)
        {
            return CreateController(snapshotBuilder, () => Vector2Int.zero, (ScannerResult result) => true);
        }

        private static ScannerController CreateController(
            System.Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            System.Func<Vector2Int> cursorProvider,
            System.Func<Vector2Int, bool> jumpTo)
        {
            return CreateController(snapshotBuilder, cursorProvider, _ => true, jumpTo);
        }

        private static ScannerController CreateController(
            System.Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            System.Func<Vector2Int> cursorProvider,
            System.Func<ScannerResult, bool> validator)
        {
            return CreateController(snapshotBuilder, cursorProvider, validator, _ => true);
        }

        private static ScannerController CreateController(
            System.Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            System.Func<Vector2Int> cursorProvider,
            System.Func<ScannerResult, bool> validator,
            System.Func<Vector2Int, bool> jumpTo)
        {
            return new ScannerController(
                snapshotBuilder,
                cursorProvider,
                validator,
                jumpTo,
                (result, directions, index, count) => null,
                ScannerDirectionMode.Square);
        }

        private static ScannerSnapshot BuildSnapshot(params ScannerEntry[] entries)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            for (int i = 0; i < entries.Length; i++)
            {
                ScannerEntry entry = entries[i];
                snapshot.Add(entry.Category, entry.Subcategory, new ScannerResult(entry.Key, entry.Label, entry.Position));
            }

            return snapshot;
        }

        private static ScannerSnapshot BuildSnapshotWithEmptyCategoryBeforeTerrain()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:gold", "Gold", new Vector2Int(1, 0)));
            snapshot.GetOrAddCategory("Empty").GetOrAddSubcategory("All");
            snapshot.Add("Terrain", "Roads", new ScannerResult("terrain:road", "Road", new Vector2Int(2, 0)));
            return snapshot;
        }

        private static ScannerEntry Entry(string category, string subcategory, string label, int x, int y)
        {
            return Entry(category, subcategory, label, x, y, category + ":" + subcategory + ":" + label + ":" + x + ":" + y);
        }

        private static ScannerEntry Entry(string category, string subcategory, string label, int x, int y, string key)
        {
            return new ScannerEntry
            {
                Category = category,
                Subcategory = subcategory,
                Label = label,
                Position = new Vector2Int(x, y),
                Key = key
            };
        }

        private sealed class ScannerEntry
        {
            public string Category;
            public string Subcategory;
            public string Label;
            public string Key;
            public Vector2Int Position;
        }
    }
}
