using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The single-key walk over one custom category. What separates it from the
    /// paging cycles is that it ignores the item grouping, so the tests care
    /// most about instances that share an item and would otherwise sit behind
    /// an instance axis this key does not have.
    /// </summary>
    [TestClass]
    public sealed class ScannerCustomCategoryWalkTests
    {
        private const string CategoryKey = "custom:1";

        [TestMethod]
        public void WalkCrossesItemsAndReachesEveryInstance()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult first = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult second = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult third = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual("chest:near", first.Result.Key);
            Assert.AreEqual("gold", second.Result.Key);
            // The second chest shares an item with the first, so the paging item
            // cycle would need its instance axis to get here.
            Assert.AreEqual("chest:far", third.Result.Key);
        }

        [TestMethod]
        public void WalkCountsThePositionOverTheWholeFlattenedList()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult first = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult third = Repeat(controller, 1, 2);

            Assert.AreEqual(1, first.ResultIndex);
            Assert.AreEqual(3, first.ResultCount);
            Assert.AreEqual(3, third.ResultIndex);
            Assert.AreEqual(3, third.ResultCount);
        }

        [TestMethod]
        public void WalkLeavesTheCategoryPathUnspoken()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult result = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.IsFalse(result.IncludePath);
        }

        [TestMethod]
        public void WalkLeadsWithTheItemNameOnlyWhenItChanges()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(
                () => cursor,
                () => BuildSnapshot(
                    Result("chest:near", "Chest", 1, 0),
                    Result("chest:far", "Chest", 2, 0),
                    Result("gold", "Gold", 3, 0)));

            ScannerCommandResult first = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult second = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult third = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.IsTrue(first.IncludeItemName);
            Assert.IsFalse(second.IncludeItemName);
            Assert.IsTrue(third.IncludeItemName);
        }

        [TestMethod]
        public void WalkWrapsAtTheEnd()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult wrapped = Repeat(controller, 1, 4);

            Assert.AreEqual("chest:near", wrapped.Result.Key);
            Assert.IsTrue(wrapped.Wrapped);
        }

        [TestMethod]
        public void WalkRunsBackwards()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult back = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, -1);

            Assert.AreEqual("chest:far", back.Result.Key);
            Assert.IsTrue(back.Wrapped);
        }

        [TestMethod]
        public void CursorAwayFromTheOriginRestartsNearestFirst()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            cursor = new Vector2Int(10, 0);
            ScannerCommandResult restarted = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual("chest:far", restarted.Result.Key);
            Assert.AreEqual(1, restarted.ResultIndex);
            Assert.IsFalse(restarted.Wrapped);
        }

        [TestMethod]
        public void CursorParkedOnTheCurrentEntryStepsOnInsteadOfRelanding()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult landed = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            // What a jump to the entry leaves behind: the cursor is on it, and
            // the nearest entry to the cursor is the one already underneath it.
            cursor = landed.Result.Position;
            ScannerCommandResult stepped = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual("chest:near", landed.Result.Key);
            Assert.AreEqual("gold", stepped.Result.Key);
        }

        [TestMethod]
        public void CursorParkedOnTheCurrentEntryStepsBackwardsToo()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult landed = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            cursor = landed.Result.Position;
            ScannerCommandResult stepped = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, -1);

            Assert.AreEqual("chest:far", stepped.Result.Key);
        }

        [TestMethod]
        public void CategoryThatIsNotInTheSnapshotReportsNoResults()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            ScannerCommandResult result = controller.ExecuteMoveCustomCategoryEntry("custom:99", 1);

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
        }

        /// <summary>
        /// One press is one scan. The walk restarting because the rebuild left
        /// it outside the category used to scan the whole map a second time at
        /// the same cursor, which on a large map is the difference between a
        /// key that answers and one that stutters.
        /// </summary>
        [TestMethod]
        public void RestartingTheWalkScansTheMapOnceForOnePress()
        {
            Vector2Int cursor = Vector2Int.zero;
            int builds = 0;
            ScannerController controller = CreateController(
                () => cursor,
                () =>
                {
                    builds++;
                    return builds <= 1
                        ? BuildSnapshotWithTheCategoryFirst()
                        : BuildSnapshotWithTheCategoryLast();
                });

            controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult restarted = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual(2, builds);
            Assert.AreEqual(ScannerCommandStatus.Result, restarted.Status);
            Assert.AreEqual("chest:near", restarted.Result.Key);
            Assert.AreEqual(1, restarted.ResultCount);
        }

        /// <summary>
        /// A contribution that throws while a snapshot is built is swallowed
        /// and carried on from, so the next snapshot can arrive without the
        /// custom categories the one before it had. The walk has to come out of
        /// that standing somewhere real: it used to keep the indices it had
        /// measured against the snapshot before, and the press after reached
        /// past the end of the new one.
        /// </summary>
        [TestMethod]
        public void KeyPressSurvivesTheCategoryVanishingFromTheSnapshot()
        {
            Vector2Int cursor = Vector2Int.zero;
            bool categoryIsGone = false;
            ScannerController controller = CreateController(
                () => cursor,
                () => categoryIsGone ? BuildSnapshotWithoutTheCategory() : BuildSnapshotWithTheCategoryLast());

            controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            cursor = new Vector2Int(5, 5);
            categoryIsGone = true;
            ScannerCommandResult gone = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult again = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            ScannerCommandResult paged = controller.ExecuteMoveCategory(1);

            Assert.AreEqual(ScannerCommandStatus.NoResults, gone.Status);
            Assert.AreEqual(ScannerCommandStatus.NoResults, again.Status);
            Assert.AreEqual(ScannerCommandStatus.Result, paged.Status);
            Assert.AreEqual("gold", paged.Result.Key);
        }

        [TestMethod]
        public void EmptyCategoryReportsNoResults()
        {
            ScannerController controller = CreateController(
                () => Vector2Int.zero,
                () =>
                {
                    ScannerSnapshot snapshot = new ScannerSnapshot();
                    snapshot.GetOrAddCategory(CategoryKey).GetOrAddSubcategory(ScannerSubcategoryKeys.All);
                    snapshot.Add("pickups", ScannerSubcategoryKeys.All, new ScannerResult("gold", "Gold", new Vector2Int(1, 0)));
                    return snapshot;
                });

            ScannerCommandResult result = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual(ScannerCommandStatus.NoResults, result.Status);
        }

        /// <summary>
        /// A search freezes the snapshot, and the walk has to step out of it the
        /// same way the category cycle does rather than walking the frozen copy.
        /// </summary>
        [TestMethod]
        public void WalkLeavesASearchSnapshotBehind()
        {
            Vector2Int cursor = Vector2Int.zero;
            ScannerController controller = CreateController(() => cursor);

            controller.ExecuteSearch("Gold");
            ScannerCommandResult result = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            Assert.AreEqual(ScannerCommandStatus.Result, result.Status);
            Assert.AreEqual("chest:near", result.Result.Key);
            Assert.AreEqual(3, result.ResultCount);
        }

        [TestMethod]
        public void PrunedEntryDoesNotStrandTheWalk()
        {
            Vector2Int cursor = Vector2Int.zero;
            bool goldIsGone = false;
            ScannerController controller = new ScannerController(
                _ => BuildSnapshot(
                    Result("chest:near", "Chest", 1, 0),
                    Result("gold", "Gold", 2, 0),
                    Result("chest:far", "Chest", 3, 0)),
                () => cursor,
                (result, cursorHint) => !goldIsGone || result.Key != "gold"
                    ? ScannerResultRefresh.Valid(result.Position)
                    : ScannerResultRefresh.Invalid,
                _ => true,
                (result, directions, index, count, includeItemName) => null,
                ScannerDirectionMode.Square);

            controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);
            goldIsGone = true;
            ScannerCommandResult stepped = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, 1);

            // Which surviving entry the walk lands on is the shared prune and
            // clamp behaviour the paging cycles have too. What this key owes the
            // player is that the entry that has gone is never announced and that
            // the count is the one the next press will step through.
            Assert.AreEqual(ScannerCommandStatus.Result, stepped.Status);
            Assert.AreNotEqual("gold", stepped.Result.Key);
            Assert.AreEqual(2, stepped.ResultCount);
            Assert.IsTrue(stepped.ResultIndex >= 1 && stepped.ResultIndex <= 2);
        }

        private static ScannerCommandResult Repeat(ScannerController controller, int delta, int times)
        {
            ScannerCommandResult result = null;
            for (int i = 0; i < times; i++)
            {
                result = controller.ExecuteMoveCustomCategoryEntry(CategoryKey, delta);
            }

            return result;
        }

        private static ScannerController CreateController(Func<Vector2Int> cursorProvider)
        {
            return CreateController(
                cursorProvider,
                () => BuildSnapshot(
                    Result("chest:near", "Chest", 1, 0),
                    Result("gold", "Gold", 2, 0),
                    Result("chest:far", "Chest", 3, 0)));
        }

        private static ScannerController CreateController(
            Func<Vector2Int> cursorProvider,
            Func<ScannerSnapshot> snapshotBuilder)
        {
            return new ScannerController(
                _ => snapshotBuilder(),
                cursorProvider,
                (result, cursorHint) => ScannerResultRefresh.Valid(result.Position),
                _ => true,
                (result, directions, index, count, includeItemName) => null,
                ScannerDirectionMode.Square);
        }

        /// <summary>
        /// Mirrors what the synthesizer produces: an All subcategory holding
        /// every result the category gathers, grouped into items by name.
        /// </summary>
        private static ScannerSnapshot BuildSnapshot(params ScannerResult[] results)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            for (int i = 0; i < results.Length; i++)
            {
                snapshot.Add(CategoryKey, ScannerSubcategoryKeys.All, results[i]);
            }

            return snapshot;
        }

        /// <summary>
        /// The custom category sits behind a built-in one, which is what makes
        /// the seat an index the smaller snapshot below has no room for.
        /// </summary>
        private static ScannerSnapshot BuildSnapshotWithTheCategoryLast()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("pickups", ScannerSubcategoryKeys.All, Result("gold", "Gold", 2, 0));
            snapshot.Add(CategoryKey, ScannerSubcategoryKeys.All, Result("chest:near", "Chest", 1, 0));
            return snapshot;
        }

        private static ScannerSnapshot BuildSnapshotWithTheCategoryFirst()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add(CategoryKey, ScannerSubcategoryKeys.All, Result("chest:near", "Chest", 1, 0));
            snapshot.Add("pickups", ScannerSubcategoryKeys.All, Result("gold", "Gold", 2, 0));
            return snapshot;
        }

        private static ScannerSnapshot BuildSnapshotWithoutTheCategory()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("pickups", ScannerSubcategoryKeys.All, Result("gold", "Gold", 2, 0));
            return snapshot;
        }

        private static ScannerResult Result(string key, string label, int x, int y)
        {
            return new ScannerResult(key, label, new Vector2Int(x, y));
        }
    }
}
