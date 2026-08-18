using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerControllerTests
    {
        [TestMethod]
        public void ExecuteInitialLandingReturnsSemanticResult()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("All", result.SubcategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.IncludePath);
        }

        [TestMethod]
        public void ExecuteInitialLandingSkipsInitializedEmptyFirstScope()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("Unvisited");
            snapshot.Add("Terrain", "Roads", new ScannerResult("terrain:road", "Road", new Vector2Int(2, 0)));
            ScannerController controller = CreateController(snapshot);

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Roads", result.SubcategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.IncludePath);
        }

        [TestMethod]
        public void ExecuteMoveItemWrapsForwardWithinSubcategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveItem(1);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemBuildsSnapshotWhenNoneExists()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemWrapsBackwardWithinSubcategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteMoveItem(-1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.AreEqual(1, result.ResultIndex);
            Assert.AreEqual(1, result.ResultCount);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveCategoryWrapsAcrossNonEmptyCategories()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Terrain", "Roads", "Road", 2, 0)));

            controller.ExecuteInitialLanding();
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

            controller.ExecuteInitialLanding();
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

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Roads", result.SubcategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemWrapsSingleResultAfterRebuildWithInitializedEmptyCategories()
        {
            ScannerController controller = CreateController(_ => BuildSnapshotWithEmptyCategoryBeforeTerrain());

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

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

            controller.ExecuteInitialLanding();
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

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemRebuildsAndPreservesCurrentIdentityBeforeMoving()
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

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveItem(1);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("C", result.Result.Label);
            Assert.AreEqual("entity:c", result.Result.Key);
        }

        [TestMethod]
        public void ExecuteMoveItemDoesNotFallbackToAllWhenCurrentKeyLeavesSubcategory()
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

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

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
        public void ExecuteMoveItemDoesNotMarkPruneRecoveryAsWrapped()
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

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Unvisited", result.SubcategoryLabel);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.AreEqual("entity:ore", result.Result.Key);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteSpeakDistanceAndDirectionRebuildsAndDoesNotFallbackWhenCurrentKeyLeavesSubcategory()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"),
                Entry("Pickups", "Unvisited", "Gold", 1, 0, "entity:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "entity:gold"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ < 2 ? first : second);

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveSubcategory(1);
            ScannerCommandResult result = controller.ExecuteSpeakDistanceAndDirection();

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

            controller.ExecuteInitialLanding();
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

            controller.ExecuteInitialLanding();
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

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemDoesNotMarkNormalMovementAsWrapped()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Pickups", "All", "Ore", 2, 0)));

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Ore", result.Result.Label);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemWrapsSingleResultBackward()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0)));

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteMoveItem(-1);

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
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Gold pile", 2, 0, "pickup:gold-pile"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 1, 0, "pickup:wood"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteSearch("gold");
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

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
            snapshot.Categories[0].Subcategories[0].Items[0].Instances[0].Kind = ScannerResultKind.TerrainGroup;
            snapshot.Categories[1].Subcategories[0].Items[0].Instances[0].Kind = ScannerResultKind.AreaGroup;
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
        public void ExecuteMoveItemInsideLookAroundDoesNotRebuildNormalScanner()
        {
            ScannerSnapshot first = BuildSnapshot(
                Entry("Pickups", "All", "Wood", 0, 5, "pickup:wood"),
                Entry("Pickups", "All", "Gold", 5, 0, "pickup:gold"));
            ScannerSnapshot second = BuildSnapshot(
                Entry("Pickups", "All", "Stone", 0, 5, "pickup:stone"));
            int builds = 0;
            ScannerController controller = CreateController(_ => builds++ == 0 ? first : second);

            controller.ExecuteLookAround(15);
            ScannerCommandResult result = controller.ExecuteMoveItem(1);

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

        [TestMethod]
        public void SpeakDistanceAndDirectionReadsTheBearingWithoutATotal()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Terrain", "Roads", "Road", 2, 3, "terrain:road")));

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteSpeakDistanceAndDirection();

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.IsTrue(result.DistanceAndDirectionOnly);
            Assert.AreEqual("3n, 2e", result.FormatDistanceAndDirection(useLongDirections: false));
        }

        [TestMethod]
        public void SpeakDistanceAndDirectionSaysHereOnTheCursor()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Terrain", "Roads", "Road", 0, 0, "terrain:road")));

            controller.ExecuteInitialLanding();
            ScannerCommandResult result = controller.ExecuteSpeakDistanceAndDirection();

            Assert.AreEqual("here", result.FormatDistanceAndDirection(useLongDirections: false));
        }

        [TestMethod]
        public void SpeakDistanceAndDirectionReportsNoResultsBeforeAnythingIsScanned()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Terrain", "Roads", "Road", 2, 3, "terrain:road")));

            ScannerCommandResult result = controller.ExecuteSpeakDistanceAndDirection();

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
        }

        /// <summary>
        /// The item name gets one turn per item, and the bearing readout never
        /// speaks it, so it must not be the announcement that spends it. The
        /// key can move the walk on its own: with the snapshot gone from under
        /// it, which a search that found nothing is enough to do, it lands on
        /// the nearest scope, and that is how it ends up sitting on an item
        /// whose name the player has never been told.
        /// </summary>
        [TestMethod]
        public void SpeakDistanceAndDirectionLeavesTheItemNameTurnUnspent()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0, "pickup:gold"),
                Entry("Pickups", "All", "Ore", 2, 0, "pickup:ore")));

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveItem(1);
            controller.ExecuteSearch("nothing by this name");
            ScannerCommandResult bearing = controller.ExecuteSpeakDistanceAndDirection();
            ScannerCommandResult announced = controller.ExecuteJumpToCurrent();

            Assert.AreEqual("Gold", bearing.Result.Label);
            Assert.IsFalse(bearing.IncludeItemName);
            Assert.AreEqual("Gold", announced.Result.Label);
            Assert.IsTrue(announced.IncludeItemName);
        }

        [TestMethod]
        public void FirstSubcategoryStepLandsInsteadOfSteppingOutOfAnEmptyCategory()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.GetOrAddCategory("Pickups").GetOrAddSubcategory("All");
            snapshot.Add("Wielders", "All", new ScannerResult("commander:1", "Cara", new Vector2Int(2, 0)));
            ScannerController controller = CreateController(snapshot);

            ScannerCommandResult result = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("Wielders", result.CategoryLabel);
            Assert.AreEqual("Cara", result.Result.Label);
        }

        [TestMethod]
        public void FirstCategoryStepLandsOnTheFirstCategoryWithResults()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Terrain", "Roads", "Road", 2, 0)));

            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Pickups", result.CategoryLabel);
            Assert.AreEqual("Gold", result.Result.Label);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void SecondCategoryStepMovesOnNormally()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Gold", 1, 0),
                Entry("Terrain", "Roads", "Road", 2, 0)));

            controller.ExecuteMoveCategory(1);
            ScannerCommandResult result = controller.ExecuteMoveCategory(1);

            Assert.AreEqual("Terrain", result.CategoryLabel);
            Assert.AreEqual("Road", result.Result.Label);
        }

        [TestMethod]
        public void RefreshMovesTheResultBeforeDirectionsAreBuilt()
        {
            ScannerController controller = CreateController(
                _ => BuildSnapshot(Entry("Terrain", "Roads", "Road", 10, 0, "terrain:road")),
                () => Vector2Int.zero,
                (candidate, cursorHint) => ScannerResultRefresh.Valid(new Vector2Int(0, 3)),
                _ => true);

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual(new Vector2Int(0, 3), result.Result.Position);
            Assert.AreEqual(1, result.Directions.Count);
            Assert.AreEqual(ScannerDirection.North, result.Directions[0].Direction);
            Assert.AreEqual(3, result.Directions[0].Count);
        }

        [TestMethod]
        public void RefreshIsGivenTheLiveCursorAsItsHint()
        {
            Vector2Int cursor = new Vector2Int(4, 7);
            Vector2Int hint = new Vector2Int(-1, -1);
            ScannerController controller = CreateController(
                _ => BuildSnapshot(Entry("Terrain", "Roads", "Road", 10, 0, "terrain:road")),
                () => cursor,
                (candidate, cursorHint) =>
                {
                    hint = cursorHint;
                    return ScannerResultRefresh.Valid(candidate.Position);
                },
                _ => true);

            controller.ExecuteInitialLanding();

            Assert.AreEqual(cursor, hint);
        }

        [TestMethod]
        public void RefreshKeepsTheBuiltLabel()
        {
            ScannerController controller = CreateController(
                _ => BuildSnapshot(Entry("Wielders", "All", "Built name", 1, 0, "commander:1")),
                () => Vector2Int.zero,
                (candidate, cursorHint) => ScannerResultRefresh.Valid(candidate.Position),
                _ => true);

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual("Built name", result.Result.Label);
        }

        [TestMethod]
        public void ThingsSharingANameBecomeOneStopInTheItemCycle()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest-1"),
                Entry("Pickups", "All", "Chest", 2, 0, "pickup:chest-2"),
                Entry("Pickups", "All", "Ancient amber", 3, 0, "pickup:amber")));

            ScannerCommandResult chest = controller.ExecuteInitialLanding();
            ScannerCommandResult amber = controller.ExecuteMoveItem(1);
            ScannerCommandResult wrapped = controller.ExecuteMoveItem(1);

            Assert.AreEqual("Chest", chest.Result.Label);
            Assert.AreEqual("pickup:chest-1", chest.Result.Key);
            Assert.AreEqual(1, chest.ResultIndex);
            Assert.AreEqual(2, chest.ResultCount);
            Assert.AreEqual("Ancient amber", amber.Result.Label);
            Assert.AreEqual(1, amber.ResultCount);
            Assert.AreEqual("pickup:chest-1", wrapped.Result.Key);
            Assert.IsTrue(wrapped.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveInstanceWalksTheCopiesOfTheCurrentItem()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest-1"),
                Entry("Pickups", "All", "Chest", 2, 0, "pickup:chest-2"),
                Entry("Pickups", "All", "Ancient amber", 3, 0, "pickup:amber")));

            controller.ExecuteInitialLanding();
            ScannerCommandResult second = controller.ExecuteMoveInstance(1);
            ScannerCommandResult wrapped = controller.ExecuteMoveInstance(1);

            Assert.AreEqual("pickup:chest-2", second.Result.Key);
            Assert.AreEqual(2, second.ResultIndex);
            Assert.AreEqual(2, second.ResultCount);
            Assert.IsFalse(second.Wrapped);
            Assert.AreEqual("pickup:chest-1", wrapped.Result.Key);
            Assert.IsTrue(wrapped.Wrapped);
        }

        [TestMethod]
        public void ExecuteMoveItemLandsOnTheNearestCopyOfTheNewItem()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest-1"),
                Entry("Pickups", "All", "Chest", 9, 0, "pickup:chest-2"),
                Entry("Pickups", "All", "Ancient amber", 3, 0, "pickup:amber")));

            controller.ExecuteInitialLanding();
            controller.ExecuteMoveInstance(1);
            controller.ExecuteMoveItem(1);
            ScannerCommandResult back = controller.ExecuteMoveItem(-1);

            Assert.AreEqual("pickup:chest-1", back.Result.Key);
            Assert.AreEqual(1, back.ResultIndex);
        }

        [TestMethod]
        public void TheItemNameLeadsAChangeOfItemAndIsDroppedBetweenCopies()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest-1"),
                Entry("Pickups", "All", "Chest", 2, 0, "pickup:chest-2"),
                Entry("Pickups", "All", "Ancient amber", 3, 0, "pickup:amber")));

            ScannerCommandResult landing = controller.ExecuteInitialLanding();
            ScannerCommandResult sameItem = controller.ExecuteMoveInstance(1);
            ScannerCommandResult newItem = controller.ExecuteMoveItem(1);

            Assert.IsTrue(landing.IncludeItemName);
            Assert.IsFalse(sameItem.IncludeItemName);
            Assert.IsTrue(newItem.IncludeItemName);
        }

        [TestMethod]
        public void TheItemNameLeadsAgainWhenTheSameThingIsReachedThroughAnotherSubcategory()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest"),
                Entry("Pickups", "Unvisited", "Chest", 1, 0, "pickup:chest")));

            controller.ExecuteInitialLanding();
            ScannerCommandResult unvisited = controller.ExecuteMoveSubcategory(1);

            Assert.AreEqual("Unvisited", unvisited.SubcategoryLabel);
            Assert.IsTrue(unvisited.IncludeItemName);
        }

        [TestMethod]
        public void LookAroundKeepsOneItemPerResultSoTheSweepIsNotRegrouped()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 0, 5, "pickup:chest-north"),
                Entry("Pickups", "All", "Wood", 5, 0, "pickup:wood"),
                Entry("Pickups", "All", "Chest", 0, -5, "pickup:chest-south")));

            ScannerCommandResult north = controller.ExecuteLookAround(15);
            ScannerCommandResult east = controller.ExecuteMoveItem(1);
            ScannerCommandResult south = controller.ExecuteMoveItem(1);

            Assert.AreEqual("pickup:chest-north", north.Result.Key);
            Assert.AreEqual("pickup:wood", east.Result.Key);
            Assert.AreEqual("pickup:chest-south", south.Result.Key);
        }

        /// <summary>
        /// The sweep is one result per item, so counting the copies of an item
        /// would answer "1 of 1" all the way round and leave the player with no
        /// idea how much is near them or how far through it they are.
        /// </summary>
        [TestMethod]
        public void LookAroundCountsThePositionOverTheWholeSweep()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 0, 5, "pickup:chest-north"),
                Entry("Pickups", "All", "Wood", 5, 0, "pickup:wood"),
                Entry("Pickups", "All", "Chest", 0, -5, "pickup:chest-south")));

            ScannerCommandResult first = controller.ExecuteLookAround(15);
            ScannerCommandResult second = controller.ExecuteMoveItem(1);
            ScannerCommandResult third = controller.ExecuteMoveItem(1);

            Assert.AreEqual(1, first.ResultIndex);
            Assert.AreEqual(3, first.ResultCount);
            Assert.AreEqual(2, second.ResultIndex);
            Assert.AreEqual(3, second.ResultCount);
            Assert.AreEqual(3, third.ResultIndex);
            Assert.AreEqual(3, third.ResultCount);
        }

        /// <summary>
        /// The shape of the revealed list, which is flat for the same reason
        /// Look Around is: the order of the whole sequence is the information,
        /// so the position has to be measured over it.
        /// </summary>
        [TestMethod]
        public void FlatSubcategoryCountsThePositionOverItsItems()
        {
            ScannerController controller = CreateController(BuildFlatSnapshot(
                Entry("Exploration", "Revealed", "Chest", 1, 0, "revealed:chest-near"),
                Entry("Exploration", "Revealed", "Chest", 2, 0, "revealed:chest-far")));

            ScannerCommandResult first = controller.ExecuteInitialLanding();
            ScannerCommandResult second = controller.ExecuteMoveItem(1);

            Assert.AreEqual(1, first.ResultIndex);
            Assert.AreEqual(2, first.ResultCount);
            Assert.AreEqual(2, second.ResultIndex);
            Assert.AreEqual(2, second.ResultCount);
        }

        /// <summary>
        /// The other half of the rule: a grouped scope still counts the copies
        /// of the item the player is walking, which is what the item cycle
        /// leaves the instance cycle to step through.
        /// </summary>
        [TestMethod]
        public void GroupedSubcategoryStillCountsTheCopiesOfTheItem()
        {
            ScannerController controller = CreateController(BuildSnapshot(
                Entry("Pickups", "All", "Chest", 1, 0, "pickup:chest-near"),
                Entry("Pickups", "All", "Chest", 2, 0, "pickup:chest-far")));

            ScannerCommandResult first = controller.ExecuteInitialLanding();
            ScannerCommandResult second = controller.ExecuteMoveInstance(1);

            Assert.AreEqual(1, first.ResultIndex);
            Assert.AreEqual(2, first.ResultCount);
            Assert.AreEqual(2, second.ResultIndex);
            Assert.AreEqual(2, second.ResultCount);
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
            return CreateController(
                snapshotBuilder,
                cursorProvider,
                (result, cursorHint) => validator(result)
                    ? ScannerResultRefresh.Valid(result.Position)
                    : ScannerResultRefresh.Invalid,
                jumpTo);
        }

        private static ScannerController CreateController(
            System.Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            System.Func<Vector2Int> cursorProvider,
            System.Func<ScannerResult, Vector2Int, ScannerResultRefresh> refreshResult,
            System.Func<Vector2Int, bool> jumpTo)
        {
            return new ScannerController(
                snapshotBuilder,
                cursorProvider,
                refreshResult,
                jumpTo,
                (result, directions, index, count, includeItemName) => null,
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

        /// <summary>
        /// The same entries under a category that hands out flat subcategories,
        /// which is how the taxonomy declares the revealed list.
        /// </summary>
        private static ScannerSnapshot BuildFlatSnapshot(params ScannerEntry[] entries)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            for (int i = 0; i < entries.Length; i++)
            {
                ScannerEntry entry = entries[i];
                ScannerCategory category = snapshot.GetOrAddCategory(entry.Category);
                category.FlatItems = true;
                category.GetOrAddSubcategory(entry.Subcategory)
                    .Add(new ScannerResult(entry.Key, entry.Label, entry.Position));
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
