using Lavapotion.Pathfinding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using Unity.Mathematics;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AdventureMapAdapterMovementCostTests
    {
        [TestMethod]
        public void TryGetReachableMovementCostReturnsMatchingFinitePathNodeCost()
        {
            PathNode[] nodes =
            {
                Node(1, 1, 0f),
                Node(2, 1, 1.5f),
                Node(3, 1, 3f)
            };

            bool found = AdventureMapAdapter.TryGetReachableMovementCost(nodes, new Vector2Int(3, 1), out float cost);

            Assert.IsTrue(found);
            Assert.AreEqual(3f, cost);
        }

        [TestMethod]
        public void TryGetReachableMovementCostIgnoresInfinitePathNodeCost()
        {
            PathNode[] nodes =
            {
                Node(1, 1, 0f),
                Node(2, 1, float.PositiveInfinity)
            };

            bool found = AdventureMapAdapter.TryGetReachableMovementCost(nodes, new Vector2Int(2, 1), out float cost);

            Assert.IsFalse(found);
            Assert.AreEqual(0f, cost);
        }

        [TestMethod]
        public void ApplyReachableMovementCostSetsInteractionCostWhenDirectCostIsMissing()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2));

            AdventureMapAdapter.ApplyReachableMovementCost(tile, 5f);

            Assert.IsTrue(tile.IsReachable);
            Assert.AreEqual(5f, tile.ReachableMovementCost.Value);
        }

        [TestMethod]
        public void ApplyReachableMovementCostPreservesDirectMovementCost()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsReachable = true,
                ReachableMovementCost = 2f
            };

            AdventureMapAdapter.ApplyReachableMovementCost(tile, 5f);

            Assert.IsTrue(tile.IsReachable);
            Assert.AreEqual(2f, tile.ReachableMovementCost.Value);
        }

        private static PathNode Node(int x, int y, float cost)
        {
            return new PathNode
            {
                point = new int2(x, y),
                travelCost = cost
            };
        }
    }
}
