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

        [TestMethod]
        public void DescribeTileReadsTheWaysARoadLeadsOnAfterTheTerrain()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.DirtRoad,
                IsReachable = true,
                RoadDirections = new[] { ScannerDirection.East, ScannerDirection.West }
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road e w, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsAForkAsThreeDirectionsWithoutNamingTheShape()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.CobblestoneRoad,
                IsReachable = true,
                RoadDirections = new[]
                {
                    ScannerDirection.North,
                    ScannerDirection.East,
                    ScannerDirection.Southwest
                }
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Cobblestone road n e sw, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileSpellsOutRoadDirectionsWhenLongDirectionsAreOn()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.DirtRoad,
                IsReachable = true,
                RoadDirections = new[] { ScannerDirection.East, ScannerDirection.West }
            };

            string text = new AdventureMapTileSpeechFormatter(
                GetDefaultOrder,
                (group, element) => element.Key != AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    && element.DefaultEnabled,
                (group, element) => element.DefaultSuffix,
                () => true,
                () => true).DescribeTile(tile);

            Assert.AreEqual("Dirt road east west, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileLeavesTerrainAloneWhenThereAreNoRoadDirections()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Grass,
                IsReachable = true,
                RoadDirections = new ScannerDirection[0]
            };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Grass, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileSaysNothingAboutRoadDirectionsWhenTheyAreTurnedOff()
        {
            int calls = 0;
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.DirtRoad,
                IsReachable = true
            };
            tile.SetRoadDirectionsSource(() =>
            {
                calls++;
                return new[] { ScannerDirection.East, ScannerDirection.West };
            });

            string text = new AdventureMapTileSpeechFormatter(
                GetDefaultOrder,
                (group, element) => element.Key != AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    && element.DefaultEnabled,
                (group, element) => element.DefaultSuffix,
                () => false,
                () => false).DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2.", text);
            Assert.AreEqual(0, calls, "turning road directions off should not cost the work of finding them");
        }

        [TestMethod]
        public void DescribeTileReadsADestinationReachedNextTurnAsNextTurn()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.Destination,
                    TravelTurns = 2,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, Destination, next turn, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileCountsDestinationTurnsFromTheNextTurnOnward()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.Destination,
                    TravelTurns = 4,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, Destination, in 3 turns, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileSaysNothingAboutTurnsForADestinationReachedThisTurn()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.Destination,
                    TravelTurns = 1,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, Destination, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsTheFurthestReachableTileOfTheNextTurnAsNextTurn()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.OnRoute,
                    TravelTurns = 2,
                    FurthestReachableTurns = 2,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, On route, furthest reachable next turn, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileCountsFurthestReachableTurnsFromTheNextTurnOnward()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.OnRoute,
                    TravelTurns = 3,
                    FurthestReachableTurns = 3,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, On route, furthest reachable in 2 turns, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileKeepsTheFurthestReachableTileOfThisTurnAsThisTurn()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.OnRoute,
                    TravelTurns = 1,
                    FurthestReachableTurns = 1,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, On route, furthest reachable this turn, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsARouteTileReachedNextTurnAsNextTurn()
        {
            string text = CreateFormatter().DescribeTile(CreateRouteTile(
                new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.OnRoute,
                    TravelTurns = 2,
                    HasRoutePreview = true
                }));

            Assert.AreEqual("Grass, On route, next turn, 4, 2.", text);
        }

        private static AdventureMapTile CreateRouteTile(AdventureMapTile.PathIndicatorInfo indicator)
        {
            return new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Grass,
                IsReachable = true,
                PathIndicator = indicator
            };
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
