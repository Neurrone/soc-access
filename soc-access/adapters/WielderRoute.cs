using System;
using System.Collections.Generic;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The route a wielder will walk to reach its destination, as the steps it
    /// takes and the movement it spends on each turn along the way. This is the
    /// same path the game draws as a route preview, so the turn ordinals here
    /// agree with the ones on the preview markers.
    /// </summary>
    internal sealed class WielderRoute
    {
        internal WielderRoute(IReadOnlyList<ScannerDirectionStep> steps, IReadOnlyList<WielderRouteTurn> turns)
        {
            Steps = steps;
            Turns = turns;
        }

        /// <summary>
        /// The tiles stepped, in the order they are walked, with a run of steps in the
        /// same direction counted as one.
        /// </summary>
        public IReadOnlyList<ScannerDirectionStep> Steps { get; private set; }

        /// <summary>Movement spent per turn, in order, skipping turns that cost nothing.</summary>
        public IReadOnlyList<WielderRouteTurn> Turns { get; private set; }

        public static bool TryBuild(IClientAdventureFacade facade, ICommanderState commander, out WielderRoute route)
        {
            route = null;
            if (facade == null
                || facade.Level == null
                || facade.Teams == null
                || commander == null
                || commander.Destination == null
                || !commander.Destination.HasDestination)
            {
                return false;
            }

            int localTeamId = facade.Teams.LocalTeamInControlId;
            Vector2Int destination = commander.Destination.Destination;
            if (localTeamId < 0 || destination == commander.Position)
            {
                return false;
            }

            PathNode[] path;
            PathNode reachablePoint;
            try
            {
                path = facade.Level.PointsInPath(localTeamId, commander.Position, destination);
                if (!PathfinderExtensions.GetIsValid(path) || path.Length < 2)
                {
                    return false;
                }

                // The wielder stops short of a tile it cannot stand on, such as
                // a building it walks up to and interacts with.
                if (!facade.Level.IsValidMoveDestination(localTeamId, ToVector2Int(path[path.Length - 1])))
                {
                    Array.Resize(ref path, path.Length - 1);
                    if (path.Length < 2)
                    {
                        return false;
                    }
                }

                reachablePoint = facade.Level.GetClosestReachablePoint(
                    localTeamId,
                    commander.Position,
                    destination,
                    commander.MovesLeft);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("WielderRoute could not path to the destination: " + exception.Message);
                return false;
            }

            float maxMovement = commander.Stats != null && commander.Stats.Movement != null
                ? commander.Stats.Movement.GetValue()
                : 0f;
            if (maxMovement <= 0f)
            {
                maxMovement = 1f;
            }

            route = new WielderRoute(
                BuildSteps(path),
                BuildTurns(path, reachablePoint.travelCost, maxMovement));
            return route.Steps.Count != 0;
        }

        internal static IReadOnlyList<ScannerDirectionStep> BuildSteps(PathNode[] path)
        {
            List<ScannerDirectionStep> steps = new List<ScannerDirectionStep>();
            for (int i = 1; i < path.Length; i++)
            {
                ScannerDirection direction;
                if (!ScannerDirectionUtility.TryGetStepDirection(
                    ToVector2Int(path[i]) - ToVector2Int(path[i - 1]),
                    out direction))
                {
                    continue;
                }

                ScannerDirectionStep previous = steps.Count != 0 ? steps[steps.Count - 1] : null;
                if (previous != null && previous.Direction == direction)
                {
                    steps[steps.Count - 1] = new ScannerDirectionStep(previous.Count + 1, direction);
                }
                else
                {
                    steps.Add(new ScannerDirectionStep(1, direction));
                }
            }

            return steps;
        }

        internal static IReadOnlyList<WielderRouteTurn> BuildTurns(PathNode[] path, float reachableCost, float maxMovement)
        {
            List<WielderRouteTurn> turns = new List<WielderRouteTurn>();
            float spentBeforeThisTurn = 0f;
            int currentTurn = GetTravelTurns(path[1].travelCost, reachableCost, maxMovement);
            for (int i = 2; i < path.Length; i++)
            {
                int travelTurns = GetTravelTurns(path[i].travelCost, reachableCost, maxMovement);
                if (travelTurns == currentTurn)
                {
                    continue;
                }

                AddTurn(turns, currentTurn, path[i - 1].travelCost - spentBeforeThisTurn);
                spentBeforeThisTurn = path[i - 1].travelCost;
                currentTurn = travelTurns;
            }

            AddTurn(turns, currentTurn, path[path.Length - 1].travelCost - spentBeforeThisTurn);
            return turns;
        }

        private static void AddTurn(List<WielderRouteTurn> turns, int travelTurns, float cost)
        {
            if (cost > 0f)
            {
                turns.Add(new WielderRouteTurn(travelTurns, cost));
            }
        }

        // Matches the ordinal the game paints on its route preview markers,
        // counting the current turn as 1.
        private static int GetTravelTurns(float nodeCost, float reachableCost, float maxMovement)
        {
            if (nodeCost <= reachableCost + 0.001f)
            {
                return 1;
            }

            return 1 + Mathf.CeilToInt((nodeCost - reachableCost) / maxMovement);
        }

        private static Vector2Int ToVector2Int(PathNode node)
        {
            return new Vector2Int(node.point.x, node.point.y);
        }
    }

    internal struct WielderRouteTurn
    {
        public WielderRouteTurn(int travelTurns, float cost)
        {
            TravelTurns = travelTurns;
            Cost = cost;
        }

        /// <summary>Ordinal that counts the current turn as 1.</summary>
        public int TravelTurns;

        public float Cost;
    }
}
