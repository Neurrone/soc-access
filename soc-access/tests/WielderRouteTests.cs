using System.Collections.Generic;
using Lavapotion.Pathfinding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using Unity.Mathematics;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class WielderRouteTests
    {
        [TestMethod]
        public void BuildTurnsChargesTheWholeRouteToThisTurnWhenItIsAllReachable()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.BuildTurns(
                Path(0f, 2f, 5f),
                reachableCost: 5f,
                maxMovement: 12f);

            Assert.AreEqual(1, turns.Count);
            AssertTurn(turns[0], 1, 5f);
        }

        [TestMethod]
        public void BuildTurnsSplitsTheCostAtEachTurnBoundary()
        {
            // 5 movement left of a 12 point allowance, so the wielder spends its
            // remaining 5 now, a full 12 next turn, then 3 to arrive.
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.BuildTurns(
                Path(0f, 5f, 17f, 20f),
                reachableCost: 5f,
                maxMovement: 12f);

            Assert.AreEqual(3, turns.Count);
            AssertTurn(turns[0], 1, 5f);
            AssertTurn(turns[1], 2, 12f);
            AssertTurn(turns[2], 3, 3f);
        }

        [TestMethod]
        public void BuildTurnsSkipsThisTurnWhenTheWielderCannotMoveYet()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.BuildTurns(
                Path(0f, 6f, 12f, 18f),
                reachableCost: 0f,
                maxMovement: 12f);

            Assert.AreEqual(2, turns.Count);
            AssertTurn(turns[0], 2, 12f);
            AssertTurn(turns[1], 3, 6f);
        }

        private static void AssertTurn(WielderRouteTurn turn, int expectedTravelTurns, float expectedCost)
        {
            Assert.AreEqual(expectedTravelTurns, turn.TravelTurns);
            Assert.AreEqual(expectedCost, turn.Cost, 0.001f);
        }

        private static PathNode[] Path(params float[] travelCosts)
        {
            PathNode[] nodes = new PathNode[travelCosts.Length];
            for (int i = 0; i < travelCosts.Length; i++)
            {
                nodes[i] = new PathNode
                {
                    point = new int2(i, 0),
                    travelCost = travelCosts[i]
                };
            }

            return nodes;
        }
    }
}
