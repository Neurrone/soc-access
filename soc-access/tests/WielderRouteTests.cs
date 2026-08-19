using System.Collections.Generic;
using Lavapotion.Pathfinding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
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

        [TestMethod]
        public void BuildTurnsCostsNothingWhenThereIsNoStepToCharge()
        {
            // The walk is shortened to a single node when the wielder stops
            // short of a tile it cannot stand on, leaving no step to charge.
            Assert.AreEqual(0, WielderRoute.BuildTurns(Path(0f), reachableCost: 5f, maxMovement: 12f).Count);
            Assert.AreEqual(0, WielderRoute.BuildTurns(null, reachableCost: 5f, maxMovement: 12f).Count);
        }

        [TestMethod]
        public void BuildStepsCountsARunOfStepsInTheSameDirectionAsOne()
        {
            IReadOnlyList<ScannerDirectionStep> steps = WielderRoute.BuildSteps(
                Route(Point(0, 0), Point(0, 1), Point(0, 2), Point(0, 3)));

            Assert.AreEqual(1, steps.Count);
            AssertStep(steps[0], 3, ScannerDirection.North);
        }

        [TestMethod]
        public void BuildStepsStartsANewRunWhenTheDirectionChanges()
        {
            IReadOnlyList<ScannerDirectionStep> steps = WielderRoute.BuildSteps(
                Route(Point(0, 0), Point(0, 1), Point(0, 2), Point(1, 2), Point(2, 2)));

            Assert.AreEqual(2, steps.Count);
            AssertStep(steps[0], 2, ScannerDirection.North);
            AssertStep(steps[1], 2, ScannerDirection.East);
        }

        [TestMethod]
        public void BuildStepsKeepsSingleStepsOfTheirOwn()
        {
            IReadOnlyList<ScannerDirectionStep> steps = WielderRoute.BuildSteps(
                Route(Point(0, 0), Point(0, 1), Point(1, 2), Point(2, 2)));

            Assert.AreEqual(3, steps.Count);
            AssertStep(steps[0], 1, ScannerDirection.North);
            AssertStep(steps[1], 1, ScannerDirection.Northeast);
            AssertStep(steps[2], 1, ScannerDirection.East);
        }

        [TestMethod]
        public void AddInteractionCostSpendsItOnTheTurnTheWielderArrives()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.AddInteractionCost(
                new List<WielderRouteTurn> { Turn(1, 5f) },
                interactionCost: 0.5f,
                movesLeft: 12f,
                maxMovement: 12f);

            Assert.AreEqual(1, turns.Count);
            AssertTurn(turns[0], 1, 5.5f);
        }

        [TestMethod]
        public void AddInteractionCostSlipsToTheNextTurnWhenArrivingSpendsTheLot()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.AddInteractionCost(
                new List<WielderRouteTurn> { Turn(1, 5f), Turn(2, 12f) },
                interactionCost: 0.5f,
                movesLeft: 5f,
                maxMovement: 12f);

            Assert.AreEqual(3, turns.Count);
            AssertTurn(turns[1], 2, 12f);
            AssertTurn(turns[2], 3, 0.5f);
        }

        [TestMethod]
        public void AddInteractionCostStandsOnItsOwnWhenThereIsNothingToWalk()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.AddInteractionCost(
                new List<WielderRouteTurn>(),
                interactionCost: 0.5f,
                movesLeft: 3f,
                maxMovement: 12f);

            Assert.AreEqual(1, turns.Count);
            AssertTurn(turns[0], 1, 0.5f);
        }

        [TestMethod]
        public void AddInteractionCostWaitsForNextTurnWhenTheWielderIsSpentAlready()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.AddInteractionCost(
                new List<WielderRouteTurn>(),
                interactionCost: 0.5f,
                movesLeft: 0f,
                maxMovement: 12f);

            Assert.AreEqual(1, turns.Count);
            AssertTurn(turns[0], 2, 0.5f);
        }

        [TestMethod]
        public void AddInteractionCostLeavesTheTurnsAloneWhenTheInteractionIsFree()
        {
            IReadOnlyList<WielderRouteTurn> turns = WielderRoute.AddInteractionCost(
                new List<WielderRouteTurn> { Turn(1, 5f) },
                interactionCost: 0f,
                movesLeft: 12f,
                maxMovement: 12f);

            Assert.AreEqual(1, turns.Count);
            AssertTurn(turns[0], 1, 5f);
        }

        private static void AssertTurn(WielderRouteTurn turn, int expectedTravelTurns, float expectedCost)
        {
            Assert.AreEqual(expectedTravelTurns, turn.TravelTurns);
            Assert.AreEqual(expectedCost, turn.Cost, 0.001f);
        }

        private static void AssertStep(ScannerDirectionStep step, int expectedCount, ScannerDirection expectedDirection)
        {
            Assert.AreEqual(expectedCount, step.Count);
            Assert.AreEqual(expectedDirection, step.Direction);
        }

        private static WielderRouteTurn Turn(int travelTurns, float cost)
        {
            return new WielderRouteTurn(travelTurns, cost);
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

        private static int2 Point(int x, int y)
        {
            return new int2(x, y);
        }

        private static PathNode[] Route(params int2[] points)
        {
            PathNode[] nodes = new PathNode[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                nodes[i] = new PathNode
                {
                    point = points[i],
                    travelCost = i
                };
            }

            return nodes;
        }
    }
}
