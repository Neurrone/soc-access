using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;
using static SongsOfConquestAccess.Tests.ScannerFixtures;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The scanner's Distance setting: which of the two distances a result is read and
    /// ordered by, and what the readout says when the walkable path does not exist.</summary>
    [TestClass]
    public sealed class ScannerDistanceModeTests : ModSettingsFixture
    {
        [TestInitialize]
        public void BindTheConfig()
        {
            BindTemporaryConfig();
        }

        [TestMethod]
        public void TheDistanceIsTheStraightLineByDefault()
        {
            Assert.AreEqual(ScannerDistanceModes.StraightLine, ModSettings.ScannerDistanceMode);
            Assert.IsFalse(ModSettings.ScannerUsesWalkablePath);
        }

        [TestMethod]
        public void TheDistanceModeRoundTrips()
        {
            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.WalkablePath);
            Assert.AreEqual(ScannerDistanceModes.WalkablePath, ModSettings.ScannerDistanceMode);

            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.StraightLine);
            Assert.AreEqual(ScannerDistanceModes.StraightLine, ModSettings.ScannerDistanceMode);
        }

        [TestMethod]
        public void TheStraightLineNeverAsksThePathSource()
        {
            StubPathSource paths = new StubPathSource();
            ScannerController controller = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                paths);

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual("3e", result.FormatDistanceAndDirection(useLongDirections: false));
            Assert.AreEqual(0, paths.DirectionCalls);
        }

        [TestMethod]
        public void TheWalkablePathReadsTheRouteTheGameAnswers()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetPath(
                "pickup:gold",
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1),
                new Vector2Int(3, 0));
            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.WalkablePath);
            ScannerController controller = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                paths);

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual("1n, 3e, 1s", result.FormatDistanceAndDirection(useLongDirections: false));
        }

        [TestMethod]
        public void AResultWithNoWalkablePathIsReadAsTheStraightLineAndSaysSo()
        {
            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.WalkablePath);
            ScannerController controller = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                new StubPathSource());

            ScannerCommandResult result = controller.ExecuteInitialLanding();

            Assert.AreEqual("straight line, 3e", result.FormatDistanceAndDirection(useLongDirections: false));
        }

        /// <summary>Terrain alone has nothing to name, so only the readout for a route something
        /// stands in says what that something is.</summary>
        [TestMethod]
        public void AStraightLineFallbackNamesWhatBlocksTheRouteWhenTheGameNamesOne()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetBlocker("pickup:gold", "A stand of Roots troops");
            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.WalkablePath);
            ScannerController blocked = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                paths);
            ScannerController impassable = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                new StubPathSource());

            Assert.AreEqual(
                "blocked by A stand of Roots troops, straight line, 3e",
                blocked.ExecuteInitialLanding().FormatDistanceAndDirection(useLongDirections: false));
            Assert.AreEqual(
                "straight line, 3e",
                impassable.ExecuteInitialLanding().FormatDistanceAndDirection(useLongDirections: false));
        }

        [TestMethod]
        public void TheWalkablePathOrdersResultsByWhatTheyCostToWalkTo()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetCost("pickup:far", 2f);
            paths.SetCost("pickup:near", 9f);
            ModSettings.SetScannerDistanceMode(ScannerDistanceModes.WalkablePath);
            ScannerController controller = Controller(
                Snapshot(
                    Entry("Pickups", "All", "Far", 9, 0, "pickup:far"),
                    Entry("Pickups", "All", "Near", 1, 0, "pickup:near"),
                    Entry("Pickups", "All", "Island", 2, 0, "pickup:island")),
                paths);

            Assert.AreEqual("Far", controller.ExecuteInitialLanding().Result.Label);
            Assert.AreEqual("Near", controller.ExecuteMoveItem(1).Result.Label);
            Assert.AreEqual("Island", controller.ExecuteMoveItem(1).Result.Label);
        }

        /// <summary>A pathfinder that answers only what a test told it: any result it was given no
        /// route for has none, and any result it was given no cost for is out of reach.</summary>
        private sealed class StubPathSource : IScannerPathSource
        {
            private readonly Dictionary<string, IReadOnlyList<ScannerDirectionStep>> _paths =
                new Dictionary<string, IReadOnlyList<ScannerDirectionStep>>();
            private readonly Dictionary<string, float> _costs = new Dictionary<string, float>();
            private readonly Dictionary<string, string> _blockers = new Dictionary<string, string>();

            public int DirectionCalls { get; private set; }

            public void SetPath(string key, params Vector2Int[] tiles)
            {
                _paths[key] = ScannerDirectionUtility.BuildPathDirections(tiles);
            }

            public void SetCost(string key, float cost)
            {
                _costs[key] = cost;
            }

            public void SetBlocker(string key, string name)
            {
                _blockers[key] = name;
            }

            public IReadOnlyList<ScannerDirectionStep> TryGetPathDirections(Vector2Int origin, ScannerResult result)
            {
                DirectionCalls++;
                IReadOnlyList<ScannerDirectionStep> path;
                return _paths.TryGetValue(result.Key, out path) ? path : null;
            }

            public string TryGetPathBlockerName(Vector2Int origin, ScannerResult result)
            {
                string blocker;
                return _blockers.TryGetValue(result.Key, out blocker) ? blocker : null;
            }

            public float GetPathCost(Vector2Int origin, ScannerResult result)
            {
                float cost;
                return _costs.TryGetValue(result.Key, out cost) ? cost : float.PositiveInfinity;
            }
        }
    }
}
