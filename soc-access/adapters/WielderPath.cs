using System;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The path the game draws as a wielder's route preview: the tiles walked, minus
    /// a final tile the wielder cannot stand on, the furthest node it reaches this
    /// turn, and the movement it has per turn. The route preview and the destination
    /// announcement both build from this, so their turn boundaries come from one place.
    /// </summary>
    internal sealed class WielderPath
    {
        private WielderPath(PathNode[] nodes, PathNode reachablePoint, int reachableIndex, float maxMovement)
        {
            Nodes = nodes;
            ReachablePoint = reachablePoint;
            ReachableIndex = reachableIndex;
            MaxMovement = maxMovement;
        }

        /// <summary>
        /// The nodes walked, starting at the wielder's own tile. A single node means
        /// the wielder is already standing next to a destination it cannot stand on,
        /// so there is nothing left to walk.
        /// </summary>
        public PathNode[] Nodes { get; private set; }

        /// <summary>The furthest node along the path the wielder reaches this turn.</summary>
        public PathNode ReachablePoint { get; private set; }

        /// <summary>Index of <see cref="ReachablePoint"/> in <see cref="Nodes"/>.</summary>
        public int ReachableIndex { get; private set; }

        /// <summary>Movement the wielder has on a full turn, never zero.</summary>
        public float MaxMovement { get; private set; }

        public static bool TryBuild(IClientAdventureFacade facade, ICommanderState commander, int localTeamId, out WielderPath path)
        {
            path = null;
            if (facade == null
                || facade.Level == null
                || facade.Teams == null
                || commander == null
                || commander.Destination == null
                || !commander.Destination.HasDestination
                || localTeamId < 0)
            {
                return false;
            }

            Vector2Int destination = commander.Destination.Destination;
            if (destination == commander.Position
                || !facade.Level.IsPointWithinMapForTravel(destination)
                || !facade.Level.GetIsPointExplored(commander.TeamId, destination)
                || facade.Teams.IsNotInCurrentTurn(localTeamId))
            {
                return false;
            }

            PathNode[] nodes;
            PathNode reachablePoint;
            int reachableIndex;
            try
            {
                nodes = facade.Level.PointsInPath(localTeamId, commander.Position, destination, (PathfinderCacheType)0);
                if (!PathfinderExtensions.GetIsValid(nodes) || nodes.Length == 0)
                {
                    return false;
                }

                // The wielder stops short of a tile it cannot stand on, such as
                // a building it walks up to and interacts with. Standing next to
                // that tile already leaves nothing to walk.
                if (!facade.Level.IsValidMoveDestination(localTeamId, ToVector2Int(nodes[nodes.Length - 1])))
                {
                    Array.Resize(ref nodes, nodes.Length - 1);
                }

                reachablePoint = facade.Level.GetClosestReachablePoint(
                    localTeamId,
                    commander.Position,
                    destination,
                    commander.MovesLeft);
                reachableIndex = FindNodeIndex(nodes, reachablePoint);
                if (reachableIndex < 0)
                {
                    reachableIndex = 0;
                    reachablePoint = nodes[0];
                }
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("WielderPath could not path to the destination: " + exception.Message);
                return false;
            }

            float maxMovement = commander.Stats != null && commander.Stats.Movement != null
                ? commander.Stats.Movement.GetValue()
                : 0f;
            if (maxMovement <= 0f)
            {
                maxMovement = 1f;
            }

            path = new WielderPath(nodes, reachablePoint, reachableIndex, maxMovement);
            return true;
        }

        /// <summary>
        /// Ordinal of the turn a node is reached on, counting the current turn as 1,
        /// matching the game's preview markers.
        /// </summary>
        public int GetTravelTurns(float nodeCost)
        {
            return GetTravelTurns(nodeCost, ReachablePoint.travelCost, MaxMovement);
        }

        internal static int GetTravelTurns(float nodeCost, float reachableCost, float maxMovement)
        {
            if (nodeCost <= reachableCost + 0.001f)
            {
                return 1;
            }

            return 1 + Mathf.CeilToInt((nodeCost - reachableCost) / maxMovement);
        }

        private static int FindNodeIndex(PathNode[] path, PathNode node)
        {
            if (path == null)
            {
                return -1;
            }

            for (int i = 0; i < path.Length; i++)
            {
                if (SameNode(path[i], node))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool SameNode(PathNode left, PathNode right)
        {
            return left.point.x == right.point.x
                && left.point.y == right.point.y
                && Mathf.Abs(left.travelCost - right.travelCost) < 0.001f;
        }

        private static Vector2Int ToVector2Int(PathNode node)
        {
            return new Vector2Int(node.point.x, node.point.y);
        }
    }
}
