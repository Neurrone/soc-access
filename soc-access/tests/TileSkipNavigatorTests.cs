using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class TileSkipNavigatorTests
    {
        [TestMethod]
        public void FindTargetStopsAtFirstDifferentTile()
        {
            TileSkipResult result = TileSkipNavigator.FindTarget(
                new Vector2Int(0, 0),
                point => new Vector2Int(point.x + 1, point.y),
                point => point.x >= 0 && point.x <= 5,
                point => point.x < 4 ? "grass" : "road");

            Assert.AreEqual(new Vector2Int(4, 0), result.Target);
            Assert.AreEqual(3, result.SkippedCount);
        }

        [TestMethod]
        public void FindTargetUsesLastValidTileWhenNoDifferentTileExists()
        {
            TileSkipResult result = TileSkipNavigator.FindTarget(
                new Vector2Int(0, 0),
                point => new Vector2Int(point.x + 1, point.y),
                point => point.x >= 0 && point.x <= 4,
                point => "grass");

            Assert.AreEqual(new Vector2Int(4, 0), result.Target);
            Assert.AreEqual(3, result.SkippedCount);
        }

        [TestMethod]
        public void FindTargetTreatsAdjacentDifferentTileAsNormalMove()
        {
            TileSkipResult result = TileSkipNavigator.FindTarget(
                new Vector2Int(0, 0),
                point => new Vector2Int(point.x + 1, point.y),
                point => point.x >= 0 && point.x <= 4,
                point => point.x == 0 ? "grass" : "road");

            Assert.AreEqual(new Vector2Int(1, 0), result.Target);
            Assert.AreEqual(0, result.SkippedCount);
        }

        [TestMethod]
        public void AdventureSignatureTreatsVisibilityAsInteresting()
        {
            AdventureMapTile visible = AdventureTile(visible: true, explored: true);
            AdventureMapTile fog = AdventureTile(visible: false, explored: true);

            Assert.AreNotEqual(
                AdventureTileSkipSignature.FromTile(visible, hasBookmark: false, stopsAtRoadForks: false),
                AdventureTileSkipSignature.FromTile(fog, hasBookmark: false, stopsAtRoadForks: false));
        }

        [TestMethod]
        public void AdventureSignatureTreatsBookmarkPresenceAsInteresting()
        {
            AdventureMapTile tile = AdventureTile(visible: true, explored: true);

            Assert.AreNotEqual(
                AdventureTileSkipSignature.FromTile(tile, hasBookmark: false, stopsAtRoadForks: false),
                AdventureTileSkipSignature.FromTile(tile, hasBookmark: true, stopsAtRoadForks: false));
        }

        [TestMethod]
        public void AdventureSignatureIgnoresTerrainWhenSameMapEntityOccupiesBothTiles()
        {
            AdventureMapTile first = AdventureTile(visible: true, explored: true);
            first.Terrain = AdventureTerrainKind.Grass;
            first.MapEntityId = 7;
            AdventureMapTile second = AdventureTile(visible: true, explored: true);
            second.Terrain = AdventureTerrainKind.Road;
            second.MapEntityId = 7;

            Assert.AreEqual(
                AdventureTileSkipSignature.FromTile(first, hasBookmark: false, stopsAtRoadForks: false),
                AdventureTileSkipSignature.FromTile(second, hasBookmark: false, stopsAtRoadForks: false));
        }

        [TestMethod]
        public void CombatSignatureTreatsSameDangerousEffectIdAsSameTile()
        {
            CombatTile first = new CombatTile(new Vector2Int(0, 0));
            first.DangerousMapEffectEntityIds.Add(42);
            CombatTile second = new CombatTile(new Vector2Int(1, 0));
            second.DangerousMapEffectEntityIds.Add(42);

            Assert.AreEqual(
                CombatTileSkipSignature.FromTile(first),
                CombatTileSkipSignature.FromTile(second));
        }

        [TestMethod]
        public void CombatSignatureTreatsDifferentDangerousEffectIdAsInteresting()
        {
            CombatTile first = new CombatTile(new Vector2Int(0, 0));
            first.DangerousMapEffectEntityIds.Add(42);
            CombatTile second = new CombatTile(new Vector2Int(1, 0));
            second.DangerousMapEffectEntityIds.Add(43);

            Assert.AreNotEqual(
                CombatTileSkipSignature.FromTile(first),
                CombatTileSkipSignature.FromTile(second));
        }

        [TestMethod]
        public void TroopPlacementSignatureTreatsSpawnPointsAsInteresting()
        {
            TroopPlacementTile empty = new TroopPlacementTile(new Vector2Int(0, 0));
            TroopPlacementTile spawn = new TroopPlacementTile(new Vector2Int(1, 0))
            {
                SpawnSide = BattleSide.Left_Attacker
            };

            Assert.AreNotEqual(
                TroopPlacementTileSkipSignature.FromTile(empty),
                TroopPlacementTileSkipSignature.FromTile(spawn));
        }

        [TestMethod]
        public void TroopPlacementSignatureTreatsSameSpawnSideAsSameTile()
        {
            TroopPlacementTile first = new TroopPlacementTile(new Vector2Int(0, 0))
            {
                SpawnSide = BattleSide.Left_Attacker,
                SpawnPointId = 1,
                GlobalSpawnPointId = 1
            };
            TroopPlacementTile second = new TroopPlacementTile(new Vector2Int(1, 0))
            {
                SpawnSide = BattleSide.Left_Attacker,
                SpawnPointId = 2,
                GlobalSpawnPointId = 2
            };

            Assert.AreEqual(
                TroopPlacementTileSkipSignature.FromTile(first),
                TroopPlacementTileSkipSignature.FromTile(second));
        }

        [TestMethod]
        public void SkippingAlongARoadStopsOnAFork()
        {
            TileSkipResult result = TileSkipNavigator.FindTarget(
                new Vector2Int(0, 0),
                point => new Vector2Int(point.x + 1, point.y),
                point => point.x >= 0 && point.x <= 5,
                point => AdventureTileSkipSignature.FromTile(RoadTile(point), hasBookmark: false, stopsAtRoadForks: true));

            Assert.AreEqual(new Vector2Int(3, 0), result.Target);
            Assert.AreEqual(2, result.SkippedCount);
        }

        [TestMethod]
        public void SkippingAlongARoadRunsPastAForkWhenRoadDirectionsAreOff()
        {
            TileSkipResult result = TileSkipNavigator.FindTarget(
                new Vector2Int(0, 0),
                point => new Vector2Int(point.x + 1, point.y),
                point => point.x >= 0 && point.x <= 5,
                point => AdventureTileSkipSignature.FromTile(RoadTile(point), hasBookmark: false, stopsAtRoadForks: false));

            Assert.AreEqual(new Vector2Int(5, 0), result.Target);
        }

        /// <summary>
        /// A road running east to west with a branch leaving it at x = 3, which is the only
        /// tile that carries road in three directions.
        /// </summary>
        private static AdventureMapTile RoadTile(Vector2Int point)
        {
            AdventureMapTile tile = new AdventureMapTile(point)
            {
                IsVisible = true,
                IsExplored = true,
                Terrain = AdventureTerrainKind.DirtRoad
            };
            tile.SetRoadDirectionsSource(() => point.x == 3
                ? new[] { ScannerDirection.North, ScannerDirection.East, ScannerDirection.West }
                : new[] { ScannerDirection.East, ScannerDirection.West });
            return tile;
        }

        private static AdventureMapTile AdventureTile(bool visible, bool explored)
        {
            return new AdventureMapTile(new Vector2Int(0, 0))
            {
                IsVisible = visible,
                IsExplored = explored,
                Terrain = AdventureTerrainKind.Grass
            };
        }
    }
}
