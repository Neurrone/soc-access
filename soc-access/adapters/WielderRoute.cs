using System;
using System.Collections.Generic;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The route a wielder will walk to reach its destination, as the steps it
    /// takes and the movement it spends on each turn along the way. It reads the
    /// walk from <see cref="WielderPath"/>, the same path the game draws as a route
    /// preview, so the turn ordinals here agree with the ones on the preview markers.
    /// </summary>
    internal sealed class WielderRoute
    {
        internal WielderRoute(
            IReadOnlyList<ScannerDirectionStep> steps,
            IReadOnlyList<WielderRouteTurn> turns,
            WielderRouteInteraction interaction = null)
        {
            Steps = steps;
            Turns = turns;
            Interaction = interaction;
        }

        /// <summary>
        /// The tiles stepped, in the order they are walked, with a run of steps in the
        /// same direction counted as one.
        /// </summary>
        public IReadOnlyList<ScannerDirectionStep> Steps { get; private set; }

        /// <summary>Movement spent per turn, in order, skipping turns that cost nothing.</summary>
        public IReadOnlyList<WielderRouteTurn> Turns { get; private set; }

        /// <summary>
        /// What the wielder does once it arrives, or null when the destination holds
        /// nothing it interacts with.
        /// </summary>
        public WielderRouteInteraction Interaction { get; private set; }

        public static bool TryBuild(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IFogManager fogManager,
            ICommanderState commander,
            out WielderRoute route)
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

            WielderPath path;
            if (!WielderPath.TryBuild(facade, commander, localTeamId, out path))
            {
                return false;
            }

            WielderRouteInteraction interaction = TryGetInteraction(
                facade,
                selectionHandler,
                localization,
                fogManager,
                commander,
                destination);

            IReadOnlyList<ScannerDirectionStep> steps = BuildSteps(path.Nodes);
            IReadOnlyList<WielderRouteTurn> turns = BuildTurns(path.Nodes, path.ReachablePoint.travelCost, path.MaxMovement);
            if (interaction != null)
            {
                turns = AddInteractionCost(turns, interaction.Cost, commander.MovesLeft, path.MaxMovement);
            }

            if (steps.Count == 0 && interaction == null)
            {
                return false;
            }

            route = new WielderRoute(steps, turns, interaction);
            return true;
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
            if (path == null || path.Length < 2)
            {
                return turns;
            }

            float spentBeforeThisTurn = 0f;
            int currentTurn = WielderPath.GetTravelTurns(path[1].travelCost, reachableCost, maxMovement);
            for (int i = 2; i < path.Length; i++)
            {
                int travelTurns = WielderPath.GetTravelTurns(path[i].travelCost, reachableCost, maxMovement);
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

        /// <summary>
        /// Folds what the interaction costs into the turn it is spent on. The game
        /// charges it from the movement left after arriving, so it lands on the turn
        /// the wielder arrives when there is enough left for it, and slips to the turn
        /// after when there is not.
        /// </summary>
        internal static IReadOnlyList<WielderRouteTurn> AddInteractionCost(
            IReadOnlyList<WielderRouteTurn> turns,
            float interactionCost,
            float movesLeft,
            float maxMovement)
        {
            List<WielderRouteTurn> result = new List<WielderRouteTurn>(turns);
            if (interactionCost <= 0f)
            {
                return result;
            }

            int arrivalTurn = result.Count != 0 ? result[result.Count - 1].TravelTurns : 1;
            float spentOnArrival = result.Count != 0 ? result[result.Count - 1].Cost : 0f;
            float allowance = arrivalTurn <= 1 ? movesLeft : maxMovement;
            if (interactionCost > allowance - spentOnArrival + 0.001f)
            {
                result.Add(new WielderRouteTurn(arrivalTurn + 1, interactionCost));
            }
            else if (result.Count != 0)
            {
                result[result.Count - 1] = new WielderRouteTurn(arrivalTurn, spentOnArrival + interactionCost);
            }
            else
            {
                result.Add(new WielderRouteTurn(arrivalTurn, interactionCost));
            }

            return result;
        }

        private static WielderRouteInteraction TryGetInteraction(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IFogManager fogManager,
            ICommanderState commander,
            Vector2Int destination)
        {
            try
            {
                WielderRouteInteraction wielderInteraction = TryGetWielderInteraction(
                    facade,
                    localization,
                    fogManager,
                    commander,
                    destination);
                if (wielderInteraction != null)
                {
                    return wielderInteraction;
                }

                if (facade.MapEntities == null || !facade.MapEntities.ExistsAt(destination))
                {
                    return null;
                }

                IMapEntity entity = facade.MapEntities.GetAt(destination);
                if (entity == null || !facade.MapEntities.CanTeamInteractWithMapEntity(commander.TeamId, entity))
                {
                    return null;
                }

                // The game's tooltip names an action only for something the wielder
                // has not visited yet. For a claimable that means not held by its
                // team already, so a wielder sent to its own mine is not told it
                // will claim it.
                if (entity.DidVisit(commander.Id))
                {
                    return null;
                }

                AdventureInteractionType interactionType;
                string action = AdventureMapEntityLabel.TryGetInteractionType(facade, commander.Id, entity, out interactionType)
                    ? GameText.Get(
                        localization,
                        "Adventure/TooltipInstruction/" + interactionType,
                        interactionType.ToString())
                    : string.Empty;
                string target = AdventureMapEntityLabel.GetMapEntityName(facade, selectionHandler, localization, entity);
                float cost = entity.GetInteractionCost(commander.Id);
                if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(target))
                {
                    return cost > 0f ? new WielderRouteInteraction(string.Empty, string.Empty, cost) : null;
                }

                return new WielderRouteInteraction(action, target, cost);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("WielderRoute could not read the destination interaction: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// A wielder standing on the destination is traded with or attacked rather than
        /// interacted with, so the game names the action through its own cursor rules
        /// instead of through pre-visit details.
        /// </summary>
        private static WielderRouteInteraction TryGetWielderInteraction(
            IClientAdventureFacade facade,
            ILocalizationHandler localization,
            IFogManager fogManager,
            ICommanderState commander,
            Vector2Int destination)
        {
            if (facade.Commanders == null)
            {
                return null;
            }

            ICommanderState other = facade.Commanders.GetAtPoint(commander.TeamId, destination);
            if (other == null || other.Id == commander.Id || !IsVisible(fogManager, other.Position))
            {
                return null;
            }

            // The game locks both actions out while the other side is in a battle,
            // so there is nothing to say the wielder will do.
            bool isPartner = facade.Teams.IsInPartnership(commander.TeamId, other.TeamId);
            if (isPartner
                ? facade.Commanders.IsCommanderInBattle(other.Id)
                : facade.Teams.IsTeamInBattle(other.TeamId))
            {
                return null;
            }

            string action = GameText.Get(
                localization,
                isPartner ? "Adventure/TooltipInstruction/Trade" : "Adventure/TooltipInstruction/Attack",
                isPartner ? "Trade" : "Attack");
            string target = facade.Commanders.GetName(other.Id);
            return string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(target)
                ? null
                : new WielderRouteInteraction(action, target, 0f);
        }

        private static bool IsVisible(IFogManager fogManager, Vector2Int position)
        {
            if (fogManager == null)
            {
                return false;
            }

            try
            {
                return fogManager.GetFog(position.x, position.y) == byte.MaxValue;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddTurn(List<WielderRouteTurn> turns, int travelTurns, float cost)
        {
            if (cost > 0f)
            {
                turns.Add(new WielderRouteTurn(travelTurns, cost));
            }
        }

        private static Vector2Int ToVector2Int(PathNode node)
        {
            return new Vector2Int(node.point.x, node.point.y);
        }
    }

    /// <summary>
    /// What a wielder does when it reaches its destination: the game's own name for the
    /// action, the name of the thing acted on, and the movement the interaction costs.
    /// </summary>
    internal sealed class WielderRouteInteraction
    {
        internal WielderRouteInteraction(string actionText, string targetName, float cost)
        {
            ActionText = actionText;
            TargetName = targetName;
            Cost = cost;
        }

        /// <summary>The game's own name for the action, such as claim, visit or pick up.</summary>
        public string ActionText { get; private set; }

        /// <summary>The localized name of the thing the wielder acts on.</summary>
        public string TargetName { get; private set; }

        /// <summary>Movement the interaction costs, on top of the walk to reach it.</summary>
        public float Cost { get; private set; }

        /// <summary>Whether the game named both the action and the thing acted on.</summary>
        public bool HasAction
        {
            get { return !string.IsNullOrWhiteSpace(ActionText) && !string.IsNullOrWhiteSpace(TargetName); }
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
