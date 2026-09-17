using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;
using static SongsOfConquestAccess.Tests.ScannerFixtures;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The "Sort scanner results by" setting: which distance a scope's results come in,
    /// and what the readout says about a result no route reaches, which is the same in both
    /// orders.</summary>
    [TestClass]
    public sealed class ScannerResultOrderTests : ModSettingsFixture
    {
        [TestInitialize]
        public void BindTheConfig()
        {
            BindTemporaryConfig();
        }

        [TestMethod]
        public void TheOrderIsTheStraightLineByDefault()
        {
            Assert.AreEqual(ScannerResultOrders.StraightLine, ModSettings.ScannerResultOrder);
            Assert.IsFalse(ModSettings.ScannerSortsByWalkablePath);
        }

        [TestMethod]
        public void TheResultOrderRoundTrips()
        {
            ModSettings.SetScannerResultOrder(ScannerResultOrders.WalkablePath);
            Assert.AreEqual(ScannerResultOrders.WalkablePath, ModSettings.ScannerResultOrder);

            ModSettings.SetScannerResultOrder(ScannerResultOrders.StraightLine);
            Assert.AreEqual(ScannerResultOrders.StraightLine, ModSettings.ScannerResultOrder);
        }

        /// <summary>The directions are the straight line whichever order the results are in, so a
        /// player who changes the setting hears the same bearing to the same result.</summary>
        [TestMethod]
        public void TheDirectionsAreTheStraightLineInBothOrders()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetCost("pickup:gold", 12f);

            Assert.AreEqual("3e", Read(paths, ScannerResultOrders.StraightLine));
            Assert.AreEqual("3e", Read(paths, ScannerResultOrders.WalkablePath));
        }

        [TestMethod]
        public void AResultAnArmyBlocksNamesTheArmyInBothOrders()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetBlockedBy("pickup:gold", "A stand of Roots troops");

            Assert.AreEqual("blocked by A stand of Roots troops, 3e", Read(paths, ScannerResultOrders.StraightLine));
            Assert.AreEqual("blocked by A stand of Roots troops, 3e", Read(paths, ScannerResultOrders.WalkablePath));
        }

        /// <summary>A result blocked by something the player cannot see is said to be blocked and
        /// nothing is named for it, in both orders.</summary>
        [TestMethod]
        public void AResultBlockedByNoVisibleArmyIsSaidWithoutANameInBothOrders()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetBlocked("pickup:gold");

            Assert.AreEqual("blocked, 3e", Read(paths, ScannerResultOrders.StraightLine));
            Assert.AreEqual("blocked, 3e", Read(paths, ScannerResultOrders.WalkablePath));
        }

        /// <summary>Terrain alone has nothing to name, so the readout is the plain directions.
        /// </summary>
        [TestMethod]
        public void AResultTerrainAloneRefusesReadsTheDirectionsAlone()
        {
            Assert.AreEqual("3e", Read(new StubPathSource(), ScannerResultOrders.WalkablePath));
        }

        /// <summary>The walkable-path order ranks a blocked result by what the walk to it would
        /// cost with nothing in the way, so it sits among the results it is as near as. Only ground
        /// no walk crosses at all goes to the back, by the straight line.</summary>
        [TestMethod]
        public void TheWalkablePathOrdersByWalkCostAndPutsABlockedResultWhereItsWalkPutsIt()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetCost("pickup:far", 9f);
            paths.SetTerrainCost("pickup:blocked", 2f);
            paths.SetBlockedBy("pickup:blocked", "A stand of Roots troops");
            ModSettings.SetScannerResultOrder(ScannerResultOrders.WalkablePath);
            ScannerController controller = Controller(
                Snapshot(
                    Entry("Pickups", "All", "Far", 1, 0, "pickup:far"),
                    Entry("Pickups", "All", "Blocked", 9, 0, "pickup:blocked"),
                    Entry("Pickups", "All", "Island", 2, 0, "pickup:island")),
                paths);

            Assert.AreEqual("Blocked", controller.ExecuteInitialLanding().Result.Label);
            Assert.AreEqual("Far", controller.ExecuteMoveItem(1).Result.Label);
            Assert.AreEqual("Island", controller.ExecuteMoveItem(1).Result.Label);
        }

        [TestMethod]
        public void TheStraightLineOrderIgnoresWhatTheWalkCosts()
        {
            StubPathSource paths = new StubPathSource();
            paths.SetCost("pickup:far", 1f);
            paths.SetCost("pickup:near", 99f);
            ScannerController controller = Controller(
                Snapshot(
                    Entry("Pickups", "All", "Far", 9, 0, "pickup:far"),
                    Entry("Pickups", "All", "Near", 1, 0, "pickup:near")),
                paths);

            Assert.AreEqual("Near", controller.ExecuteInitialLanding().Result.Label);
            Assert.AreEqual("Far", controller.ExecuteMoveItem(1).Result.Label);
        }

        private string Read(StubPathSource paths, string order)
        {
            ModSettings.SetScannerResultOrder(order);
            ScannerController controller = Controller(
                Snapshot(Entry("Pickups", "All", "Gold", 3, 0, "pickup:gold")),
                paths);

            return controller.ExecuteInitialLanding().FormatDistanceAndDirection(useLongDirections: false);
        }

        /// <summary>A pathfinder that answers only what a test told it: a result it was given no
        /// cost for is one nothing reaches today, a result it was given no terrain cost for is one
        /// no walk crosses at all, and a result is blocked only where a test said so.</summary>
        private sealed class StubPathSource : IScannerPathSource
        {
            private readonly Dictionary<string, float> _costs = new Dictionary<string, float>();
            private readonly Dictionary<string, float> _terrainCosts = new Dictionary<string, float>();
            private readonly Dictionary<string, string> _blocked = new Dictionary<string, string>();

            public void SetCost(string key, float cost)
            {
                _costs[key] = cost;
            }

            public void SetTerrainCost(string key, float cost)
            {
                _terrainCosts[key] = cost;
            }

            public void SetBlockedBy(string key, string armyName)
            {
                _blocked[key] = armyName;
            }

            public void SetBlocked(string key)
            {
                _blocked[key] = null;
            }

            public bool TryGetPathBlocker(Vector2Int origin, ScannerResult result, out string armyName)
            {
                return _blocked.TryGetValue(result.Key, out armyName);
            }

            public float GetPathCost(Vector2Int origin, ScannerResult result)
            {
                float cost;
                return _costs.TryGetValue(result.Key, out cost) ? cost : float.PositiveInfinity;
            }

            public float GetTerrainPathCost(Vector2Int origin, ScannerResult result)
            {
                float cost;
                return _terrainCosts.TryGetValue(result.Key, out cost) ? cost : float.PositiveInfinity;
            }
        }
    }
}
