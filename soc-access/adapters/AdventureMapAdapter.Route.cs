using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE ROUTE PREVIEW: the markers the game draws along a selected wielder's planned path, read
    /// back as facts a tile can carry - on-route or destination, which turn a tile is reached on,
    /// the furthest tile reachable this turn, the cost marks, and whether the destination can be
    /// interacted with and whether that can happen this turn.
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        // The selected wielder's route preview for one frame, and the game-read values it is the
        // answer to. See BuildRoutePreviewInfo.
        private RoutePreviewInfo _routePreview;
        private int _routePreviewFrame = -1;
        private int _routePreviewCommanderId;
        private int _routePreviewTeamId;
        private Vector2Int _routePreviewOrigin;
        private Vector2Int _routePreviewDestination;
        private float _routePreviewMovesLeft;
        private bool _routePreviewSecondaryHeld;

        private AdventureMapTile.PathIndicatorInfo BuildPathIndicatorForTile(Vector2Int position, ICommanderState selectedCommander, int localTeamId)
        {
            if (selectedCommander == null
                || !selectedCommander.IsAlive
                || selectedCommander.Destination == null
                || !selectedCommander.Destination.HasDestination
                || _facade == null
                || _facade.Level == null
                || _facade.Teams == null)
            {
                return null;
            }

            Vector2Int destination = selectedCommander.Destination.Destination;
            bool isDestinationTile = position == destination;
            RoutePreviewInfo preview = BuildRoutePreviewInfo(selectedCommander, localTeamId);
            if (preview == null)
            {
                return isDestinationTile
                    ? new AdventureMapTile.PathIndicatorInfo
                    {
                        Kind = AdventureMapTile.PathIndicatorKind.Destination,
                        TravelTurns = 1,
                        HasRoutePreview = false
                    }
                    : null;
            }

            RouteTileInfo routeTile;
            if (preview.Tiles.TryGetValue(position, out routeTile))
            {
                return routeTile.ToPathIndicator();
            }

            if (isDestinationTile)
            {
                return new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = AdventureMapTile.PathIndicatorKind.Destination,
                    TravelTurns = preview.DestinationTravelTurns,
                    IsInteractable = preview.IsInteractableDestination,
                    CanInteractThisTurn = preview.CanInteractDestinationThisTurn,
                    HasRoutePreview = true
                };
            }

            return null;
        }

        /// <summary>
        /// The route preview for the selected wielder's planned path, kept for one frame.
        ///
        /// Building it runs the game's <c>PointsInPath</c> and <c>GetClosestReachablePoint</c> - two
        /// whole-path searches - and <c>GetTile</c> asks for it once per tile it builds, so a
        /// scanner snapshot or a skip-navigator sweep paid thousands of searches for one answer that
        /// cannot differ between them. The key is everything the build reads from the game: the
        /// commander's id, tile and movement left, the destination it is walking to, the team,
        /// whether the secondary button is held, and <c>Time.frameCount</c>, which closes the key
        /// because within one frame nothing the game owns has moved. Every part is read from the
        /// game on the call, so a step, a new destination, a selection change and a hot reload all
        /// miss on their own with no hook to tell them to (AGENTS.md, "Screen Resolution"). A miss
        /// is cached too, so a held button or an unwalkable destination costs one search and not one
        /// per tile.
        /// </summary>
        private RoutePreviewInfo BuildRoutePreviewInfo(ICommanderState selectedCommander, int localTeamId)
        {
            int frame = Time.frameCount;
            int commanderId = selectedCommander.Id;
            Vector2Int origin = selectedCommander.Position;
            Vector2Int destination = selectedCommander.Destination.Destination;
            float movesLeft = selectedCommander.MovesLeft;
            bool secondaryHeld = IsSecondaryInputHolding();
            if (_routePreviewFrame == frame
                && _routePreviewCommanderId == commanderId
                && _routePreviewTeamId == localTeamId
                && _routePreviewOrigin == origin
                && _routePreviewDestination == destination
                && _routePreviewMovesLeft == movesLeft
                && _routePreviewSecondaryHeld == secondaryHeld)
            {
                return _routePreview;
            }

            RoutePreviewInfo preview = secondaryHeld
                ? null
                : BuildRoutePreview(selectedCommander, localTeamId, destination);
            _routePreview = preview;
            _routePreviewFrame = frame;
            _routePreviewCommanderId = commanderId;
            _routePreviewTeamId = localTeamId;
            _routePreviewOrigin = origin;
            _routePreviewDestination = destination;
            _routePreviewMovesLeft = movesLeft;
            _routePreviewSecondaryHeld = secondaryHeld;
            return preview;
        }

        private RoutePreviewInfo BuildRoutePreview(ICommanderState selectedCommander, int localTeamId, Vector2Int destination)
        {
            WielderPath path;
            if (!WielderPath.TryBuild(_facade, selectedCommander, localTeamId, out path) || path.Nodes.Length < 2)
            {
                return null;
            }

            PathNode[] drawPath = path.Nodes;
            PathNode reachablePoint = path.ReachablePoint;

            RoutePreviewInfo preview = new RoutePreviewInfo
            {
                Destination = destination,
                ReachablePoint = reachablePoint,
                ReachableIndex = path.ReachableIndex,
                MaxMovement = path.MaxMovement,
                IsInteractableDestination = IsInteractableDestination(selectedCommander, destination, localTeamId)
            };
            preview.CanInteractDestinationThisTurn = preview.IsInteractableDestination
                && CanInteractDestinationThisTurn(selectedCommander, destination, localTeamId, reachablePoint);

            for (int i = 1; i < drawPath.Length; i++)
            {
                PathNode node = drawPath[i];
                Vector2Int point = WielderPath.ToVector2Int(node);
                RouteTileInfo tileInfo = preview.GetOrCreate(point);
                tileInfo.Kind = point == destination
                    ? AdventureMapTile.PathIndicatorKind.Destination
                    : AdventureMapTile.PathIndicatorKind.OnRoute;
                tileInfo.TravelTurns = path.GetTravelTurns(node.travelCost);
                tileInfo.HasRoutePreview = true;
                if (point == destination)
                {
                    tileInfo.IsInteractable = preview.IsInteractableDestination;
                    tileInfo.CanInteractThisTurn = preview.CanInteractDestinationThisTurn;
                }
            }

            RouteTileInfo destinationInfo = preview.GetOrCreate(destination);
            destinationInfo.Kind = AdventureMapTile.PathIndicatorKind.Destination;
            destinationInfo.TravelTurns = path.GetTravelTurns(drawPath[drawPath.Length - 1].travelCost);
            destinationInfo.IsInteractable = preview.IsInteractableDestination;
            destinationInfo.CanInteractThisTurn = preview.CanInteractDestinationThisTurn;
            destinationInfo.HasRoutePreview = true;
            preview.DestinationTravelTurns = destinationInfo.TravelTurns;

            AddReachableBoundaryMarkers(preview, drawPath);
            AddCostMarks(preview, drawPath, path.ReachableIndex);
            return preview;
        }

        private void AddReachableBoundaryMarkers(RoutePreviewInfo preview, PathNode[] drawPath)
        {
            if (preview == null || drawPath == null || drawPath.Length == 0)
            {
                return;
            }

            Vector2Int reachablePoint = WielderPath.ToVector2Int(preview.ReachablePoint);
            if (preview.ReachableIndex >= 0
                && preview.ReachableIndex < drawPath.Length - 1
                && reachablePoint != preview.Destination)
            {
                preview.GetOrCreate(reachablePoint).FurthestReachableTurns = 1;
            }

            List<PathNode> nonReachable = new List<PathNode>();
            for (int i = preview.ReachableIndex + 1; i < drawPath.Length; i++)
            {
                nonReachable.Add(drawPath[i]);
            }

            int number = 1;
            for (int i = 0; i < nonReachable.Count - 1; i++)
            {
                int nextNumber = Mathf.CeilToInt((nonReachable[i].travelCost - preview.ReachablePoint.travelCost) / preview.MaxMovement);
                if (i > 0 && nextNumber != number)
                {
                    PathNode markerNode = nonReachable[i - 1];
                    preview.GetOrCreate(WielderPath.ToVector2Int(markerNode)).FurthestReachableTurns = Math.Max(2, nextNumber);
                    number = nextNumber;
                }
            }

            if (nonReachable.Count > 1)
            {
                PathNode previousToFinal = nonReachable[nonReachable.Count - 2];
                float remainingInSegment = preview.MaxMovement - (previousToFinal.travelCost - preview.ReachablePoint.travelCost) % preview.MaxMovement;
                if (remainingInSegment < 0.5f)
                {
                    number++;
                    preview.GetOrCreate(WielderPath.ToVector2Int(previousToFinal)).FurthestReachableTurns = Math.Max(2, number);
                }
            }
        }

        private void AddCostMarks(RoutePreviewInfo preview, PathNode[] drawPath, int reachableIndex)
        {
            if (preview == null || drawPath == null || drawPath.Length < 2)
            {
                return;
            }

            AddCostMarksForSegment(preview, drawPath, 0, Math.Min(reachableIndex, drawPath.Length - 1));
            AddCostMarksForSegment(preview, drawPath, Math.Max(0, reachableIndex), drawPath.Length - 1);
        }

        private void AddCostMarksForSegment(RoutePreviewInfo preview, PathNode[] drawPath, int startIndex, int endIndex)
        {
            if (endIndex <= startIndex)
            {
                return;
            }

            int previousCost = Mathf.FloorToInt(drawPath[startIndex].travelCost);
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                int currentCost = Mathf.FloorToInt(drawPath[i].travelCost);
                if (currentCost > previousCost)
                {
                    preview.GetOrCreate(WielderPath.ToVector2Int(drawPath[i])).CostMark = currentCost;
                    previousCost = currentCost;
                }
            }
        }

        private bool IsInteractableDestination(ICommanderState selectedCommander, Vector2Int destination, int localTeamId)
        {
            if (selectedCommander == null)
            {
                return false;
            }

            ICommanderState commanderAtDestination = _facade.Commanders.GetAtPoint(localTeamId, destination);
            if (commanderAtDestination != null && GetFog(destination) == byte.MaxValue)
            {
                return true;
            }

            if (_facade.MapEntities == null || !_facade.MapEntities.ExistsAt(destination))
            {
                return false;
            }

            IMapEntity entity = _facade.MapEntities.GetAt(destination);
            ILocationComponent location;
            return entity != null
                && _facade.MapEntities.CanTeamInteractWithMapEntity(selectedCommander.TeamId, entity)
                && entity.TryGetComponent<ILocationComponent>(out location);
        }

        private bool CanInteractDestinationThisTurn(ICommanderState selectedCommander, Vector2Int destination, int localTeamId, PathNode reachablePoint)
        {
            if (selectedCommander == null)
            {
                return false;
            }

            ICommanderState commanderAtDestination = _facade.Commanders.GetAtPoint(localTeamId, destination);
            if (commanderAtDestination != null && GetFog(destination) == byte.MaxValue)
            {
                return IsDestinationInteractionMarkerActive(selectedCommander, destination, reachablePoint);
            }

            if (_facade.MapEntities == null || !_facade.MapEntities.ExistsAt(destination))
            {
                return false;
            }

            IMapEntity entity = _facade.MapEntities.GetAt(destination);
            if (entity == null)
            {
                return false;
            }

            return IsDestinationInteractionMarkerActive(selectedCommander, destination, reachablePoint)
                || _facade.Level.CanMoveToAndInteract(entity.Id, selectedCommander.Id);
        }

        private bool IsDestinationInteractionMarkerActive(ICommanderState selectedCommander, Vector2Int destination, PathNode reachablePoint)
        {
            if (selectedCommander == null)
            {
                return false;
            }

            Vector2Int reachable = WielderPath.ToVector2Int(reachablePoint);
            if (reachable == destination)
            {
                return true;
            }

            return _facade.Level.AreNeighbors(reachablePoint.point, new int2(destination.x, destination.y))
                && selectedCommander.MovesLeft - reachablePoint.travelCost >= 0.5f;
        }

        private bool IsSecondaryInputHolding()
        {
            return _inputManager != null
                && _inputManager.Screen != null
                && _inputManager.Screen.Secondary != null
                && _inputManager.Screen.Secondary.IsActive
                && _inputManager.Screen.Secondary.IsHolding;
        }

        private sealed class RoutePreviewInfo
        {
            public Vector2Int Destination;
            public PathNode ReachablePoint;
            public int ReachableIndex;
            public float MaxMovement;
            public int DestinationTravelTurns = 1;
            public bool IsInteractableDestination;
            public bool CanInteractDestinationThisTurn;
            public readonly Dictionary<Vector2Int, RouteTileInfo> Tiles = new Dictionary<Vector2Int, RouteTileInfo>();

            public RouteTileInfo GetOrCreate(Vector2Int position)
            {
                RouteTileInfo info;
                if (!Tiles.TryGetValue(position, out info))
                {
                    info = new RouteTileInfo();
                    Tiles.Add(position, info);
                }

                return info;
            }
        }

        private sealed class RouteTileInfo
        {
            public AdventureMapTile.PathIndicatorKind Kind;
            public int TravelTurns = 1;
            public int? FurthestReachableTurns;
            public bool IsInteractable;
            public bool CanInteractThisTurn;
            public int? CostMark;
            public bool HasRoutePreview;

            public AdventureMapTile.PathIndicatorInfo ToPathIndicator()
            {
                return new AdventureMapTile.PathIndicatorInfo
                {
                    Kind = Kind,
                    TravelTurns = TravelTurns,
                    FurthestReachableTurns = FurthestReachableTurns,
                    IsInteractable = IsInteractable,
                    CanInteractThisTurn = CanInteractThisTurn,
                    CostMark = CostMark,
                    HasRoutePreview = HasRoutePreview
                };
            }
        }
    }
}
