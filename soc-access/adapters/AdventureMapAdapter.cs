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
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapAdapter
    {
        private const byte ExploredButNotVisibleFogValue = 128;
        private const ushort ObjectiveBeaconBlueprintId = 50;
        private const ushort FallenBeaconBlueprintId = 158;
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(AdventureViewInstaller), "Container");

        private readonly DiContainer _container;
        private readonly IClientAdventureFacade _facade;
        private readonly ISelectionHandler _selectionHandler;
        private readonly IFogManager _fogManager;
        private readonly IGrid _grid;
        private readonly ICameraController _cameraController;
        private readonly object _cartographyConverter;
        private readonly IAdventureTooltipManager _tooltipManager;
        private readonly ILocalizationHandler _localizationHandler;
        private readonly ICartographyVisualManifest _cartographyVisualManifest;
        private readonly IHumanAdventureController _humanAdventureController;
        private readonly IHumanAdventureControllerFacade _humanAdventureControllerFacade;
        private readonly IInputManager _inputManager;
        private readonly ISystemPopups _systemPopups;
        private readonly AdventureMapRevealedRegistry _revealedRegistry;
        private readonly MethodInfo _worldToPointMethod;
        private readonly MethodInfo _pointToWorldMethod;
        private readonly MethodInfo _getTooltipForTilePositionMethod;
        private readonly FieldInfo _runtimeTooltipBehaviorField;
        private readonly FieldInfo _innerTooltipManagerField;
        private readonly FieldInfo _gamepadTooltipHandleField;
        private readonly FieldInfo _fogHasFinishedLoadingField;
        private readonly FieldInfo _currentInputModuleField;
        private GameObject _cursorOverlay;
        private RectTransform[] _cursorOverlaySegments;
        private Vector2Int? _focusedOverlayTile;

        public AdventureMapAdapter(AdventureViewInstaller installer, AdventureMapRevealedRegistry revealedRegistry = null)
            : this(
                installer,
                GetContainer(installer),
                Resolve<IClientAdventureFacade>(GetContainer(installer)),
                Resolve<ISelectionHandler>(GetContainer(installer)),
                Resolve<IFogManager>(GetContainer(installer)),
                Resolve<IGrid>(GetContainer(installer)),
                Resolve<ICameraController>(GetContainer(installer)),
                ResolveByTypeName(GetContainer(installer), "Lavapotion.Cartography.ICartographyConverter"),
                Resolve<IAdventureTooltipManager>(GetContainer(installer)),
                Resolve<ILocalizationHandler>(GetContainer(installer)),
                Resolve<ICartographyVisualManifest>(GetContainer(installer)),
                Resolve<IHumanAdventureController>(GetContainer(installer)),
                Resolve<IHumanAdventureControllerFacade>(GetContainer(installer)),
                Resolve<IInputManager>(GetContainer(installer)),
                Resolve<ISystemPopups>(GetContainer(installer)),
                revealedRegistry)
        {
        }

        public AdventureMapAdapter(
            object sourceKey,
            DiContainer container,
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            IFogManager fogManager,
            IGrid grid,
            ICameraController cameraController,
            object cartographyConverter,
            IAdventureTooltipManager tooltipManager,
            ILocalizationHandler localizationHandler,
            ICartographyVisualManifest cartographyVisualManifest,
            IHumanAdventureController humanAdventureController,
            IHumanAdventureControllerFacade humanAdventureControllerFacade,
            IInputManager inputManager,
            ISystemPopups systemPopups,
            AdventureMapRevealedRegistry revealedRegistry = null)
        {
            SourceKey = sourceKey;
            _container = container;
            _facade = facade;
            _selectionHandler = selectionHandler;
            _fogManager = fogManager;
            _grid = grid;
            _cameraController = cameraController;
            _cartographyConverter = cartographyConverter;
            _tooltipManager = tooltipManager;
            _localizationHandler = localizationHandler;
            _cartographyVisualManifest = cartographyVisualManifest;
            _humanAdventureController = humanAdventureController;
            _humanAdventureControllerFacade = humanAdventureControllerFacade;
            _inputManager = inputManager;
            _systemPopups = systemPopups;
            _revealedRegistry = revealedRegistry;
            _worldToPointMethod = cartographyConverter != null
                ? AccessTools.Method(cartographyConverter.GetType(), "WorldToPoint", new[] { typeof(float3) })
                : null;
            _pointToWorldMethod = cartographyConverter != null
                ? AccessTools.Method(cartographyConverter.GetType(), "PointToWorld", new[] { typeof(int2), typeof(int) })
                : null;
            Type tooltipManagerType = tooltipManager != null ? tooltipManager.GetType() : null;
            _getTooltipForTilePositionMethod = tooltipManagerType != null
                ? AccessTools.Method(tooltipManagerType, "GetTooltipForTilePosition", new[] { typeof(Vector2Int) })
                : null;
            _runtimeTooltipBehaviorField = tooltipManagerType != null
                ? AccessTools.Field(tooltipManagerType, "_tooltipBehavior")
                : null;
            _innerTooltipManagerField = tooltipManagerType != null
                ? AccessTools.Field(tooltipManagerType, "_tooltipManager")
                : null;
            _gamepadTooltipHandleField = tooltipManagerType != null
                ? AccessTools.Field(tooltipManagerType, "_gamepadTooltipHandle")
                : null;
            _fogHasFinishedLoadingField = fogManager != null
                ? AccessTools.Field(fogManager.GetType(), "_hasFinishedLoading")
                : null;
            _currentInputModuleField = humanAdventureController != null
                ? AccessTools.Field(humanAdventureController.GetType(), "_currentInputModule")
                : null;
            Hud = new AdventureHudAdapter(this, _container);
        }

        public object SourceKey { get; private set; }

        public AdventureHudAdapter Hud { get; private set; }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public ISelectionHandler SelectionHandler
        {
            get { return _selectionHandler; }
        }

        public IFogManager FogManager
        {
            get { return _fogManager; }
        }

        public IHumanAdventureControllerFacade HumanAdventureControllerFacade
        {
            get { return _humanAdventureControllerFacade; }
        }

        public ILocalizationHandler LocalizationHandler
        {
            get { return _localizationHandler; }
        }

        public ISystemPopups SystemPopups
        {
            get { return _systemPopups; }
        }

        public int LocalTeamId
        {
            get { return _facade != null && _facade.Teams != null ? _facade.Teams.LocalTeamInControlId : -1; }
        }

        public AdventureBookmarkGameIdentity GetBookmarkGameIdentity()
        {
            if (_facade == null || _facade.Level == null || _facade.Teams == null)
            {
                return null;
            }

            LevelStartInfo startInfo = _facade.Level.StartInfo;
            string mode = startInfo != null ? startInfo.Mode.ToString() : null;
            string campaignIdentifier = startInfo != null && startInfo.Campaign != null
                ? startInfo.Campaign.CampaignIdentifier
                : null;
            string mapFile = null;
            try
            {
                mapFile = _facade.Level.GetFileName();
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read map file name for bookmarks: " + exception.Message);
            }

            uint mapRandomSeed = _facade.MapSettings != null ? _facade.MapSettings.RandomSeed : 0;
            return AdventureBookmarkGameIdentity.Create(
                mode,
                mapFile,
                campaignIdentifier,
                mapRandomSeed,
                _facade.InstanceRandomSeed,
                LocalTeamId);
        }

        public bool IsValidMapTile(Vector2Int position)
        {
            return IsWithinMap(position);
        }

        private static T Resolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch
            {
                return null;
            }
        }

        private static DiContainer GetContainer(AdventureViewInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static object ResolveByTypeName(DiContainer container, string typeName)
        {
            if (container == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            try
            {
                return container.Resolve(type);
            }
            catch
            {
                return null;
            }
        }

        public bool IsPresent()
        {
            return GetReadinessDiagnostic() == null;
        }

        public string GetReadinessDiagnostic()
        {
            if (SourceKey == null)
            {
                return "missing source";
            }

            if (_container == null)
            {
                return "missing container";
            }

            if (_facade == null)
            {
                return "missing adventure facade";
            }

            if (_facade.Level == null)
            {
                return "missing level facade";
            }

            if (_facade.Teams == null)
            {
                return "missing team facade";
            }

            if (_facade.MapEntities == null)
            {
                return "missing map entity facade";
            }

            if (_facade.Commanders == null)
            {
                return "missing commander facade";
            }

            if (_selectionHandler == null)
            {
                return "missing selection handler";
            }

            if (_fogManager == null)
            {
                return "missing fog manager";
            }

            if (_cameraController == null)
            {
                return "missing camera controller";
            }

            if (_cartographyConverter == null)
            {
                return "missing cartography converter";
            }

            if (_tooltipManager == null)
            {
                return "missing tooltip manager";
            }

            if (_localizationHandler == null)
            {
                return "missing localization handler";
            }

            if (_cartographyVisualManifest == null)
            {
                return "missing cartography visual manifest";
            }

            if (_inputManager == null)
            {
                return "missing input manager";
            }

            if (_facade.Level.Width <= 0 || _facade.Level.Height <= 0)
            {
                return "invalid map size " + _facade.Level.Width + "x" + _facade.Level.Height;
            }

            if (!_facade.IsGameStarted)
            {
                return "game not started";
            }

            if (_facade.Teams.LocalTeamInControl == null || _facade.Teams.LocalTeamInControlId < 0)
            {
                return "missing local team";
            }

            if (!HumanAdventureController.CanLocalTeamUseHUD(_facade))
            {
                return "local team cannot use HUD";
            }

            if (!IsFogReady())
            {
                return "fog not ready";
            }

            return null;
        }

        public Vector2Int GetInitialTile()
        {
            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            if (selectedCommander != null && IsWithinMap(selectedCommander.Position))
            {
                return selectedCommander.Position;
            }

            IMapEntity selectedMapEntity = _selectionHandler.SelectedMapEntity;
            if (selectedMapEntity != null && IsWithinMap(selectedMapEntity.Position))
            {
                return selectedMapEntity.Position;
            }

            return GetCameraCenterTile();
        }

        public Vector2Int Move(Vector2Int currentTile, int xDelta, int yDelta)
        {
            return ClampToMap(new Vector2Int(currentTile.x + xDelta, currentTile.y + yDelta));
        }

        public AdventureMapTile GetTile(Vector2Int position)
        {
            Vector2Int clamped = ClampToMap(position);
            AdventureMapTile tile = new AdventureMapTile(clamped);
            int localTeamId = GetLocalTeamId();
            byte fog = GetFog(clamped);
            tile.IsVisible = fog == byte.MaxValue || _fogManager.IsVisible(clamped);
            tile.IsExplored = tile.IsVisible
                || fog == ExploredButNotVisibleFogValue
                || _facade.Level.GetIsPointExplored(localTeamId, clamped);
            PopulateZoneOfControl(tile, localTeamId);

            if (!tile.IsExplored)
            {
                return tile;
            }

            tile.Terrain = _facade.Level.GetGroundType(clamped);
            PopulateEnvironment(tile, clamped);
            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            tile.IsImpassable = float.IsPositiveInfinity(_facade.Level.GetStaticTravelCost(localTeamId, clamped));
            tile.IsBlocked = !tile.IsImpassable && !_facade.Level.IsValidMoveDestination(localTeamId, clamped);
            tile.IsReachable = selectedCommander != null
                && selectedCommander.IsAlive
                && _facade.Level.IsPointWithinReach(localTeamId, selectedCommander.Position, clamped, selectedCommander.MovesLeft);

            if (tile.IsVisible)
            {
                ICommanderState commander = GetCommanderAtVisiblePoint(clamped);
                if (commander != null)
                {
                    tile.Commander = CreateCommanderInfo(commander, selectedCommander, localTeamId);
                }
            }

            IMapEntity entity = GetRawMapEntityAt(clamped);
            if (entity != null && !entity.IsVisibleInGame)
            {
                entity = null;
            }

            if (entity != null && entity.Category == MapEntityCategory.Artistic)
            {
                entity = null;
            }

            if (entity != null && (tile.IsVisible || fog == ExploredButNotVisibleFogValue))
            {
                tile.MapEntity = entity;
                tile.MapEntityName = GetMapEntityName(entity);
                PopulateMapEntityTooltipSpeech(tile, entity, selectedCommander);
                tile.MapEntityRelationship = FormatSpatialRelationship(GetMapEntityRelationship(entity, localTeamId));
                tile.IsReachable = tile.IsReachable
                    || (selectedCommander != null && _facade.Level.CanMoveToAndInteract(entity.Id, selectedCommander.Id));
            }

            if (tile.IsVisible)
            {
                tile.IsInteractionPoint = _facade.MapEntities.IsInteractionPoint(localTeamId, clamped);
            }

            tile.PathIndicator = BuildPathIndicatorForTile(clamped, selectedCommander, localTeamId);

            return tile;
        }

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

        private RoutePreviewInfo BuildRoutePreviewInfo(ICommanderState selectedCommander, int localTeamId)
        {
            if (selectedCommander == null
                || selectedCommander.Destination == null
                || !selectedCommander.Destination.HasDestination
                || localTeamId < 0)
            {
                return null;
            }

            Vector2Int destination = selectedCommander.Destination.Destination;
            if (IsSecondaryInputHolding()
                || destination == selectedCommander.Position
                || !_facade.Level.IsPointWithinMapForTravel(destination)
                || !_facade.Level.GetIsPointExplored(selectedCommander.TeamId, destination)
                || _facade.Teams.IsNotInCurrentTurn(localTeamId))
            {
                return null;
            }

            PathNode[] fullPath = _facade.Level.PointsInPath(localTeamId, selectedCommander.Position, destination, (PathfinderCacheType)0);
            if (!PathfinderExtensions.GetIsValid(fullPath))
            {
                return null;
            }

            PathNode[] drawPath = fullPath.ToArray();
            if (drawPath.Length == 0)
            {
                return null;
            }

            Vector2Int originalLastPoint = ToVector2Int(drawPath[drawPath.Length - 1]);
            if (!_facade.Level.IsValidMoveDestination(localTeamId, originalLastPoint))
            {
                Array.Resize(ref drawPath, drawPath.Length - 1);
            }

            if (drawPath.Length < 2)
            {
                return null;
            }

            PathNode reachablePoint = _facade.Level.GetClosestReachablePoint(
                localTeamId,
                selectedCommander.Position,
                destination,
                selectedCommander.MovesLeft);
            int reachableIndex = FindNodeIndex(drawPath, reachablePoint);
            if (reachableIndex < 0)
            {
                reachableIndex = 0;
                reachablePoint = drawPath[0];
            }

            float maxMovement = selectedCommander.Stats != null && selectedCommander.Stats.Movement != null
                ? selectedCommander.Stats.Movement.GetValue()
                : 0f;
            if (maxMovement <= 0f)
            {
                maxMovement = 1f;
            }

            RoutePreviewInfo preview = new RoutePreviewInfo
            {
                Destination = destination,
                ReachablePoint = reachablePoint,
                ReachableIndex = reachableIndex,
                MaxMovement = maxMovement,
                IsInteractableDestination = IsInteractableDestination(selectedCommander, destination, localTeamId)
            };
            preview.CanInteractDestinationThisTurn = preview.IsInteractableDestination
                && CanInteractDestinationThisTurn(selectedCommander, destination, localTeamId, reachablePoint);

            for (int i = 1; i < drawPath.Length; i++)
            {
                PathNode node = drawPath[i];
                Vector2Int point = ToVector2Int(node);
                RouteTileInfo tileInfo = preview.GetOrCreate(point);
                tileInfo.Kind = point == destination
                    ? AdventureMapTile.PathIndicatorKind.Destination
                    : AdventureMapTile.PathIndicatorKind.OnRoute;
                tileInfo.TravelTurns = GetTravelTurns(node.travelCost, reachablePoint.travelCost, maxMovement);
                tileInfo.HasRoutePreview = true;
                if (point == destination)
                {
                    tileInfo.IsInteractable = preview.IsInteractableDestination;
                    tileInfo.CanInteractThisTurn = preview.CanInteractDestinationThisTurn;
                }
            }

            RouteTileInfo destinationInfo = preview.GetOrCreate(destination);
            destinationInfo.Kind = AdventureMapTile.PathIndicatorKind.Destination;
            destinationInfo.TravelTurns = GetTravelTurns(drawPath[drawPath.Length - 1].travelCost, reachablePoint.travelCost, maxMovement);
            destinationInfo.IsInteractable = preview.IsInteractableDestination;
            destinationInfo.CanInteractThisTurn = preview.CanInteractDestinationThisTurn;
            destinationInfo.HasRoutePreview = true;
            preview.DestinationTravelTurns = destinationInfo.TravelTurns;

            AddReachableBoundaryMarkers(preview, drawPath);
            AddCostMarks(preview, drawPath, reachableIndex);
            return preview;
        }

        private void AddReachableBoundaryMarkers(RoutePreviewInfo preview, PathNode[] drawPath)
        {
            if (preview == null || drawPath == null || drawPath.Length == 0)
            {
                return;
            }

            Vector2Int reachablePoint = ToVector2Int(preview.ReachablePoint);
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
                    preview.GetOrCreate(ToVector2Int(markerNode)).FurthestReachableTurns = Math.Max(2, nextNumber);
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
                    preview.GetOrCreate(ToVector2Int(previousToFinal)).FurthestReachableTurns = Math.Max(2, number);
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
                    preview.GetOrCreate(ToVector2Int(drawPath[i])).CostMark = currentCost;
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

            Vector2Int reachable = ToVector2Int(reachablePoint);
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

        private static int GetTravelTurns(float nodeCost, float reachableCost, float maxMovement)
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

        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            InitializeAdventureScannerCategories(snapshot);
            Dictionary<Vector2Int, AdventureMapTile> tileCache = new Dictionary<Vector2Int, AdventureMapTile>();
            int localTeamId = GetLocalTeamId();
            AddPickupScannerResults(snapshot, tileCache);
            AddResourceGeneratorScannerResults(snapshot, localTeamId, tileCache);
            AddBeaconScannerResults(snapshot, tileCache);
            AddWielderScannerResults(snapshot, tileCache);
            AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Town, MapEntityCategory.Settlement, MapEntityCategory.BuildSite);
            AddTroopSourceScannerResults(snapshot, localTeamId, tileCache);
            AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Building);
            AddObjectiveScannerResults(snapshot, tileCache);
            AddObstacleScannerResults(snapshot, localTeamId, origin, tileCache);
            AddArtifactMarketScannerResults(snapshot, tileCache);
            AddTeleportScannerResults(snapshot, tileCache);
            AddAdventureTerrainScannerResults(snapshot, origin, tileCache);
            AddRevealedScannerResults(snapshot);
            return snapshot;
        }

        public bool ValidateScannerResult(ScannerResult result)
        {
            if (result == null || !IsWithinMap(result.Position))
            {
                RemoveRevealedScannerResult(result);
                return false;
            }

            if (result.Kind == ScannerResultKind.CommanderZoneOfControl)
            {
                bool validZone = ValidateCommanderZoneOfControlResult(result);
                if (!validZone)
                {
                    RemoveRevealedScannerResult(result);
                }

                return validZone;
            }

            AdventureMapTile tile = GetTile(result.Position);
            if (tile == null || !tile.IsExplored)
            {
                RemoveRevealedScannerResult(result);
                return false;
            }

            if (result.StableReference is int stableId)
            {
                if (tile.Commander != null && tile.Commander.Raw != null && tile.Commander.Raw.Id == stableId)
                {
                    return true;
                }

                if (tile.MapEntity != null && tile.MapEntity.Id == stableId)
                {
                    return true;
                }

                RemoveRevealedScannerResult(result);
                return false;
            }

            return true;
        }

        private void RemoveRevealedScannerResult(ScannerResult result)
        {
            if (result == null || _revealedRegistry == null)
            {
                return;
            }

            _revealedRegistry.Remove(result.Key);
        }

        private void AddWielderScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (commander == null || !commander.IsAlive || !IsWithinMap(commander.Position))
                {
                    continue;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, commander.Position);
                if (tile == null || tile.Commander == null)
                {
                    continue;
                }

                string name = FirstNonEmpty(tile.Commander.Name, ModText.Get(ModStrings.Events.Wielder));
                snapshot.Add(ModText.Get(ModStrings.Scanner.Wielders), ModText.Get(ModStrings.Scanner.All),
                    new ScannerResult(ScannerKey("commander", commander.Id), name, commander.Position)
                    {
                        StableReference = commander.Id
                    });
            }
        }

        private void AddMapEntityScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            IEnumerable<IMapEntity> entities = _facade != null && _facade.MapEntities != null ? _facade.MapEntities.All : null;
            if (entities == null)
            {
                return;
            }

            foreach (IMapEntity entity in entities)
            {
                if (entity == null || !entity.IsEnabled || !IsWithinMap(entity.Position))
                {
                    continue;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                if (tile == null || tile.MapEntity == null || tile.MapEntity.Id != entity.Id)
                {
                    continue;
                }

                string name = FirstNonEmpty(tile.MapEntityName, GetMapEntityName(entity));
                bool notVisible = !tile.IsVisible;
                string relationship = FormatScannerRelationship(GetMapEntityRelationship(entity, localTeamId));
                ScannerResult result = new ScannerResult(ScannerKey("entity", entity.Id), name, entity.Position)
                {
                    NotVisible = notVisible,
                    StableReference = entity.Id
                };

                AddStructuralMapEntityResult(snapshot, entity, relationship, result);
                AddTroopSourceResult(snapshot, entity, relationship, result);
                if (IsScannerPickupEntity(entity))
                {
                    AddPickupResult(snapshot, entity, result);
                }

                AddSpecialMapEntityResult(snapshot, entity, result);
            }
        }

        private static void InitializeAdventureScannerCategories(ScannerSnapshot snapshot)
        {
            string all = ModText.Get(ModStrings.Scanner.All);
            ScannerCategory pickups = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Pickups));
            pickups.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Unvisited));
            pickups.GetOrAddSubcategory(all);
            pickups.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Knowledge));
            pickups.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Power));
            pickups.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Riches));

            AddRelationshipSubcategories(snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.ResourceGenerators)));
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Beacons)).GetOrAddSubcategory(all);
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Wielders)).GetOrAddSubcategory(all);
            AddRelationshipSubcategories(snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.SettlementsAndBuildSites)));
            AddRelationshipSubcategories(snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.TroopSources)));
            AddRelationshipSubcategories(snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Buildings)));
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Objectives)).GetOrAddSubcategory(all);
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Obstacles)).GetOrAddSubcategory(all);
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.ArtifactMarkets)).GetOrAddSubcategory(all);
            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Teleport)).GetOrAddSubcategory(all);

            ScannerCategory terrain = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Terrain));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Roads));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Bridges));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Water));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Impassable));

            ScannerCategory revealed = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Revealed));
            revealed.PreserveResultOrder = true;
            revealed.GetOrAddSubcategory(all);
        }

        private static void AddRelationshipSubcategories(ScannerCategory category)
        {
            category.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.All));
            category.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Neutral));
            category.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Friendly));
            category.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Enemy));
        }

        private void AddStructuralScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache, params MapEntityCategory[] categories)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsCategory(entity, categories))
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result == null)
                {
                    return;
                }

                string relationship = FormatScannerRelationship(GetMapEntityRelationship(entity, localTeamId));
                AddStructuralMapEntityResult(snapshot, entity, relationship, result);
            });
        }

        private void AddTroopSourceScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<IRecruitmentPoolComponent>() && !entity.HasComponent<ITroopDwellingComponent>())
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddTroopSourceResult(snapshot, entity, FormatScannerRelationship(GetMapEntityRelationship(entity, localTeamId)), result);
                }
            });
        }

        private void AddPickupScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsScannerPickupEntity(entity))
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddPickupResult(snapshot, entity, result);
                }
            });
        }

        private void AddResourceGeneratorScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (entity.Category != MapEntityCategory.ResourceGenerator)
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddResourceGeneratorResult(snapshot, FormatScannerRelationship(GetMapEntityRelationship(entity, localTeamId)), result);
                }
            });
        }

        private void AddBeaconScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsBeaconOfPowerEntity(entity))
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Beacons), ModText.Get(ModStrings.Scanner.All), result);
                }
            });
        }

        private void AddArtifactMarketScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<IArtifactMarketComponent>())
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.ArtifactMarkets), ModText.Get(ModStrings.Scanner.All), result);
                }
            });
        }

        private void AddObjectiveScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (entity.Category != MapEntityCategory.Objective && entity.Category != MapEntityCategory.Story)
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Objectives), ModText.Get(ModStrings.Scanner.All), result);
                }
            });
        }

        private void AddTeleportScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<ITeleportComponent>() && !entity.HasComponent<ITownPortalComponent>() && !entity.HasComponent<ITownPortalBuildingComponent>())
                {
                    return;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Teleport), ModText.Get(ModStrings.Scanner.All), result);
                }
            });
        }

        private void AddRevealedScannerResults(ScannerSnapshot snapshot)
        {
            if (_revealedRegistry == null || snapshot == null)
            {
                return;
            }

            IReadOnlyList<AdventureMapRevealedEntry> entries = _revealedRegistry.Entries;
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            ScannerCategory category = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Revealed));
            category.PreserveResultOrder = true;
            ScannerSubcategory all = category.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.All));
            for (int i = 0; i < entries.Count; i++)
            {
                AdventureMapRevealedEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Label))
                {
                    continue;
                }

                AdventureMapTile tile = IsWithinMap(entry.Position) ? GetTile(entry.Position) : null;
                all.Results.Add(new ScannerResult(entry.Key, entry.Label, entry.Position)
                {
                    NotVisible = tile != null && !tile.IsVisible,
                    StableReference = entry.StableReference
                });
            }
        }

        private void AddObstacleScannerResults(ScannerSnapshot snapshot, int localTeamId, Vector2Int origin, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result == null)
                {
                    return;
                }

                if (entity.Category == MapEntityCategory.Hostile)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), result);
                }
                else if (entity.HasComponent<IMagicGateCommonComponent>() || entity.HasComponent<IUnlockWithArtifactComponent>())
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), result);
                }
                else if (entity.Category == MapEntityCategory.Obstacle)
                {
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), result);
                }
            });

            AddHostileZoneOfControlScannerResults(snapshot, localTeamId, origin);
        }

        private void AddHostileZoneOfControlScannerResults(ScannerSnapshot snapshot, int localTeamId, Vector2Int origin)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null || localTeamId < 0)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (!IsOverlayVisibleZoneOfControlSource(commander) || !IsHostileZoneOfControlSource(commander, localTeamId))
                {
                    continue;
                }

                List<Vector2Int> points = GetZoneOfControlPoints(localTeamId, commander.Id);
                if (points.Count == 0)
                {
                    continue;
                }

                Vector2Int representative = ClosestPoint(points, origin);
                string name = FirstNonEmpty(GetCommanderName(commander), ModText.Get(ModStrings.Spatial.Commander));
                ScannerResult result = new ScannerResult(
                    ScannerKey("zoc", commander.Id),
                    ScannerResultLabels.ZoneOfControl(points.Count, name),
                    representative)
                {
                    Kind = ScannerResultKind.CommanderZoneOfControl,
                    StableReference = commander.Id
                };
                result.Points.AddRange(points);
                snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), result);
            }
        }

        private void PopulateZoneOfControl(AdventureMapTile tile, int localTeamId)
        {
            if (tile == null || localTeamId < 0)
            {
                return;
            }

            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (!IsOverlayVisibleZoneOfControlSource(commander))
                {
                    continue;
                }

                if (tile.Position == commander.Position || !ZoneOfControlContains(localTeamId, commander.Id, tile.Position))
                {
                    continue;
                }

                string name = FirstNonEmpty(GetCommanderName(commander), ModText.Get(ModStrings.Spatial.Commander));
                if (!ContainsString(tile.ZoneOfControlNames, name))
                {
                    tile.ZoneOfControlNames.Add(name);
                }
            }
        }

        private bool ValidateCommanderZoneOfControlResult(ScannerResult result)
        {
            if (!(result.StableReference is int commanderId))
            {
                return false;
            }

            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return false;
            }

            ICommanderState commander = FindCommanderById(commanderId);
            if (!IsOverlayVisibleZoneOfControlSource(commander) || !IsHostileZoneOfControlSource(commander, localTeamId))
            {
                return false;
            }

            return ZoneOfControlContains(localTeamId, commanderId, result.Position);
        }

        private bool IsOverlayVisibleZoneOfControlSource(ICommanderState commander)
        {
            if (commander == null || commander.InternalState != CommanderInternalState.Default || !IsWithinMap(commander.Position))
            {
                return false;
            }

            return _fogManager.GetFog(commander.Position.x, commander.Position.y) == byte.MaxValue;
        }

        private bool IsHostileZoneOfControlSource(ICommanderState commander, int localTeamId)
        {
            return commander != null
                && _facade.Teams != null
                && !_facade.Teams.IsInPartnership(commander.TeamId, localTeamId);
        }

        private List<Vector2Int> GetZoneOfControlPoints(int localTeamId, int commanderId)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            IEnumerable<int2> nativePoints = _facade != null && _facade.Commanders != null
                ? _facade.Commanders.GetZoneOfControlPoints(localTeamId, commanderId)
                : null;
            if (nativePoints == null)
            {
                return points;
            }

            ICommanderState commander = FindCommanderById(commanderId);
            foreach (int2 point in nativePoints)
            {
                Vector2Int vector = new Vector2Int(point.x, point.y);
                if (IsWithinMap(vector) && (commander == null || vector != commander.Position))
                {
                    points.Add(vector);
                }
            }

            return points;
        }

        private bool ZoneOfControlContains(int localTeamId, int commanderId, Vector2Int position)
        {
            IEnumerable<int2> nativePoints = _facade != null && _facade.Commanders != null
                ? _facade.Commanders.GetZoneOfControlPoints(localTeamId, commanderId)
                : null;
            if (nativePoints == null)
            {
                return false;
            }

            foreach (int2 point in nativePoints)
            {
                if (point.x == position.x && point.y == position.y)
                {
                    ICommanderState commander = FindCommanderById(commanderId);
                    if (commander != null && position == commander.Position)
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private ICommanderState FindCommanderById(int commanderId)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null)
            {
                return null;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (commander != null && commander.Id == commanderId)
                {
                    return commander;
                }
            }

            return null;
        }

        private void ForEachScannerEntity(Dictionary<Vector2Int, AdventureMapTile> tileCache, Action<IMapEntity> action)
        {
            IEnumerable<IMapEntity> entities = _facade != null && _facade.MapEntities != null ? _facade.MapEntities.All : null;
            if (entities == null || action == null)
            {
                return;
            }

            foreach (IMapEntity entity in entities)
            {
                if (entity == null || !entity.IsEnabled || !IsWithinMap(entity.Position))
                {
                    continue;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                if (tile == null || tile.MapEntity == null || tile.MapEntity.Id != entity.Id)
                {
                    continue;
                }

                action(entity);
            }
        }

        private ScannerResult CreateMapEntityScannerResult(IMapEntity entity, AdventureMapTile tile)
        {
            if (entity == null || tile == null || tile.MapEntity == null || tile.MapEntity.Id != entity.Id)
            {
                return null;
            }

            string name = FirstNonEmpty(tile.MapEntityName, GetMapEntityName(entity));
            return new ScannerResult(ScannerKey("entity", entity.Id), name, entity.Position)
            {
                NotVisible = !tile.IsVisible,
                StableReference = entity.Id
            };
        }

        private static bool IsCategory(IMapEntity entity, MapEntityCategory[] categories)
        {
            if (entity == null || categories == null)
            {
                return false;
            }

            for (int i = 0; i < categories.Length; i++)
            {
                if (entity.Category == categories[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void AddStructuralMapEntityResult(ScannerSnapshot snapshot, IMapEntity entity, string relationship, ScannerResult result)
        {
            switch (entity.Category)
            {
                case MapEntityCategory.Town:
                case MapEntityCategory.Settlement:
                case MapEntityCategory.BuildSite:
                    snapshot.Add(ModText.Get(ModStrings.Scanner.SettlementsAndBuildSites), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                    snapshot.Add(ModText.Get(ModStrings.Scanner.SettlementsAndBuildSites), relationship, CloneResult(result));
                    break;
                case MapEntityCategory.Building:
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Buildings), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                    snapshot.Add(ModText.Get(ModStrings.Scanner.Buildings), relationship, CloneResult(result));
                    break;
            }
        }

        private void AddResourceGeneratorResult(ScannerSnapshot snapshot, string relationship, ScannerResult result)
        {
            snapshot.Add(ModText.Get(ModStrings.Scanner.ResourceGenerators), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            snapshot.Add(ModText.Get(ModStrings.Scanner.ResourceGenerators), relationship, CloneResult(result));
        }

        private void AddTroopSourceResult(ScannerSnapshot snapshot, IMapEntity entity, string relationship, ScannerResult result)
        {
            if (entity.HasComponent<IRecruitmentPoolComponent>() || entity.HasComponent<ITroopDwellingComponent>())
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.TroopSources), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                snapshot.Add(ModText.Get(ModStrings.Scanner.TroopSources), relationship, CloneResult(result));
            }
        }

        private void AddPickupResult(ScannerSnapshot snapshot, IMapEntity entity, ScannerResult result)
        {
            MapEntityPreVisitDetails.PreVisitHint hint = GetPreVisitHint(entity);
            string subcategory = null;
            switch (hint)
            {
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfKnowledge:
                    subcategory = ModText.Get(ModStrings.Scanner.Knowledge);
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfPower:
                    subcategory = ModText.Get(ModStrings.Scanner.Power);
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfRiches:
                    subcategory = ModText.Get(ModStrings.Scanner.Riches);
                    break;
            }

            if (subcategory == null)
            {
                return;
            }

            snapshot.Add(ModText.Get(ModStrings.Scanner.Pickups), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            if (IsUnvisited(entity))
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Pickups), ModText.Get(ModStrings.Scanner.Unvisited), CloneResult(result));
            }

            snapshot.Add(ModText.Get(ModStrings.Scanner.Pickups), subcategory, CloneResult(result));
        }

        private static bool IsScannerPickupEntity(IMapEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return entity.Category == MapEntityCategory.Pickup
                || entity.Category == MapEntityCategory.Artifact
                || entity.Category == MapEntityCategory.Experience
                || entity.Category == MapEntityCategory.Spell
                || entity.Category == MapEntityCategory.Effect;
        }

        private static bool IsBeaconOfPowerEntity(IMapEntity entity)
        {
            return entity != null
                && (entity.BlueprintId == ObjectiveBeaconBlueprintId
                    || entity.BlueprintId == FallenBeaconBlueprintId);
        }

        private bool IsUnvisited(IMapEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
            return selectedCommander == null || !entity.DidVisit(selectedCommander.Id);
        }

        private void AddSpecialMapEntityResult(ScannerSnapshot snapshot, IMapEntity entity, ScannerResult result)
        {
            if (entity.HasComponent<IArtifactMarketComponent>())
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.ArtifactMarkets), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }

            if (entity.Category == MapEntityCategory.Objective || entity.Category == MapEntityCategory.Story)
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Objectives), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }

            if (entity.HasComponent<ITeleportComponent>() || entity.HasComponent<ITownPortalComponent>() || entity.HasComponent<ITownPortalBuildingComponent>())
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Teleport), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }

            if (entity.Category == MapEntityCategory.Hostile)
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }
            else if (entity.HasComponent<IMagicGateCommonComponent>() || entity.HasComponent<IUnlockWithArtifactComponent>())
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }
            else if (entity.Category == MapEntityCategory.Obstacle)
            {
                snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
            }
        }

        private MapEntityPreVisitDetails.PreVisitHint GetPreVisitHint(IMapEntity entity)
        {
            try
            {
                ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
                IDetails details = entity.GetPreVisitDetails(
                    selectedCommander != null ? selectedCommander.Id : -1,
                    false,
                    ScoutingDetailLevel.VeryFar,
                    null,
                    selectedCommander != null && selectedCommander.IsAlive);
                MapEntityPreVisitDetails preVisit = details as MapEntityPreVisitDetails;
                return preVisit != null ? preVisit.Hint : MapEntityPreVisitDetails.PreVisitHint.None;
            }
            catch
            {
                return MapEntityPreVisitDetails.PreVisitHint.None;
            }
        }

        private AdventureMapTile GetScannerTile(Dictionary<Vector2Int, AdventureMapTile> tileCache, Vector2Int position)
        {
            if (tileCache == null)
            {
                return GetTile(position);
            }

            Vector2Int clamped = ClampToMap(position);
            AdventureMapTile tile;
            if (!tileCache.TryGetValue(clamped, out tile))
            {
                tile = GetTile(clamped);
                tileCache.Add(clamped, tile);
            }

            return tile;
        }

        private void AddAdventureTerrainScannerResults(ScannerSnapshot snapshot, Vector2Int origin, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            if (_facade == null || _facade.Level == null)
            {
                return;
            }

            TerrainScanCell[,] terrain = BuildTerrainScan(GetLocalTeamId());
            AddTerrainGroups(snapshot, terrain, ModText.Get(ModStrings.Scanner.Roads), "road", ModStrings.Scanner.RoadTileCount, origin, cell => cell.Road);
            AddTerrainGroups(snapshot, terrain, ModText.Get(ModStrings.Scanner.Bridges), "bridge", ModStrings.Scanner.BridgeTileCount, origin, cell => cell.Bridge);
            AddTerrainGroups(snapshot, terrain, ModText.Get(ModStrings.Scanner.Water), "water", ModStrings.Scanner.WaterTileCount, origin, cell => cell.Water);
            AddTerrainGroups(snapshot, terrain, ModText.Get(ModStrings.Scanner.Impassable), "impassable", ModStrings.Scanner.ImpassableTileCount, origin, cell => cell.Impassable);
            AddScannerGroups(
                snapshot,
                ModText.Get(ModStrings.Scanner.Obstacles),
                ModText.Get(ModStrings.Scanner.All),
                terrain,
                "blocked",
                ModStrings.Scanner.BlockedTileCount,
                origin,
                cell => cell.Blocked);
        }

        private TerrainScanCell[,] BuildTerrainScan(int localTeamId)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            byte[] exploration = localTeamId >= 0 ? _facade.Level.GetExplorationForTeam(localTeamId) : null;
            TerrainScanCell[,] terrain = new TerrainScanCell[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Vector2Int point = new Vector2Int(x, y);
                    bool explored = IsExplored(exploration, index);
                    bool visible = false;
                    if (!explored && _fogManager != null)
                    {
                        visible = GetFog(point) == byte.MaxValue || _fogManager.IsVisible(point);
                    }

                    bool eligible = explored || visible;
                    bool impassable = eligible && IsImpassableTerrain(localTeamId, point);
                    terrain[x, y] = new TerrainScanCell
                    {
                        Explored = eligible,
                        Road = eligible && SafeGetRoad(index) > 0,
                        Bridge = eligible && SafeGetBridge(index) > 0,
                        Water = eligible && SafeGetWater(index) > 0,
                        Impassable = impassable,
                        Blocked = eligible && !impassable && IsBlockedTerrain(localTeamId, point)
                    };
                }
            }

            return terrain;
        }

        private static bool IsExplored(byte[] exploration, int index)
        {
            return exploration != null
                && index >= 0
                && index < exploration.Length
                && exploration[index] == ExploredButNotVisibleFogValue;
        }

        private bool IsImpassableTerrain(int localTeamId, Vector2Int point)
        {
            if (localTeamId < 0)
            {
                return false;
            }

            try
            {
                return float.IsPositiveInfinity(_facade.Level.GetStaticTravelCost(localTeamId, point));
            }
            catch
            {
                return false;
            }
        }

        private bool IsBlockedTerrain(int localTeamId, Vector2Int point)
        {
            if (localTeamId < 0)
            {
                return false;
            }

            try
            {
                return !_facade.Level.IsValidMoveDestination(localTeamId, point);
            }
            catch
            {
                return false;
            }
        }

        private byte SafeGetRoad(int index)
        {
            try
            {
                return _facade.Level.GetRoad(index);
            }
            catch
            {
                return 0;
            }
        }

        private byte SafeGetBridge(int index)
        {
            try
            {
                return _facade.Level.GetBridge(index);
            }
            catch
            {
                return 0;
            }
        }

        private byte SafeGetWater(int index)
        {
            try
            {
                return _facade.Level.GetWater(index);
            }
            catch
            {
                return 0;
            }
        }

        private void AddTerrainGroups(
            ScannerSnapshot snapshot,
            TerrainScanCell[,] terrain,
            string subcategory,
            string label,
            ModPluralString labelText,
            Vector2Int origin,
            Func<TerrainScanCell, bool> predicate)
        {
            AddScannerGroups(snapshot, ModText.Get(ModStrings.Scanner.Terrain), subcategory, terrain, label, labelText, origin, predicate);
        }

        private void AddScannerGroups(
            ScannerSnapshot snapshot,
            string category,
            string subcategory,
            TerrainScanCell[,] terrain,
            string label,
            ModPluralString labelText,
            Vector2Int origin,
            Func<TerrainScanCell, bool> predicate)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            bool[,] visited = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[x, y])
                    {
                        continue;
                    }

                    Vector2Int start = new Vector2Int(x, y);
                    if (!terrain[x, y].Explored || !predicate(terrain[x, y]))
                    {
                        visited[x, y] = true;
                        continue;
                    }

                    List<Vector2Int> group = FloodTerrainGroup(start, terrain, visited, predicate);
                    Vector2Int representative = ClosestPoint(group, origin);
                    ScannerResult result = new ScannerResult(
                        ScannerGroupKey(category, subcategory, label, group),
                        ModText.Plural(labelText, group.Count, group.Count),
                        representative)
                    {
                        Kind = category == "Terrain" ? ScannerResultKind.TerrainGroup : ScannerResultKind.AreaGroup
                    };
                    result.Points.AddRange(group);
                    snapshot.Add(category, subcategory, result);
                }
            }
        }

        private List<Vector2Int> FloodTerrainGroup(Vector2Int start, TerrainScanCell[,] terrain, bool[,] visited, Func<TerrainScanCell, bool> predicate)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                TerrainScanCell cell = terrain[point.x, point.y];
                if (!cell.Explored || !predicate(cell))
                {
                    continue;
                }

                result.Add(point);
                EnqueueTerrainNeighbors(queue, visited, point);
            }

            return result;
        }

        private struct TerrainScanCell
        {
            public bool Explored;

            public bool Road;

            public bool Bridge;

            public bool Water;

            public bool Impassable;

            public bool Blocked;
        }

        private void EnqueueTerrainNeighbors(Queue<Vector2Int> queue, bool[,] visited, Vector2Int point)
        {
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y);
            EnqueueTerrainNeighbor(queue, visited, point.x, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x, point.y - 1);
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y - 1);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y - 1);
        }

        private void EnqueueTerrainNeighbor(Queue<Vector2Int> queue, bool[,] visited, int x, int y)
        {
            if (x < 0 || y < 0 || x >= _facade.Level.Width || y >= _facade.Level.Height || visited[x, y])
            {
                return;
            }

            visited[x, y] = true;
            queue.Enqueue(new Vector2Int(x, y));
        }

        private static bool HasEnvironment(AdventureMapTile tile, string text)
        {
            if (tile == null || tile.Environment == null)
            {
                return false;
            }

            for (int i = 0; i < tile.Environment.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tile.Environment[i]) && tile.Environment[i].ToLowerInvariant().Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2Int ClosestPoint(List<Vector2Int> points, Vector2Int origin)
        {
            if (points == null || points.Count == 0)
            {
                return origin;
            }

            Vector2Int best = points[0];
            int bestDistance = DistanceSquared(origin, best);
            for (int i = 1; i < points.Count; i++)
            {
                int distance = DistanceSquared(origin, points[i]);
                if (distance < bestDistance)
                {
                    best = points[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }

        private static ScannerResult CloneResult(ScannerResult result)
        {
            ScannerResult clone = new ScannerResult(result.Key, result.Label, result.Position)
            {
                NotVisible = result.NotVisible,
                StableReference = result.StableReference,
                Kind = result.Kind
            };
            clone.Points.AddRange(result.Points);
            return clone;
        }

        private static string ScannerKey(string prefix, int id)
        {
            return prefix + ":" + id;
        }

        private static string ScannerGroupKey(string category, string subcategory, string label, List<Vector2Int> points)
        {
            List<Vector2Int> sorted = points != null ? new List<Vector2Int>(points) : new List<Vector2Int>();
            sorted.Sort((left, right) =>
            {
                int xCompare = left.x.CompareTo(right.x);
                return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
            });

            List<string> parts = new List<string>
            {
                "group",
                category ?? string.Empty,
                subcategory ?? string.Empty,
                label ?? string.Empty,
                sorted.Count.ToString()
            };
            for (int i = 0; i < sorted.Count; i++)
            {
                parts.Add(sorted[i].x + "," + sorted[i].y);
            }

            return string.Join(":", parts.ToArray());
        }

        private static string FormatScannerRelationship(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ModText.Get(ModStrings.Scanner.Neutral);
            }

            switch (value)
            {
                case "friendly":
                    return ModText.Get(ModStrings.Scanner.Friendly);
                case "enemy":
                    return ModText.Get(ModStrings.Scanner.Enemy);
                default:
                    return ModText.Get(ModStrings.Scanner.Neutral);
            }
        }

        private static string FormatSpatialRelationship(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ModText.Get(ModStrings.Spatial.Neutral);
            }

            switch (value)
            {
                case "friendly":
                    return ModText.Get(ModStrings.Spatial.Friendly);
                case "enemy":
                    return ModText.Get(ModStrings.Spatial.Enemy);
                default:
                    return ModText.Get(ModStrings.Spatial.Neutral);
            }
        }

        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsWithinMap(tile))
            {
                return;
            }

            try
            {
                EnsureCursorOverlay();
                if (_cursorOverlaySegments == null || _cursorOverlay == null)
                {
                    return;
                }

                SetScreenOverlayPosition(GetScreenPoint(tile));
                _cursorOverlay.SetActive(true);
                _focusedOverlayTile = tile;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public Tooltip GetTooltip(Vector2Int tile)
        {
            if (_tooltipManager == null || !ShouldShowFocusedTileTooltip(tile))
            {
                return null;
            }

            IDetails details = GetTooltipDetailsForTile(tile);
            if (details == null)
            {
                return null;
            }

            object runtimeTooltipBehavior = _runtimeTooltipBehaviorField?.GetValue(_tooltipManager);
            ITooltipable tooltipable = runtimeTooltipBehavior as ITooltipable;
            if (tooltipable == null)
            {
                return null;
            }

            DetailsTextUtility captured = DetailsTextUtility.Capture(details, _localizationHandler);
            List<string> textLines = new List<string>(captured.TextLines);
            EnrichArtifactTooltipLines(details, textLines);
            List<TooltipAction> actions = BuildMapTooltipActions(tile, captured.InstructionRows, textLines);
            return new Tooltip(
                () => textLines,
                new VisualTooltipMetadata(tooltipable, GetScreenPoint(tile), details),
                actions);
        }

        private void EnrichArtifactTooltipLines(IDetails details, List<string> textLines)
        {
            ArtifactPreVisitDetails artifactDetails = details as ArtifactPreVisitDetails;
            if (artifactDetails == null || artifactDetails.Artifacts == null || textLines == null || textLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < artifactDetails.Artifacts.Length; i++)
            {
                ArtifactDetails artifact = artifactDetails.Artifacts[i];
                string name = Localize(artifact.NameKey);
                string formattedName = ArtifactSpeechFormatter.FormatName(name, artifact.PowerLevelColor);
                if (string.IsNullOrWhiteSpace(name) || name == formattedName)
                {
                    continue;
                }

                ReplaceFirstTooltipLine(textLines, name, formattedName);
            }
        }

        private static bool ReplaceFirstTooltipLine(List<string> textLines, string oldText, string newText)
        {
            string normalizedOldText = SpeechTextSanitizer.Normalize(oldText);
            for (int i = 0; i < textLines.Count; i++)
            {
                if (SpeechTextSanitizer.Normalize(textLines[i]).Equals(normalizedOldText, StringComparison.OrdinalIgnoreCase))
                {
                    textLines[i] = newText;
                    return true;
                }
            }

            return false;
        }

        private List<TooltipAction> BuildMapTooltipActions(
            Vector2Int tile,
            IReadOnlyList<TooltipInstructionRow> instructionRows,
            List<string> textLines)
        {
            List<TooltipAction> actions = new List<TooltipAction>();
            if (instructionRows == null || instructionRows.Count == 0)
            {
                return actions;
            }

            for (int i = 0; i < instructionRows.Count; i++)
            {
                TooltipInstructionRow row = instructionRows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.Text))
                {
                    continue;
                }

                Vector2Int capturedTile = tile;
                if (IsPrimaryMapInstruction(row.InputType))
                {
                    RemoveExactLine(textLines, row.Text);
                    actions.Add(new TooltipAction(row.Text, () => HandlePrimaryAction(capturedTile)));
                }
                else if (IsSecondaryMapInstruction(row.InputType))
                {
                    RemoveExactLine(textLines, row.Text);
                    actions.Add(new TooltipAction(row.Text, () => HandleSecondaryAction(capturedTile)));
                }
            }

            return actions;
        }

        private bool IsPrimaryMapInstruction(InputType inputType)
        {
            // Map tooltip rows describe the native input that would activate the
            // row. When it is primary input, reuse the same primary map path as
            // pressing Enter on the accessibility cursor; the native map input
            // module decides what primary means for the focused tile.
            if (_inputManager != null)
            {
                return inputType == InputType.GetLeftMouseClickOrConfirm(_inputManager)
                    || inputType == InputType.LeftMouseClickOrSelect;
            }

            return inputType == InputType.LeftMouseClickOrConfirm
                || inputType == InputType.LeftMouseClickOrSelect;
        }

        private bool IsSecondaryMapInstruction(InputType inputType)
        {
            // Secondary rows cover Visit, Pickup, Attack, Claim, Repair, and
            // similar map interactions. Use the same path as the accessibility
            // map secondary key so we do not recreate game interaction rules.
            if (_inputManager != null)
            {
                return inputType == InputType.GetRightMouseClickOrCursorConfirm(_inputManager);
            }

            return inputType == InputType.RightMouseClickOrCursorConfirm;
        }

        private static void RemoveExactLine(List<string> lines, string lineToRemove)
        {
            if (lines == null || string.IsNullOrWhiteSpace(lineToRemove))
            {
                return;
            }

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (string.Equals(lines[i], lineToRemove, StringComparison.Ordinal))
                {
                    lines.RemoveAt(i);
                }
            }
        }

        public void EnsureTileInView(Vector2Int tile)
        {
            if (!IsWithinMap(tile) || _cameraController == null)
            {
                return;
            }

            try
            {
                Vector3 world = GetWorldCenter(tile);
                _cameraController.MoveToIncludePosition(
                    world,
                    0.10f,
                    0.10f,
                    0.10f,
                    0.10f);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to keep focused tile in view: " + exception.Message);
            }
        }

        public void MoveCameraToTile(Vector2Int tile)
        {
            if (!IsWithinMap(tile) || _cameraController == null)
            {
                return;
            }

            try
            {
                Vector3 world = GetWorldCenter(tile);
                _cameraController.MoveToPosition(world, false, Vector3.zero, null, true);
                SocAccessPlugin.Instance?.StartCoroutine(RefreshFocusedTileOverlayAfterCameraMove(tile));
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to move camera to focused tile: " + exception.Message);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (_cursorOverlay == null)
            {
                _focusedOverlayTile = null;
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(_cursorOverlay);
                _cursorOverlay = null;
                _cursorOverlaySegments = null;
                _focusedOverlayTile = null;
                _tooltipManager?.HideTileTooltip();
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to clear focused tile overlay: " + exception.Message);
            }
        }

        public bool HandlePrimaryAction(Vector2Int position)
        {
            Vector2Int tilePosition = ClampToMap(position);

            try
            {
                TryInvokeNativeMapInput(
                    tilePosition,
                    "primary",
                    "Map input is not ready.",
                    "Could not target tile.",
                    delegate(object inputModule, ScreenInputOverride screenInputOverride)
                    {
                        InvokeNativeInputModuleAction(inputModule, "HandlePrimaryInputStart");
                        InvokeNativeInputModuleAction(inputModule, "HandlePrimaryInputClick");
                    });
                return true;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter primary action failed: " + exception);
                PublishDenied(tilePosition, "Could not perform primary action.");
                return true;
            }
        }

        public bool HandleSecondaryAction(Vector2Int position)
        {
            Vector2Int tilePosition = ClampToMap(position);

            try
            {
                TryInvokeNativeMapInput(
                    tilePosition,
                    "secondary",
                    "Map interaction is not ready.",
                    "Could not target tile.",
                    delegate(object inputModule, ScreenInputOverride screenInputOverride)
                    {
                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputStart");
                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputEnded");
                    });
                return true;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter secondary action failed: " + exception);
                PublishDenied(tilePosition, "Could not perform secondary action.");
                return true;
            }
        }

        public bool TrySelectNextWielder()
        {
            if (_humanAdventureController == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not select next wielder: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextIdleCommander();
                return true;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter next wielder action failed: " + exception);
                return true;
            }
        }

        public bool TrySelectNextSettlement()
        {
            if (_humanAdventureController == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not select next settlement: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextTown();
                return true;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter next settlement action failed: " + exception);
                return true;
            }
        }

        private bool TryInvokeNativeMapInput(
            Vector2Int tilePosition,
            string inputName,
            string unavailableMessage,
            string targetFailureMessage,
            Action<object, ScreenInputOverride> invoke)
        {
            if (_humanAdventureControllerFacade == null)
            {
                PublishDenied(tilePosition, unavailableMessage);
                return false;
            }

            object inputModule = GetCurrentInputModule();
            if (inputModule == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not invoke native " + inputName + " action because current input module was null");
                PublishDenied(tilePosition, unavailableMessage);
                return false;
            }

            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(tilePosition, out screenInputOverride))
            {
                PublishDenied(tilePosition, targetFailureMessage);
                return false;
            }

            try
            {
                InvokeNativeInputModuleAction(inputModule, "UpdateCurrentTile");
                invoke?.Invoke(inputModule, screenInputOverride);
            }
            finally
            {
                screenInputOverride.Restore();
            }

            return true;
        }

        private bool TryBeginScreenInputOverride(Vector2Int tilePosition, out ScreenInputOverride screenInputOverride)
        {
            screenInputOverride = null;
            if (_inputManager == null || _inputManager.Screen == null || _inputManager.Screen.Primary == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            if (_cameraController == null || _cameraController.Camera == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because the adventure camera was unavailable");
                return false;
            }

            object response = ResolveWritableScreenInputResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
                return false;
            }

            Vector3 worldPosition = GetWorldCenter(tilePosition);
            Vector3 screenPosition3 = _cameraController.Camera.WorldToScreenPoint(worldPosition);
            Vector2 screenPosition = new Vector2(screenPosition3.x, screenPosition3.y);
            if (screenPosition3.z < 0f
                || screenPosition.x < 0f
                || screenPosition.y < 0f
                || screenPosition.x > Screen.width
                || screenPosition.y > Screen.height)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not target tile " + FormatTile(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
                return false;
            }

            screenInputOverride = ScreenInputOverride.Apply(response, screenPosition);
            return screenInputOverride != null;
        }

        private object ResolveWritableScreenInputResponse(object response)
        {
            if (response == null)
            {
                return null;
            }

            PropertyInfo positionProperty = AccessTools.Property(response.GetType(), "Position");
            if (positionProperty != null && positionProperty.CanWrite)
            {
                return response;
            }

            FieldInfo currentResponseField = AccessTools.Field(response.GetType(), "_currentResponse");
            object currentResponse = currentResponseField != null ? currentResponseField.GetValue(response) : null;
            positionProperty = currentResponse != null ? AccessTools.Property(currentResponse.GetType(), "Position") : null;
            if (positionProperty != null && positionProperty.CanWrite)
            {
                return currentResponse;
            }

            FieldInfo mouseResponseField = AccessTools.Field(response.GetType(), "_mouseResponse");
            object mouseResponse = mouseResponseField != null ? mouseResponseField.GetValue(response) : null;
            positionProperty = mouseResponse != null ? AccessTools.Property(mouseResponse.GetType(), "Position") : null;
            if (positionProperty != null && positionProperty.CanWrite)
            {
                return mouseResponse;
            }

            return null;
        }

        private sealed class ScreenInputOverride
        {
            private readonly object _response;
            private readonly PropertyInfo _positionProperty;
            private readonly PropertyInfo _deltaProperty;
            private readonly PropertyInfo _isOverUIProperty;
            private readonly PropertyInfo _isPanningProperty;
            private readonly PropertyInfo _wasActivatedOverUIProperty;
            private readonly object _oldPosition;
            private readonly object _oldDelta;
            private readonly object _oldIsOverUI;
            private readonly object _oldIsPanning;
            private readonly object _oldWasActivatedOverUI;
            private bool _restored;

            private ScreenInputOverride(
                object response,
                Vector2 screenPosition,
                PropertyInfo positionProperty,
                PropertyInfo deltaProperty,
                PropertyInfo isOverUIProperty,
                PropertyInfo isPanningProperty,
                PropertyInfo wasActivatedOverUIProperty)
            {
                _response = response;
                ScreenPosition = screenPosition;
                _positionProperty = positionProperty;
                _deltaProperty = deltaProperty;
                _isOverUIProperty = isOverUIProperty;
                _isPanningProperty = isPanningProperty;
                _wasActivatedOverUIProperty = wasActivatedOverUIProperty;
                _oldPosition = _positionProperty.GetValue(_response, null);
                _oldDelta = _deltaProperty.GetValue(_response, null);
                _oldIsOverUI = _isOverUIProperty.GetValue(_response, null);
                _oldIsPanning = _isPanningProperty.GetValue(_response, null);
                _oldWasActivatedOverUI = _wasActivatedOverUIProperty.GetValue(_response, null);

                _positionProperty.SetValue(_response, screenPosition, null);
                _deltaProperty.SetValue(_response, Vector2.zero, null);
                _isOverUIProperty.SetValue(_response, false, null);
                _isPanningProperty.SetValue(_response, false, null);
                _wasActivatedOverUIProperty.SetValue(_response, false, null);
            }

            public Vector2 ScreenPosition { get; private set; }

            public static ScreenInputOverride Apply(object response, Vector2 screenPosition)
            {
                if (response == null)
                {
                    return null;
                }

                Type responseType = response.GetType();
                PropertyInfo positionProperty = GetWritableProperty(responseType, "Position");
                PropertyInfo deltaProperty = GetWritableProperty(responseType, "Delta");
                PropertyInfo isOverUIProperty = GetWritableProperty(responseType, "IsOverUI");
                PropertyInfo isPanningProperty = GetWritableProperty(responseType, "IsPanning");
                PropertyInfo wasActivatedOverUIProperty = GetWritableProperty(responseType, "WasActivatedOverUI");
                if (positionProperty == null
                    || deltaProperty == null
                    || isOverUIProperty == null
                    || isPanningProperty == null
                    || wasActivatedOverUIProperty == null)
                {
                    SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because required writable properties were missing on " + responseType.FullName);
                    return null;
                }

                return new ScreenInputOverride(
                    response,
                    screenPosition,
                    positionProperty,
                    deltaProperty,
                    isOverUIProperty,
                    isPanningProperty,
                    wasActivatedOverUIProperty);
            }

            public void Restore()
            {
                if (_restored)
                {
                    return;
                }

                _positionProperty.SetValue(_response, _oldPosition, null);
                _deltaProperty.SetValue(_response, _oldDelta, null);
                _isOverUIProperty.SetValue(_response, _oldIsOverUI, null);
                _isPanningProperty.SetValue(_response, _oldIsPanning, null);
                _wasActivatedOverUIProperty.SetValue(_response, _oldWasActivatedOverUI, null);
                _restored = true;
            }

            private static PropertyInfo GetWritableProperty(Type type, string name)
            {
                PropertyInfo property = AccessTools.Property(type, name);
                return property != null && property.CanWrite ? property : null;
            }
        }

        private object GetCurrentInputModule()
        {
            if (_humanAdventureController == null || _currentInputModuleField == null)
            {
                return null;
            }

            return _currentInputModuleField.GetValue(_humanAdventureController);
        }

        private void InvokeNativeInputModuleAction(object inputModule, string methodName)
        {
            MethodInfo method = inputModule != null ? AccessTools.Method(inputModule.GetType(), methodName) : null;
            if (method == null)
            {
                throw new MissingMethodException(inputModule != null ? inputModule.GetType().FullName : "<null>", methodName);
            }

            method.Invoke(inputModule, null);
        }

        private void PublishDenied(Vector2Int tilePosition, string message)
        {
            AccessibilityEventBus.Publish(new MapActionFailedEvent(tilePosition, message));
        }

        private Vector2Int GetCameraCenterTile()
        {
            if (_cameraController != null && _worldToPointMethod != null)
            {
                try
                {
                    Vector3 centerPosition = _cameraController.CalculateCenterPosition();
                    object point = _worldToPointMethod.Invoke(_cartographyConverter, new object[] { new float3(centerPosition.x, centerPosition.y, centerPosition.z) });
                    if (point is int2)
                    {
                        int2 intPoint = (int2)point;
                        return ClampToMap(new Vector2Int(intPoint.x, intPoint.y));
                    }
                }
                catch (Exception exception)
                {
                    SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to resolve camera center tile: " + exception.Message);
                }
            }

            return ClampToMap(new Vector2Int(_facade.Level.Width / 2, _facade.Level.Height / 2));
        }

        private void EnsureCursorOverlay()
        {
            if (_cursorOverlay != null && _cursorOverlaySegments != null)
            {
                return;
            }

            _cursorOverlay = new GameObject("SongsOfConquestAccess_AdventureMapCursor");
            Canvas canvas = _cursorOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Keep the cursor above map visuals and the native overlay canvas
            // (29998), but below native tooltip canvases (30001) and windows.
            canvas.sortingOrder = 29999;
            CanvasScaler scaler = _cursorOverlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            CanvasGroup canvasGroup = _cursorOverlay.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            _cursorOverlaySegments = new[]
            {
                CreateOverlaySegment("Top"),
                CreateOverlaySegment("Right"),
                CreateOverlaySegment("Bottom"),
                CreateOverlaySegment("Left")
            };
        }

        private RectTransform CreateOverlaySegment(string name)
        {
            GameObject segment = new GameObject(name);
            segment.transform.SetParent(_cursorOverlay.transform, false);
            Image image = segment.AddComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private void SetScreenOverlayPosition(Vector2 point)
        {
            const float size = 42f;
            const float thickness = 4f;
            if (_cursorOverlaySegments == null || _cursorOverlaySegments.Length != 4)
            {
                return;
            }

            SetSegment(_cursorOverlaySegments[0], point + new Vector2(0f, size * 0.5f), new Vector2(size, thickness));
            SetSegment(_cursorOverlaySegments[1], point + new Vector2(size * 0.5f, 0f), new Vector2(thickness, size));
            SetSegment(_cursorOverlaySegments[2], point + new Vector2(0f, -size * 0.5f), new Vector2(size, thickness));
            SetSegment(_cursorOverlaySegments[3], point + new Vector2(-size * 0.5f, 0f), new Vector2(thickness, size));
        }

        private static void SetSegment(RectTransform segment, Vector2 position, Vector2 size)
        {
            segment.anchoredPosition = position;
            segment.sizeDelta = size;
        }

        private IEnumerator RefreshFocusedTileOverlayAfterCameraMove(Vector2Int tile)
        {
            for (int i = 0; i < 30; i++)
            {
                yield return null;

                if (!_focusedOverlayTile.HasValue || _focusedOverlayTile.Value != tile)
                {
                    yield break;
                }

                SetFocusedTileOverlay(tile);
                if (_cameraController == null || !_cameraController.IsMoving)
                {
                    yield break;
                }
            }
        }

        private Vector3 GetWorldCenter(Vector2Int tile)
        {
            if (_pointToWorldMethod != null)
            {
                try
                {
                    object world = _pointToWorldMethod.Invoke(_cartographyConverter, new object[] { new int2(tile.x, tile.y), -1 });
                    if (world is float3)
                    {
                        float3 point = (float3)world;
                        return new Vector3(point.x, point.y, point.z);
                    }
                }
                catch (Exception exception)
                {
                    SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to resolve tile world position: " + exception.Message);
                }
            }

            return new Vector3(tile.x, 0f, tile.y);
        }

        private void ShowFocusedTileTooltip(Vector2Int tile)
        {
            if (_tooltipManager == null)
            {
                return;
            }

            _tooltipManager.HideTileTooltip();
            if (!ShouldShowFocusedTileTooltip(tile))
            {
                return;
            }

            IDetails details = GetTooltipDetailsForTile(tile);
            if (details == null)
            {
                return;
            }

            object runtimeTooltipBehavior = _runtimeTooltipBehaviorField?.GetValue(_tooltipManager);
            ITooltipManager innerTooltipManager = _innerTooltipManagerField?.GetValue(_tooltipManager) as ITooltipManager;
            if (runtimeTooltipBehavior == null || innerTooltipManager == null)
            {
                return;
            }

            Vector2 point = GetScreenPoint(tile);
            ITooltipManager.Handle handle = innerTooltipManager.ForceDisplayTooltip(
                runtimeTooltipBehavior as ITooltipable,
                new TooltipLocation(point),
                details);
            if (_gamepadTooltipHandleField != null)
            {
                _gamepadTooltipHandleField.SetValue(_tooltipManager, handle);
            }
        }

        private bool ShouldShowFocusedTileTooltip(Vector2Int tile)
        {
            int localTeamId = GetLocalTeamId();
            try
            {
                if (_facade.MapEntities.GetAt(tile) != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                return _facade.Commanders.ExistsAtPoint(localTeamId, tile);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IDetails GetTooltipDetailsForTile(Vector2Int tile)
        {
            if (_getTooltipForTilePositionMethod == null)
            {
                return null;
            }

            try
            {
                return _getTooltipForTilePositionMethod.Invoke(_tooltipManager, new object[] { tile }) as IDetails;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to get focused tile tooltip details: " + exception.Message);
                return null;
            }
        }

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            Vector3 world = GetWorldCenter(tile);
            if (_cameraController == null || _cameraController.Camera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 point = _cameraController.Camera.WorldToScreenPoint(world);
            return new Vector2(point.x, point.y);
        }

        private Vector2Int ClampToMap(Vector2Int position)
        {
            int x = Math.Max(0, Math.Min(_facade.Level.Width - 1, position.x));
            int y = Math.Max(0, Math.Min(_facade.Level.Height - 1, position.y));
            return new Vector2Int(x, y);
        }

        private bool IsWithinMap(Vector2Int position)
        {
            return _facade != null
                && _facade.Level != null
                && position.x >= 0
                && position.y >= 0
                && position.x < _facade.Level.Width
                && position.y < _facade.Level.Height;
        }

        private int GetLocalTeamId()
        {
            return _facade.Teams.LocalTeamInControlId;
        }

        private bool IsFogReady()
        {
            if (_fogManager.width != _facade.Level.Width || _fogManager.height != _facade.Level.Height)
            {
                return false;
            }

            if (_fogHasFinishedLoadingField == null)
            {
                return true;
            }

            try
            {
                object value = _fogHasFinishedLoadingField.GetValue(_fogManager);
                return value is bool && (bool)value;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read fog readiness: " + exception.Message);
                return false;
            }
        }

        private byte GetFog(Vector2Int position)
        {
            try
            {
                return _fogManager.GetFog(position.x, position.y);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private string GetCommanderRelationship(ICommanderState commander, int localTeamId)
        {
            if (commander == null)
            {
                return string.Empty;
            }

            if (commander.TeamId == localTeamId)
            {
                return "friendly";
            }

            if (_facade.Teams != null && commander.TeamId == _facade.Teams.GetNeutralTeamId)
            {
                return "neutral";
            }

            return _facade.Teams.IsInPartnership(localTeamId, commander.TeamId) ? "friendly" : "enemy";
        }

        private AdventureMapTile.CommanderInfo CreateCommanderInfo(
            ICommanderState commander,
            ICommanderState selectedCommander,
            int localTeamId)
        {
            AdventureMapTile.CommanderInfo info = new AdventureMapTile.CommanderInfo
            {
                Raw = commander,
                Name = GetCommanderName(commander),
                IsSelected = ReferenceEquals(commander, selectedCommander),
                Relationship = FormatSpatialRelationship(GetCommanderRelationship(commander, localTeamId)),
                IsOwnedByLocalTeam = commander != null && commander.TeamId == localTeamId,
                MovementLabel = GameText.Get(_localizationHandler, "Commanders/Tooltip/Movement", "Movement")
            };

            if (!info.IsOwnedByLocalTeam || commander == null)
            {
                return info;
            }

            info.MovesLeft = commander.MovesLeft;
            if (commander.Stats != null && commander.Stats.Movement != null)
            {
                info.MaxMovement = commander.Stats.Movement.GetValue();
            }

            if (commander.Destination == null || !commander.Destination.HasDestination)
            {
                return info;
            }

            info.HasDestination = true;
            info.Destination = commander.Destination.Destination;
            PopulateThisTurnDestination(info);
            return info;
        }

        private void PopulateThisTurnDestination(AdventureMapTile.CommanderInfo info)
        {
            if (info == null || info.Raw == null || !info.HasDestination || _facade == null || _facade.Level == null)
            {
                return;
            }

            try
            {
                var reachablePointInPath = _facade.Level.GetReachablePointInPath(info.Raw, info.Destination);
                if (reachablePointInPath.Item1)
                {
                    info.HasThisTurnDestination = true;
                    info.ThisTurnDestination = reachablePointInPath.Item2;
                }
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read commander destination path: " + exception.Message);
            }
        }

        private ICommanderState GetCommanderAtVisiblePoint(Vector2Int position)
        {
            IEnumerable<ICommanderState> commanders = _facade.Commanders.All;
            if (commanders == null)
            {
                return null;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (commander != null && commander.IsAlive && commander.Position == position)
                {
                    return commander;
                }
            }

            return null;
        }

        private string GetMapEntityRelationship(IMapEntity entity, int localTeamId)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            if (_facade.MapEntities.IsOwnedByNeutralTeam(entity))
            {
                return "neutral";
            }

            int owningTeamId = _facade.MapEntities.GetOwningTeamId(entity);
            if (owningTeamId < 0)
            {
                return "neutral";
            }

            if (owningTeamId == localTeamId)
            {
                return "friendly";
            }

            return _facade.Teams.IsInPartnership(localTeamId, owningTeamId) ? "friendly" : "enemy";
        }

        private IMapEntity GetRawMapEntityAt(Vector2Int position)
        {
            try
            {
                return _facade.MapEntities.GetAt(position);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool ContainsInteractionPoint(int mapEntityId, Vector2Int position)
        {
            Vector2Int[] interactionPoints = null;
            try
            {
                interactionPoints = _facade.MapEntities.GetInteractionPoints(mapEntityId);
            }
            catch (Exception)
            {
                return false;
            }

            if (interactionPoints == null)
            {
                return false;
            }

            for (int i = 0; i < interactionPoints.Length; i++)
            {
                if (interactionPoints[i] == position)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + "," + tile.y;
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        }

        private string GetCommanderName(ICommanderState commander)
        {
            if (commander == null || _facade == null || _facade.Commanders == null)
            {
                return string.Empty;
            }

            try
            {
                return _facade.Commanders.GetName(commander.Id);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool ContainsString(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatPossessive(string name)
        {
            return ModText.FormatPossessiveName(name, ModStrings.Spatial.CommanderPossessive);
        }

        private void PopulateEnvironment(AdventureMapTile tile, Vector2Int position)
        {
            if (tile == null)
            {
                return;
            }

            AddEnvironment(tile, GetRoadName(GetLayerValue(position, LayerKind.Road)));
            AddEnvironment(tile, GetBridgeName(GetLayerValue(position, LayerKind.Bridge)));
            AddEnvironment(tile, GetWaterName(GetLayerValue(position, LayerKind.Water)));
        }

        private void AddEnvironment(AdventureMapTile tile, string value)
        {
            if (tile == null || string.IsNullOrWhiteSpace(value) || tile.Environment.Contains(value))
            {
                return;
            }

            tile.Environment.Add(value);
        }

        private byte GetLayerValue(Vector2Int position, LayerKind kind)
        {
            try
            {
                switch (kind)
                {
                    case LayerKind.Road:
                        return _facade.Level.GetRoad(position);
                    case LayerKind.Bridge:
                        return _facade.Level.GetBridge(position);
                    case LayerKind.Water:
                        return _facade.Level.GetWater(position);
                    default:
                        return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private string GetRoadName(byte road)
        {
            switch (road)
            {
                case 1:
                    return ModText.Get(ModStrings.Spatial.DirtRoad);
                case 2:
                    return ModText.Get(ModStrings.Spatial.CobblestoneRoad);
                default:
                    return road > 0 ? ModText.Get(ModStrings.Spatial.Road) : string.Empty;
            }
        }

        private string GetBridgeName(byte bridge)
        {
            if (bridge == 0)
            {
                return string.Empty;
            }

            BrushSet brush = _cartographyVisualManifest.GetBridgeBrush(bridge);
            return FormatBrushName(brush.name, ModStrings.Spatial.Bridge);
        }

        private string GetWaterName(byte water)
        {
            switch (water)
            {
                case 1:
                    return ModText.Get(ModStrings.Spatial.ShallowWater);
                case 2:
                    return ModText.Get(ModStrings.Spatial.DeepWater);
                case 3:
                    return ModText.Get(ModStrings.Spatial.WaterEdge);
                default:
                    return water > 0 ? ModText.Get(ModStrings.Spatial.Water) : string.Empty;
            }
        }

        private static string FormatBrushName(string value, ModString fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                return ModText.Get(fallback);
            }

            string normalized = value.Replace('_', ' ').Replace('-', ' ').Replace('/', ' ');
            ModString localized;
            return TryGetKnownBrushString(normalized, out localized)
                ? ModText.Get(localized)
                : ModText.Get(fallback);
        }

        private static bool TryGetKnownBrushString(string value, out ModString localized)
        {
            switch (FormatEnumName(value).ToLowerInvariant())
            {
                case "arid trees":
                case "arid tree":
                    localized = ModStrings.Spatial.AridTrees;
                    return true;
                case "bridge":
                    localized = ModStrings.Spatial.Bridge;
                    return true;
                case "cobblestone road":
                    localized = ModStrings.Spatial.CobblestoneRoad;
                    return true;
                case "deep water":
                    localized = ModStrings.Spatial.DeepWater;
                    return true;
                case "deforestation":
                    localized = ModStrings.Spatial.Deforestation;
                    return true;
                case "dirt road":
                    localized = ModStrings.Spatial.DirtRoad;
                    return true;
                case "farmland":
                    localized = ModStrings.Spatial.Farmland;
                    return true;
                case "fog":
                    localized = ModStrings.Spatial.Fog;
                    return true;
                case "light":
                    localized = ModStrings.Spatial.Light;
                    return true;
                case "mountain":
                case "mountains":
                    localized = ModStrings.Spatial.Mountains;
                    return true;
                case "road":
                    localized = ModStrings.Spatial.Road;
                    return true;
                case "shallow water":
                    localized = ModStrings.Spatial.ShallowWater;
                    return true;
                case "tree":
                case "trees":
                    localized = ModStrings.Spatial.Trees;
                    return true;
                case "wall":
                    localized = ModStrings.Spatial.Wall;
                    return true;
                case "water":
                    localized = ModStrings.Spatial.Water;
                    return true;
                case "water edge":
                    localized = ModStrings.Spatial.WaterEdge;
                    return true;
                default:
                    localized = default(ModString);
                    return false;
            }
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]) && value[i - 1] != ' ')
                {
                    chars.Add(' ');
                }

                chars.Add(current);
            }

            string formatted = new string(chars.ToArray()).Trim();
            if (formatted.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(formatted[0]) + (formatted.Length > 1 ? formatted.Substring(1) : string.Empty);
        }

        private void PopulateMapEntityTooltipSpeech(AdventureMapTile tile, IMapEntity entity, ICommanderState selectedCommander)
        {
            if (tile == null || entity == null)
            {
                return;
            }

            try
            {
                if (selectedCommander != null && entity.DidVisit(selectedCommander.Id))
                {
                    tile.MapEntityVisited = true;
                    return;
                }

                IDetails details = entity.GetPreVisitDetails(
                    selectedCommander != null ? selectedCommander.Id : -1,
                    false,
                    ScoutingDetailLevel.VeryFar,
                    null,
                    selectedCommander != null && selectedCommander.IsAlive);

                MapEntityPreVisitDetails preVisitDetails = details as MapEntityPreVisitDetails;
                if (preVisitDetails == null)
                {
                    return;
                }

                string name = Localize(preVisitDetails.NameKey);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    tile.MapEntityName = name;
                }

                if (preVisitDetails.Hint != MapEntityPreVisitDetails.PreVisitHint.None)
                {
                    tile.MapEntityHint = Localize("Adventure/Tooltips/PreVisitHint/" + preVisitDetails.Hint);
                }

            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read map entity tooltip details: " + exception.Message);
            }
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string customNameKey = string.Empty;
            if (entity.TryGetCustomNameKey(out customNameKey))
            {
                string customName = Localize(customNameKey);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return customName;
                }
            }

            string localizedName = Localize(entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return entity.Name;
            }

            return entity.NameKey;
        }

        private string Localize(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || _localizationHandler == null)
            {
                return string.Empty;
            }

            try
            {
                string text = _localizationHandler.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? string.Empty : text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private enum LayerKind
        {
            Road,
            Bridge,
            Water
        }

    }
}
