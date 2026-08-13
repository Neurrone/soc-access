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
            Assert.AreEqual("Berry", subcategory.Items[0].Label);
            Assert.AreEqual("Zinc", subcategory.Items[1].Label);
            Assert.AreEqual(new Vector2Int(0, 2), subcategory.Items[2].Instances[0].Position);
            Assert.AreEqual(new Vector2Int(2, 0), subcategory.Items[2].Instances[1].Position);
        }

        /// <summary>
        /// The whole-category sweep holds both sides, and the player asking for
        /// their own spawn points should not have to walk the enemy's to find
        /// them.
        /// </summary>
        [TestMethod]
        public void AddGivesEachSideOfTheSameThingItsOwnItem()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("SpawnPoints", "All", Sided("spawn:friendly:1", 1, 0, ScannerResultRelationship.Friendly));
            snapshot.Add("SpawnPoints", "All", Sided("spawn:enemy:1", 2, 0, ScannerResultRelationship.Enemy));
            snapshot.Add("SpawnPoints", "All", Sided("spawn:friendly:2", 3, 0, ScannerResultRelationship.Friendly));

            ScannerSubcategory subcategory = snapshot.Categories[0].Subcategories[0];
            Assert.AreEqual(2, subcategory.Items.Count);
            Assert.AreEqual(2, subcategory.Items[0].Instances.Count);
            Assert.AreEqual("spawn:friendly:1", subcategory.Items[0].Instances[0].Key);
            Assert.AreEqual("spawn:friendly:2", subcategory.Items[0].Instances[1].Key);
            Assert.AreEqual(1, subcategory.Items[1].Instances.Count);
            Assert.AreEqual("spawn:enemy:1", subcategory.Items[1].Instances[0].Key);
        }

        /// <summary>
        /// State parts the same way the side does: a looted chest and one still
        /// worth walking to are not copies of each other.
        /// </summary>
        [TestMethod]
        public void AddGivesEachStateOfTheSameThingItsOwnItem()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:chest-1", "Chest", new Vector2Int(1, 0)));
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:chest-2", "Chest", new Vector2Int(2, 0))
            {
                Unvisited = true
            });
            snapshot.Add("Pickups", "All", new ScannerResult("pickup:chest-3", "Chest", new Vector2Int(3, 0))
            {
                Unvisited = true,
                NotVisible = true
            });

            ScannerSubcategory subcategory = snapshot.Categories[0].Subcategories[0];
            Assert.AreEqual(3, subcategory.Items.Count);
            Assert.AreEqual("pickup:chest-1", subcategory.Items[0].Instances[0].Key);
            Assert.AreEqual("pickup:chest-2", subcategory.Items[1].Instances[0].Key);
            Assert.AreEqual("pickup:chest-3", subcategory.Items[2].Instances[0].Key);
        }

        [TestMethod]
        public void AddKeepsOneItemPerResultWhenTheSubcategoryIsFlat()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            ScannerSubcategory subcategory = snapshot.GetOrAddCategory("LookAround").GetOrAddSubcategory("All");
            subcategory.FlatItems = true;

            subcategory.Add(Sided("spawn:friendly:1", 1, 0, ScannerResultRelationship.Friendly));
            subcategory.Add(Sided("spawn:friendly:2", 3, 0, ScannerResultRelationship.Friendly));

            Assert.AreEqual(2, subcategory.Items.Count);
        }

        private static ScannerResult Sided(string key, int x, int y, ScannerResultRelationship relationship)
        {
            return new ScannerResult(key, "Spawn point", new Vector2Int(x, y))
            {
                Relationship = relationship
            };
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
        public void SortByDistancePreservesOrderForPreservedSubcategories()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            ScannerCategory exploration = snapshot.GetOrAddCategory("Exploration");
            ScannerSubcategory revealed = exploration.GetOrAddSubcategory("Revealed");
            revealed.PreserveResultOrder = true;
            revealed.Add(new ScannerResult("entity:far", "Far", new Vector2Int(10, 0)));
            revealed.Add(new ScannerResult("entity:near", "Near", new Vector2Int(1, 0)));

            snapshot.SortByDistance(Vector2Int.zero);

            Assert.AreEqual("Far", revealed.Items[0].Label);
            Assert.AreEqual("Near", revealed.Items[1].Label);
        }

        /// <summary>
        /// The flag is per subcategory, so a neighbour under the same category
        /// still sorts. This is what lets Unexplored and Revealed share one.
        /// </summary>
        [TestMethod]
        public void SortByDistanceStillSortsUnpreservedSiblings()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            ScannerCategory exploration = snapshot.GetOrAddCategory("Exploration");
            ScannerSubcategory revealed = exploration.GetOrAddSubcategory("Revealed");
            revealed.PreserveResultOrder = true;
            revealed.Add(new ScannerResult("entity:far", "Far", new Vector2Int(10, 0)));
            revealed.Add(new ScannerResult("entity:near", "Near", new Vector2Int(1, 0)));
            ScannerSubcategory unexplored = exploration.GetOrAddSubcategory("Unexplored");
            unexplored.Add(new ScannerResult("region:far", "Far region", new Vector2Int(10, 0)));
            unexplored.Add(new ScannerResult("region:near", "Near region", new Vector2Int(1, 0)));

            snapshot.SortByDistance(Vector2Int.zero);

            Assert.AreEqual("Far", revealed.Items[0].Label);
            Assert.AreEqual("Near region", unexplored.Items[0].Label);
        }

        [TestMethod]
        public void AdventureMapRevealedRegistryAppendsAndUpdatesByKey()
        {
            AdventureMapRevealedRegistry registry = new AdventureMapRevealedRegistry();

            registry.AddOrUpdate("entity:1", "Old Gold", new Vector2Int(1, 0), 1, AdventureMapRevealedKind.MapEntity);
            registry.AddOrUpdate("commander:2", "Wielder", new Vector2Int(2, 0), 2, AdventureMapRevealedKind.Wielder);
            registry.AddOrUpdate("entity:1", "Gold", new Vector2Int(3, 0), 1, AdventureMapRevealedKind.MapEntity);

            Assert.AreEqual(2, registry.Entries.Count);
            Assert.AreEqual("entity:1", registry.Entries[0].Key);
            Assert.AreEqual("Gold", registry.Entries[0].Label);
            Assert.AreEqual(new Vector2Int(3, 0), registry.Entries[0].Position);
            Assert.AreEqual(0, registry.Entries[0].Sequence);
            Assert.AreEqual("commander:2", registry.Entries[1].Key);
            Assert.AreEqual(1, registry.Entries[1].Sequence);
        }

        [TestMethod]
        public void AdventureMapRevealedRegistryRemovesEntries()
        {
            AdventureMapRevealedRegistry registry = new AdventureMapRevealedRegistry();
            registry.AddOrUpdate("entity:1", "Gold", new Vector2Int(1, 0), 1, AdventureMapRevealedKind.MapEntity);
            registry.AddOrUpdate("commander:2", "Wielder", new Vector2Int(2, 0), 2, AdventureMapRevealedKind.Wielder);

            Assert.IsTrue(registry.Remove("entity:1"));

            Assert.AreEqual(1, registry.Entries.Count);
            Assert.AreEqual("commander:2", registry.Entries[0].Key);
        }

        [TestMethod]
        public void AdventureMapScannerStateKeepsSameRevealedRegistry()
        {
            AdventureMapScannerState state = new AdventureMapScannerState();

            AdventureMapRevealedRegistry first = state.RevealedRegistry;
            AdventureMapRevealedRegistry second = state.RevealedRegistry;

            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void AdventureMapScannerStateClearEmptiesRevealedRegistryWithoutReplacingIt()
        {
            AdventureMapScannerState state = new AdventureMapScannerState();
            AdventureMapRevealedRegistry registry = state.RevealedRegistry;
            registry.AddOrUpdate("entity:1", "Gold", new Vector2Int(1, 0), 1, AdventureMapRevealedKind.MapEntity);

            state.Clear();

            Assert.AreSame(registry, state.RevealedRegistry);
            Assert.AreEqual(0, state.RevealedRegistry.Entries.Count);
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
            Assert.AreEqual("Pickups", snapshot.Categories[0].Key);
            Assert.AreEqual(1, snapshot.Categories[0].Subcategories.Count);
            Assert.AreEqual("All", snapshot.Categories[0].Subcategories[0].Key);
        }

        [TestMethod]
        public void AddUsesAllSubcategoryWhenSubcategoryIsBlank()
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();

            snapshot.Add("Terrain", "", new ScannerResult("terrain:water", "Water", new Vector2Int(2, 3)));

            Assert.AreEqual("Terrain", snapshot.Categories[0].Key);
            Assert.AreEqual(ScannerSubcategoryKeys.All, snapshot.Categories[0].Subcategories[0].Key);
            Assert.AreEqual("Water", snapshot.Categories[0].Subcategories[0].Items[0].Instances[0].Label);
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
            Assert.AreEqual(0, location.ItemIndex);
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
            Assert.AreEqual(0, location.ItemIndex);
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
            Assert.AreEqual(-1, location.ItemIndex);
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
            Assert.AreEqual("All", snapshot.Categories[0].Subcategories[0].Key);
            Assert.IsFalse(snapshot.Categories[0].Subcategories[0].HasResults);
            Assert.AreEqual("Unvisited", snapshot.Categories[0].Subcategories[1].Key);
            Assert.AreEqual("Ore", snapshot.Categories[0].Subcategories[1].Items[0].Instances[0].Label);
        }
    }
}
