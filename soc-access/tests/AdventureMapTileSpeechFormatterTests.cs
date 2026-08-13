using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AdventureMapTileSpeechFormatterTests
    {
        [TestMethod]
        public void DescribeTileReadsWallAsTerrainWithImpassableStatus()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(12, 9))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Wall,
                IsImpassable = true
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Wall, impassable, 12, 9.", text);
        }

        [TestMethod]
        public void DescribeTileReadsDirtRoadAsTheOnlyTerrain()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.DirtRoad,
                IsReachable = true
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsGroundTerrainWithoutEnvironmentLayer()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(7, 4))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Grass
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Grass, 7, 4.", text);
        }

        [TestMethod]
        public void DescribeTileDoesNotReadMovementCostByDefault()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                IsReachable = true,
                ReachableMovementCost = 3f,
                Terrain = AdventureTerrainKind.DirtRoad
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsEnabledMovementCostAtEnd()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                IsReachable = true,
                ReachableMovementCost = 3f,
                Terrain = AdventureTerrainKind.DirtRoad
            };

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2, Movement cost: 3.", text);
        }

        [TestMethod]
        public void DescribeTileDoesNotReadMovementCostForUnexploredTile()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = false,
                IsVisible = false,
                ReachableMovementCost = 3f
            };

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Unexplored, 4, 2.", text);
        }

        private static AdventureMapTileSpeechFormatter CreateFormatter()
        {
            return CreateFormatter(enableMovementCost: false);
        }

        private static AdventureMapTileSpeechFormatter CreateFormatter(bool enableMovementCost)
        {
            return new AdventureMapTileSpeechFormatter(
                GetDefaultOrder,
                (group, element) => element.Key == AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    ? enableMovementCost
                    : element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);
        }

        private static IReadOnlyList<string> GetDefaultOrder(AnnouncementGroupDefinition group)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < group.Elements.Count; i++)
            {
                keys.Add(group.Elements[i].Key);
            }

            return keys;
        }
    }
}
