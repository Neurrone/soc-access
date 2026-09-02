using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class RoadConnectionsTests
    {
        [TestMethod]
        public void StraightRoadCarriesOnBothWaysAlongItself()
        {
            AssertDirections(
                Compute(new Vector2Int(10, 10), HorizontalRoad(10, 0, 20)),
                ScannerDirection.East,
                ScannerDirection.West);
        }

        [TestMethod]
        public void DiagonalRoadCarriesOnAlongTheDiagonal()
        {
            AssertDirections(
                Compute(new Vector2Int(10, 10), tile => tile.x == tile.y && tile.x >= 0 && tile.x <= 20),
                ScannerDirection.Northeast,
                ScannerDirection.Southwest);
        }

        [TestMethod]
        public void BendNamesTheTwoTilesItJoins()
        {
            // Runs west along y=10 up to x=10, then turns north.
            AssertDirections(
                Compute(
                    new Vector2Int(10, 10),
                    tile => (tile.y == 10 && tile.x >= 0 && tile.x <= 10)
                        || (tile.x == 10 && tile.y >= 10 && tile.y <= 20)),
                ScannerDirection.North,
                ScannerDirection.West);
        }

        [TestMethod]
        public void JunctionNamesEveryTileItJoins()
        {
            AssertDirections(
                Compute(
                    new Vector2Int(10, 10),
                    tile => (tile.y == 10 && tile.x >= 0 && tile.x <= 20)
                        || (tile.x == 10 && tile.y >= 10 && tile.y <= 20)),
                ScannerDirection.North,
                ScannerDirection.East,
                ScannerDirection.West);
        }

        [TestMethod]
        public void CrossroadsNamesAllFourWays()
        {
            AssertDirections(
                Compute(
                    new Vector2Int(10, 10),
                    tile => (tile.y == 10 && tile.x >= 0 && tile.x <= 20)
                        || (tile.x == 10 && tile.y >= 0 && tile.y <= 20)),
                ScannerDirection.North,
                ScannerDirection.East,
                ScannerDirection.South,
                ScannerDirection.West);
        }

        [TestMethod]
        public void DeadEndCarriesOnOnlyBackTheWayItCame()
        {
            AssertDirections(
                Compute(new Vector2Int(10, 10), HorizontalRoad(10, 0, 10)),
                ScannerDirection.West);
        }

        [TestMethod]
        public void IsolatedRoadTileCarriesOnNowhere()
        {
            AssertDirections(Compute(new Vector2Int(10, 10), tile => tile == new Vector2Int(10, 10)));
        }

        [TestMethod]
        public void OriginIsTakenOnTrustSoOnlyTheNeighboursAreAsked()
        {
            // Whether the tile you are standing on is road is the adapter's call, made from the
            // same terrain it names the tile by. Compute is only ever handed a road, so it asks
            // about the eight around it and nothing else.
            AssertDirections(
                Compute(new Vector2Int(10, 11), HorizontalRoad(10, 0, 20)),
                ScannerDirection.Southwest,
                ScannerDirection.South,
                ScannerDirection.Southeast);
        }

        [TestMethod]
        public void WideRoadNamesEveryTileItJoinsSoACurveStaysFollowable()
        {
            // A road painted two tiles wide genuinely joins five neighbours here. Naming all of
            // them is the point: each one is a move that keeps you on the road.
            AssertDirections(
                Compute(new Vector2Int(10, 10), WideRoad(9, 10, 0, 20)),
                ScannerDirection.East,
                ScannerDirection.Southeast,
                ScannerDirection.South,
                ScannerDirection.Southwest,
                ScannerDirection.West);
        }

        [TestMethod]
        public void TileSurroundedByRoadNamesAllEightWays()
        {
            AssertDirections(
                Compute(new Vector2Int(10, 10), WideRoad(9, 11, 0, 20)),
                ScannerDirection.North,
                ScannerDirection.Northeast,
                ScannerDirection.East,
                ScannerDirection.Southeast,
                ScannerDirection.South,
                ScannerDirection.Southwest,
                ScannerDirection.West,
                ScannerDirection.Northwest);
        }

        [TestMethod]
        public void BranchOffAWideRoadIsNamedAlongsideTheRoadItself()
        {
            AssertDirections(
                Compute(
                    new Vector2Int(10, 10),
                    tile => WideRoad(9, 10, 0, 20)(tile) || (tile.x == 10 && tile.y >= 11 && tile.y <= 18)),
                ScannerDirection.North,
                ScannerDirection.East,
                ScannerDirection.Southeast,
                ScannerDirection.South,
                ScannerDirection.Southwest,
                ScannerDirection.West);
        }

        [TestMethod]
        public void UnexploredGroundStopsTheRoadRatherThanGuessingPastIt()
        {
            // Fog is accepted as a dead end. This tile still joins the road to its west, it just
            // says nothing about the ground east of what we have seen.
            AssertDirections(
                Compute(
                    new Vector2Int(11, 10),
                    tile => HorizontalRoad(10, 0, 20)(tile) && tile.x <= 11),
                ScannerDirection.West);
        }

        [TestMethod]
        public void DirectionsAreNamedInCompassOrder()
        {
            IReadOnlyList<ScannerDirection> directions = Compute(
                new Vector2Int(10, 10),
                WideRoad(9, 10, 0, 20));

            CollectionAssert.AreEqual(
                new[]
                {
                    ScannerDirection.East,
                    ScannerDirection.Southeast,
                    ScannerDirection.South,
                    ScannerDirection.Southwest,
                    ScannerDirection.West
                },
                new List<ScannerDirection>(directions));
        }

        [TestMethod]
        public void TileWorksOutRoadDirectionsOnlyOnceAndOnlyWhenAsked()
        {
            int calls = 0;
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(10, 10));
            tile.SetRoadDirectionsSource(() =>
            {
                calls++;
                return new[] { ScannerDirection.East };
            });

            Assert.AreEqual(0, calls, "road directions should not be worked out until something reads them");

            IReadOnlyList<ScannerDirection> first = tile.RoadDirections;
            IReadOnlyList<ScannerDirection> second = tile.RoadDirections;

            Assert.AreEqual(1, calls, "road directions should be worked out once and remembered");
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void TileWithoutARoadDirectionSourceCarriesOnNowhere()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(10, 10));

            Assert.IsNotNull(tile.RoadDirections);
            Assert.AreEqual(0, tile.RoadDirections.Count);
        }

        private static IReadOnlyList<ScannerDirection> Compute(Vector2Int origin, Func<Vector2Int, bool> isRoad)
        {
            return RoadConnections.Compute(origin, isRoad);
        }

        private static Func<Vector2Int, bool> HorizontalRoad(int y, int minX, int maxX)
        {
            return tile => tile.y == y && tile.x >= minX && tile.x <= maxX;
        }

        private static Func<Vector2Int, bool> WideRoad(int minY, int maxY, int minX, int maxX)
        {
            return tile => tile.y >= minY && tile.y <= maxY && tile.x >= minX && tile.x <= maxX;
        }

        private static void AssertDirections(
            IReadOnlyList<ScannerDirection> actual,
            params ScannerDirection[] expected)
        {
            List<ScannerDirection> sortedActual = new List<ScannerDirection>(actual);
            sortedActual.Sort();
            List<ScannerDirection> sortedExpected = new List<ScannerDirection>(expected);
            sortedExpected.Sort();
            CollectionAssert.AreEqual(
                sortedExpected,
                sortedActual,
                "expected [" + string.Join(", ", sortedExpected) + "] but got [" + string.Join(", ", sortedActual) + "]");
        }
    }
}
