using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class BattlefieldCellCostsTests
    {
        [TestMethod]
        public void StaticTravelCostWithoutManifestFallsBackToWaterAndTheCallersBlockerRule()
        {
            Assert.AreEqual(1f, Cost(water: 0, blocked: false), "plain ground costs one");
            Assert.IsTrue(float.IsPositiveInfinity(Cost(water: 1, blocked: false)), "water is impassable");
            Assert.IsTrue(float.IsPositiveInfinity(Cost(water: 0, blocked: true)), "a blocking decoration is impassable");
        }

        private static float Cost(int water, bool blocked)
        {
            return BattlefieldCellCosts.StaticTravelCost(
                null,
                theme: 1,
                terrainType: 2,
                customType: 0,
                decoration: blocked ? 4 : 0,
                standaloneDecoration: 0,
                effect: 0,
                water: water,
                bridge: 0,
                blockedWithoutManifest: blocked);
        }
    }
}
