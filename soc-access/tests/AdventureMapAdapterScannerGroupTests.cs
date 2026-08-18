using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// How a result covering many tiles survives the map changing underneath
    /// it. The tile the group speaks through is re-picked against the cursor,
    /// so the tile that stops qualifying first is usually the one the group was
    /// just announced through, and losing it must not lose the group.
    /// </summary>
    [TestClass]
    public sealed class AdventureMapAdapterScannerGroupTests
    {
        [TestMethod]
        public void GroupSpeaksThroughTheNearestTileThatStillQualifies()
        {
            ScannerResult result = Group(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));
            Vector2Int position;

            bool valid = AdventureMapAdapter.TryPickSurvivingScannerGroupPoint(
                result,
                Vector2Int.zero,
                Only(new Vector2Int(2, 0), new Vector2Int(3, 0)),
                out position);

            Assert.IsTrue(valid);
            Assert.AreEqual(new Vector2Int(2, 0), position);
        }

        [TestMethod]
        public void TilesProvedGoneLeaveTheGroupAndTheRestStay()
        {
            ScannerResult result = Group(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));
            Vector2Int position;

            AdventureMapAdapter.TryPickSurvivingScannerGroupPoint(
                result,
                Vector2Int.zero,
                Only(new Vector2Int(3, 0)),
                out position);

            CollectionAssert.AreEqual(new[] { new Vector2Int(3, 0) }, result.Points);
        }

        /// <summary>
        /// Judging a tile costs a pathfind, so the walk stops at the survivor.
        /// The tiles beyond it were never asked about and stay in the group.
        /// </summary>
        [TestMethod]
        public void TilesBeyondTheSurvivorAreLeftAlone()
        {
            ScannerResult result = Group(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));
            Vector2Int position;

            AdventureMapAdapter.TryPickSurvivingScannerGroupPoint(
                result,
                Vector2Int.zero,
                Only(new Vector2Int(2, 0)),
                out position);

            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 0), new Vector2Int(3, 0) }, result.Points);
        }

        [TestMethod]
        public void GroupIsOnlyGoneWhenNoTileQualifies()
        {
            ScannerResult result = Group(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));
            Vector2Int position;

            bool valid = AdventureMapAdapter.TryPickSurvivingScannerGroupPoint(
                result,
                Vector2Int.zero,
                Only(),
                out position);

            Assert.IsFalse(valid);
        }

        /// <summary>
        /// The cursor is what the representative is measured from, so the same
        /// group answers through a different tile from somewhere else.
        /// </summary>
        [TestMethod]
        public void TheSurvivorIsMeasuredFromTheCursor()
        {
            ScannerResult result = Group(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(9, 0));
            Vector2Int position;

            AdventureMapAdapter.TryPickSurvivingScannerGroupPoint(
                result,
                new Vector2Int(10, 0),
                Only(new Vector2Int(1, 0), new Vector2Int(9, 0)),
                out position);

            Assert.AreEqual(new Vector2Int(9, 0), position);
        }

        private static ScannerResult Group(params Vector2Int[] points)
        {
            ScannerResult result = new ScannerResult("unexplored:1", "Unexplored", points[0])
            {
                Kind = ScannerResultKind.UnexploredGroup
            };
            result.Points.AddRange(points);
            return result;
        }

        private static Func<Vector2Int, bool> Only(params Vector2Int[] valid)
        {
            HashSet<Vector2Int> qualifying = new HashSet<Vector2Int>(valid);
            return point => qualifying.Contains(point);
        }
    }
}
