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
            AdventureMapTile tile = TileFixtures.Tile(12, 9, AdventureTerrainKind.Wall);
            tile.IsImpassable = true;

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Wall, impassable, 12, 9.", text);
        }

        [TestMethod]
        public void DescribeTileReadsDirtRoadAsTheOnlyTerrain()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsGroundTerrainWithoutEnvironmentLayer()
        {
            AdventureMapTile tile = TileFixtures.Tile(7, 4, AdventureTerrainKind.Grass);

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Grass, 7, 4.", text);
        }

        [TestMethod]
        public void DescribeTileDoesNotReadMovementCostByDefault()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.ReachableMovementCost = 3f;

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsEnabledMovementCostAtEnd()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.ReachableMovementCost = 3f;

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2, Movement cost: 3.", text);
        }

        [TestMethod]
        public void DescribeTileKeepsTheDecimalsOfAMovementCost()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.ReachableMovementCost = 15.5f;

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2, Movement cost: 15.5.", text);
        }

        [TestMethod]
        public void DescribeTileReadsACostOfLessThanHalfAPointRatherThanCallingItFree()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.ReachableMovementCost = 0.4f;

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable, 4, 2, Movement cost: 0.4.", text);
        }

        [TestMethod]
        public void FormatMovementNumberKeepsALargeCostOutOfExponentialForm()
        {
            Assert.AreEqual("123.45", AdventureMapTileSpeechFormatter.FormatMovementNumber(123.45f));
            Assert.AreEqual("3.75", AdventureMapTileSpeechFormatter.FormatMovementNumber(3.75f));
            Assert.AreEqual("4", AdventureMapTileSpeechFormatter.FormatMovementNumber(4f));
            Assert.AreEqual("0.4", AdventureMapTileSpeechFormatter.FormatMovementNumber(0.4f));
        }

        [TestMethod]
        public void DescribeTileDoesNotReadMovementCostForUnexploredTile()
        {
            AdventureMapTile tile = TileFixtures.Bare(4, 2);
            tile.ReachableMovementCost = 3f;

            string text = CreateFormatter(enableMovementCost: true).DescribeTile(tile);

            Assert.AreEqual("Unexplored, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsTheWaysARoadLeadsOnAfterTheTerrain()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.SetRoadDirectionsSource(() => new[] { ScannerDirection.East, ScannerDirection.West });

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, e w, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsAForkAsThreeDirectionsWithoutNamingTheShape()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.CobblestoneRoad);
            tile.SetRoadDirectionsSource(() => new[]
            {
                ScannerDirection.North,
                ScannerDirection.East,
                ScannerDirection.Southwest
            });

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Cobblestone road, n e sw, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileSpellsOutRoadDirectionsWhenLongDirectionsAreOn()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.SetRoadDirectionsSource(() => new[] { ScannerDirection.East, ScannerDirection.West });

            string text = new AdventureMapTileSpeechFormatter(
                GetDefaultOrder,
                (group, element) => element.Key != AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    && element.DefaultEnabled,
                (group, element) => element.DefaultSuffix,
                () => true).DescribeTile(tile);

            Assert.AreEqual("Dirt road, east west, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileLeavesTerrainAloneWhenThereAreNoRoadDirections()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.Grass);
            tile.IsReachable = true;
            tile.SetRoadDirectionsSource(() => new ScannerDirection[0]);

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Grass, reachable, 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileSaysNothingAboutRoadDirectionsWhenTheyAreTurnedOff()
        {
            int calls = 0;
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.SetRoadDirectionsSource(() =>
            {
                calls++;
                return new[] { ScannerDirection.East, ScannerDirection.West };
            });

            string text = new AdventureMapTileSpeechFormatter(
                GetDefaultOrder,
                (group, element) => element.Key != AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    && element.Key != AdventureMapAnnouncementDefinitions.TileKeys.RoadDirections
                    && element.DefaultEnabled,
                (group, element) => element.DefaultSuffix).DescribeTile(tile);

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

        [TestMethod]
        public void DescribeTileReadsNeitherTerrainNorRoadDirectionsUnderASettlement()
        {
            int calls = 0;
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.DirtRoad);
            tile.IsReachable = true;
            tile.EntityCategory = AdventureEntityCategory.Settlement;
            tile.SetRoadDirectionsSource(() =>
            {
                calls++;
                return new[] { ScannerDirection.East, ScannerDirection.West };
            });

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("reachable, 4, 2.", text);
            Assert.AreEqual(0, calls, "a settlement tile should not cost the work of finding road directions");
        }

        [TestMethod]
        public void DescribeTileStillReadsTheTerrainUnderAWielder()
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.Grass);
            tile.EntityCategory = AdventureEntityCategory.Wielder;
            tile.Commander = new AdventureMapTile.CommanderInfo { Name = "Cecilia Stoutheart" };

            string text = CreateFormatter().DescribeTile(tile);

            Assert.AreEqual("Cecilia Stoutheart, Grass, 4, 2.", text);
        }

        private static AdventureMapTile CreateRouteTile(AdventureMapTile.PathIndicatorInfo indicator)
        {
            AdventureMapTile tile = TileFixtures.Tile(4, 2, AdventureTerrainKind.Grass);
            tile.IsReachable = true;
            tile.PathIndicator = indicator;
            return tile;
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
