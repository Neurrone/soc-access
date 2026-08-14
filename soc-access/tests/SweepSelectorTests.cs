using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class SweepSelectorTests
    {
        private static readonly Vector2Int Origin = new Vector2Int(10, 10);

        [TestMethod]
        public void NullSnapshotProducesNoPings()
        {
            Assert.AreEqual(0, SweepSelector.ForLookAround(null, null).Count);
            Assert.AreEqual(0, SweepSelector.ForScannerResult(null, null).Count);
        }

        [TestMethod]
        public void EachEntityCategoryMapsToItsVoice()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Add(source, ModStrings.Scanner.Wielders, "commander:1", new Vector2Int(9, 10), AdventureEntityCategory.Wielder);
            Add(source, ModStrings.Scanner.SettlementsAndBuildSites, "entity:2", new Vector2Int(10, 11), AdventureEntityCategory.Settlement);
            Add(source, ModStrings.Scanner.ResourceGenerators, "entity:3", new Vector2Int(10, 9), AdventureEntityCategory.ResourceDeposit);
            Add(source, ModStrings.Scanner.Pickups, "entity:4", new Vector2Int(8, 10), AdventureEntityCategory.Pickup);

            Dictionary<string, string> byPosition = CueKeysByPosition(Select(source));

            Assert.AreEqual(CueLibrary.SweepWielder, byPosition["9,10"]);
            Assert.AreEqual(CueLibrary.SweepSettlement, byPosition["10,11"]);
            Assert.AreEqual(CueLibrary.SweepResource, byPosition["10,9"]);
            Assert.AreEqual(CueLibrary.SweepPickup, byPosition["8,10"]);
        }

        [TestMethod]
        public void UncategorizedResultsAreSkipped()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Add(source, ModStrings.Scanner.Obstacles, "entity:1", new Vector2Int(9, 10), AdventureEntityCategory.None);
            Add(source, ModStrings.Scanner.Objectives, "entity:2", new Vector2Int(11, 10), AdventureEntityCategory.None);
            Add(source, ModStrings.Scanner.Teleport, "entity:3", new Vector2Int(10, 11), AdventureEntityCategory.None);

            Assert.AreEqual(0, Select(source).Count);
        }

        [TestMethod]
        public void OneTileListedUnderTwoCategoriesPingsOnce()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Add(source, ModStrings.Scanner.SettlementsAndBuildSites, "entity:2", new Vector2Int(11, 10), AdventureEntityCategory.Settlement);
            Add(source, ModStrings.Scanner.TroopSources, "entity:2", new Vector2Int(11, 10), AdventureEntityCategory.Settlement);
            Add(source, ModStrings.Scanner.Buildings, "entity:2", new Vector2Int(11, 10), AdventureEntityCategory.Settlement);

            IReadOnlyList<SweepEntry> entries = Select(source);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(CueLibrary.SweepSettlement, entries[0].Cues[0].Key);
        }

        [TestMethod]
        public void ResultsOutsideTheRadiusAreExcluded()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Add(source, ModStrings.Scanner.Wielders, "commander:1", new Vector2Int(13, 10), AdventureEntityCategory.Wielder);
            Add(source, ModStrings.Scanner.Wielders, "commander:2", new Vector2Int(20, 10), AdventureEntityCategory.Wielder);

            IReadOnlyList<SweepEntry> entries = SweepSelector.ForLookAround(
                ScannerLookAround.Build(source, Origin, 5),
                null);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(new Vector2Int(13, 10), entries[0].Position);
        }

        [TestMethod]
        public void AffiliationIsStackedAfterTheCategoryVoice()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Vector2Int position = new Vector2Int(9, 10);
            Add(source, ModStrings.Scanner.Wielders, "commander:1", position, AdventureEntityCategory.Wielder);

            Dictionary<Vector2Int, AdventureMapTile> tiles = new Dictionary<Vector2Int, AdventureMapTile>();
            tiles[position] = CommanderTile(position, ModStrings.Spatial.Enemy);

            IReadOnlyList<SweepEntry> entries = Select(source, point => Lookup(tiles, point));

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(2, entries[0].Cues.Count);
            Assert.AreEqual(CueLibrary.SweepWielder, entries[0].Cues[0].Key);
            Assert.AreEqual(CueLibrary.EntityEnemy, entries[0].Cues[1].Key);
            Assert.IsFalse(entries[0].Cues[0].FollowsPrevious);
            Assert.IsTrue(entries[0].Cues[1].FollowsPrevious);
        }

        [TestMethod]
        public void FriendlyEntitiesAreMarkedAndNeutralOnesAreNot()
        {
            ScannerSnapshot source = new ScannerSnapshot();
            Vector2Int friendly = new Vector2Int(9, 10);
            Vector2Int neutral = new Vector2Int(11, 10);
            Add(source, ModStrings.Scanner.SettlementsAndBuildSites, "entity:1", friendly, AdventureEntityCategory.Settlement);
            Add(source, ModStrings.Scanner.ResourceGenerators, "entity:2", neutral, AdventureEntityCategory.ResourceDeposit);

            Dictionary<Vector2Int, AdventureMapTile> tiles = new Dictionary<Vector2Int, AdventureMapTile>();
            tiles[friendly] = MapEntityTile(friendly, ModStrings.Spatial.Friendly);
            tiles[neutral] = MapEntityTile(neutral, ModStrings.Spatial.Neutral);

            Dictionary<string, string[]> stacks = new Dictionary<string, string[]>();
            IReadOnlyList<SweepEntry> entries = Select(source, point => Lookup(tiles, point));
            for (int i = 0; i < entries.Count; i++)
            {
                stacks[entries[i].Position.x + "," + entries[i].Position.y] = Keys(entries[i].Cues);
            }

            CollectionAssert.AreEqual(new[] { CueLibrary.SweepSettlement, CueLibrary.EntityFriendly }, stacks["9,10"]);
            CollectionAssert.AreEqual(new[] { CueLibrary.SweepResource }, stacks["11,10"]);
        }

        [TestMethod]
        public void TheResultCategoryWinsOverWhateverSitsOnItsTile()
        {
            // A multi-tile settlement can put its result on a tile that carries no entity.
            Vector2Int position = new Vector2Int(9, 10);
            ScannerResult result = new ScannerResult("entity:1", "Trove", position)
            {
                EntityCategory = AdventureEntityCategory.Pickup
            };

            Dictionary<Vector2Int, AdventureMapTile> tiles = new Dictionary<Vector2Int, AdventureMapTile>();
            tiles[position] = new AdventureMapTile(position) { IsExplored = true, Terrain = AdventureTerrainKind.Grass };

            CollectionAssert.AreEqual(
                new[] { CueLibrary.SweepPickup },
                Keys(SweepSelector.ForScannerResult(result, point => Lookup(tiles, point))));
        }

        [TestMethod]
        public void TerrainGroupResultsHaveNoGestureOfTheirOwn()
        {
            ScannerResult result = new ScannerResult("grass:1", "12 grass tiles", new Vector2Int(9, 10))
            {
                Kind = ScannerResultKind.TerrainGroup
            };

            Assert.AreEqual(0, SweepSelector.ForScannerResult(result, null).Count);
        }

        private static IReadOnlyList<SweepEntry> Select(ScannerSnapshot source)
        {
            return Select(source, null);
        }

        private static IReadOnlyList<SweepEntry> Select(
            ScannerSnapshot source,
            Func<Vector2Int, AdventureMapTile> tileLookup)
        {
            return SweepSelector.ForLookAround(ScannerLookAround.Build(source, Origin, 15), tileLookup);
        }

        private static void Add(
            ScannerSnapshot snapshot,
            ModString category,
            string key,
            Vector2Int position,
            AdventureEntityCategory entityCategory)
        {
            snapshot.Add(
                ModText.Get(category),
                ModText.Get(ModStrings.Scanner.All),
                new ScannerResult(key, key, position) { EntityCategory = entityCategory });
        }

        private static AdventureMapTile CommanderTile(Vector2Int position, ModString relationship)
        {
            return new AdventureMapTile(position)
            {
                IsExplored = true,
                EntityCategory = AdventureEntityCategory.Wielder,
                Commander = new AdventureMapTile.CommanderInfo
                {
                    Relationship = ModText.Get(relationship),
                    IsOwnedByLocalTeam = false
                }
            };
        }

        private static AdventureMapTile MapEntityTile(Vector2Int position, ModString relationship)
        {
            return new AdventureMapTile(position)
            {
                IsExplored = true,
                MapEntityId = 1,
                MapEntityRelationship = ModText.Get(relationship)
            };
        }

        private static AdventureMapTile Lookup(Dictionary<Vector2Int, AdventureMapTile> tiles, Vector2Int point)
        {
            AdventureMapTile tile;
            return tiles.TryGetValue(point, out tile) ? tile : null;
        }

        private static string[] Keys(IReadOnlyList<TileCue> cues)
        {
            string[] keys = new string[cues.Count];
            for (int i = 0; i < cues.Count; i++)
            {
                keys[i] = cues[i].Key;
            }

            return keys;
        }

        private static Dictionary<string, string> CueKeysByPosition(IReadOnlyList<SweepEntry> entries)
        {
            Dictionary<string, string> keys = new Dictionary<string, string>();
            for (int i = 0; i < entries.Count; i++)
            {
                keys[entries[i].Position.x + "," + entries[i].Position.y] = entries[i].Cues[0].Key;
            }

            return keys;
        }
    }
}
