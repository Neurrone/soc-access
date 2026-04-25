using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapAdapter
    {
        private const byte ExploredButNotVisibleFogValue = 128;

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
        private readonly MethodInfo _worldToPointMethod;
        private readonly MethodInfo _pointToWorldMethod;
        private readonly MethodInfo _getTooltipForTilePositionMethod;
        private readonly FieldInfo _runtimeTooltipBehaviorField;
        private readonly FieldInfo _innerTooltipManagerField;
        private readonly FieldInfo _gamepadTooltipHandleField;
        private readonly FieldInfo _fogHasFinishedLoadingField;
        private GameObject _cursorOverlay;
        private LineRenderer _cursorLine;

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
            ICartographyVisualManifest cartographyVisualManifest)
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
        }

        public object SourceKey { get; private set; }

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

            if (!tile.IsExplored)
            {
                LogTileDiagnostic(tile, fog, "skipped entity lookup because tile is unexplored");
                return tile;
            }

            tile.Terrain = _facade.Level.GetGroundType(clamped);
            PopulateEnvironment(tile, clamped);
            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            tile.IsBlocked = float.IsPositiveInfinity(_facade.Level.GetTravelCost(localTeamId, clamped, addExplorationCost: false));
            tile.IsReachable = selectedCommander != null
                && selectedCommander.IsAlive
                && _facade.Level.IsPointWithinReach(localTeamId, selectedCommander.Position, clamped, selectedCommander.MovesLeft);

            if (tile.IsVisible)
            {
                tile.Commander = GetCommanderAtVisiblePoint(clamped);
                if (tile.Commander != null)
                {
                    tile.CommanderName = _facade.Commanders.GetName(tile.Commander.Id);
                    tile.IsSelectedCommander = ReferenceEquals(tile.Commander, selectedCommander);
                    tile.CommanderRelationship = GetCommanderRelationship(tile.Commander, localTeamId);
                }
            }

            string entityResolutionDiagnostic;
            IMapEntity entity = GetRawMapEntityAt(clamped, out entityResolutionDiagnostic);
            if (entity != null && !entity.IsVisibleInGame)
            {
                entityResolutionDiagnostic += "; filtered because visibleInGame=False";
                entity = null;
            }

            if (entity != null && entity.Category == MapEntityCategory.Artistic)
            {
                entityResolutionDiagnostic += "; filtered because category=Artistic";
                entity = null;
            }

            if (entity != null && (tile.IsVisible || fog == ExploredButNotVisibleFogValue))
            {
                tile.MapEntity = entity;
                tile.MapEntityName = GetMapEntityName(entity);
                PopulateMapEntityTooltipSpeech(tile, entity, selectedCommander);
                tile.MapEntityRelationship = GetMapEntityRelationship(entity, localTeamId);
                tile.IsReachable = tile.IsReachable
                    || (selectedCommander != null && _facade.Level.CanMoveToAndInteract(entity.Id, selectedCommander.Id));
            }

            if (tile.IsVisible)
            {
                tile.IsInteractionPoint = _facade.MapEntities.IsInteractionPoint(localTeamId, clamped);
            }

            LogTileDiagnostic(tile, fog, entityResolutionDiagnostic);
            return tile;
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
                if (_cursorLine == null || _cursorOverlay == null)
                {
                    return;
                }

                if (ShouldShowTileDiamond(tile))
                {
                    Vector3[] points = GetTileOutlinePoints(tile);
                    _cursorLine.positionCount = points.Length;
                    _cursorLine.SetPositions(points);
                    _cursorOverlay.SetActive(true);
                }
                else
                {
                    _cursorOverlay.SetActive(false);
                }

                ShowFocusedTileTooltip(tile);
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (_cursorOverlay == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(_cursorOverlay);
                _cursorOverlay = null;
                _cursorLine = null;
                _tooltipManager?.HideTileTooltip();
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to clear focused tile overlay: " + exception.Message);
            }
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
                    SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to resolve camera center tile: " + exception.Message);
                }
            }

            return ClampToMap(new Vector2Int(_facade.Level.Width / 2, _facade.Level.Height / 2));
        }

        private void EnsureCursorOverlay()
        {
            if (_cursorOverlay != null && _cursorLine != null)
            {
                return;
            }

            _cursorOverlay = new GameObject("SongsOfConquestAccess_AdventureMapCursor");
            _cursorLine = _cursorOverlay.AddComponent<LineRenderer>();
            _cursorLine.useWorldSpace = true;
            _cursorLine.loop = true;
            _cursorLine.widthMultiplier = 0.08f;
            _cursorLine.numCornerVertices = 2;
            _cursorLine.numCapVertices = 2;
            _cursorLine.startColor = Color.yellow;
            _cursorLine.endColor = Color.yellow;
            Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = Color.yellow;
            material.renderQueue = 5000;
            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            _cursorLine.material = material;
        }

        private Vector3[] GetTileOutlinePoints(Vector2Int tile)
        {
            Vector3 center = GetWorldCenter(tile);
            Vector3 east = GetWorldCenter(ClampToMap(new Vector2Int(tile.x + 1, tile.y)));
            Vector3 north = GetWorldCenter(ClampToMap(new Vector2Int(tile.x, tile.y + 1)));
            Vector3 west = GetWorldCenter(ClampToMap(new Vector2Int(tile.x - 1, tile.y)));
            Vector3 south = GetWorldCenter(ClampToMap(new Vector2Int(tile.x, tile.y - 1)));

            Vector3 eastCorner = MidpointOrFallback(center, east, new Vector3(0.75f, 0f, 0f));
            Vector3 northCorner = MidpointOrFallback(center, north, new Vector3(0f, 0f, 0.75f));
            Vector3 westCorner = MidpointOrFallback(center, west, new Vector3(-0.75f, 0f, 0f));
            Vector3 southCorner = MidpointOrFallback(center, south, new Vector3(0f, 0f, -0.75f));
            return new[]
            {
                RaiseAboveGround(eastCorner),
                RaiseAboveGround(northCorner),
                RaiseAboveGround(westCorner),
                RaiseAboveGround(southCorner)
            };
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
                    SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to resolve tile world position: " + exception.Message);
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

        private bool ShouldShowTileDiamond(Vector2Int tile)
        {
            int localTeamId = GetLocalTeamId();
            try
            {
                if (_facade.Commanders.ExistsAtPoint(localTeamId, tile))
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                IMapEntity entity = _facade.MapEntities.GetAt(tile);
                if (entity != null && entity.IsVisibleInGame && entity.Category != MapEntityCategory.Artistic)
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }

            return true;
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to get focused tile tooltip details: " + exception.Message);
                return null;
            }
        }

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            Vector3 world = GetWorldCenter(tile);
            Camera camera = Camera.main;
            if (camera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return camera.WorldToScreenPoint(world);
        }

        private static Vector3 MidpointOrFallback(Vector3 center, Vector3 neighbour, Vector3 fallbackOffset)
        {
            if ((neighbour - center).sqrMagnitude < 0.001f)
            {
                return center + fallbackOffset;
            }

            return Vector3.Lerp(center, neighbour, 0.5f);
        }

        private static Vector3 RaiseAboveGround(Vector3 point)
        {
            point.y += 3f;
            return point;
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read fog readiness: " + exception.Message);
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

            return _facade.Teams.IsInPartnership(localTeamId, commander.TeamId) ? "friendly" : "enemy";
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

        private IMapEntity GetRawMapEntityAt(Vector2Int position, out string diagnostic)
        {
            try
            {
                IMapEntity entity = _facade.MapEntities.GetAt(position);
                diagnostic = "GetAt=" + DescribeMapEntity(entity);
                return entity;
            }
            catch (Exception exception)
            {
                diagnostic = "GetAt threw " + exception.GetType().Name + ": " + exception.Message;
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

        private IMapEntity ResolveHoverParent(IMapEntity entity, List<string> details, string source)
        {
            if (entity == null)
            {
                return null;
            }

            if (entity.CanHover())
            {
                if (details != null)
                {
                    details.Add(source + " hoverable=" + DescribeMapEntity(entity));
                }

                return entity;
            }

            try
            {
                IMapEntity parent = _facade.MapEntities.GetParentEntity(entity);
                if (details != null)
                {
                    details.Add(source + " parent=" + DescribeMapEntity(parent));
                }

                if (parent != null && parent.CanHover())
                {
                    return parent;
                }
            }
            catch (Exception exception)
            {
                if (details != null)
                {
                    details.Add(source + " parent lookup threw " + exception.GetType().Name + ": " + exception.Message);
                }
            }

            return null;
        }

        private void LogTileDiagnostic(AdventureMapTile tile, byte fog, string entityResolutionDiagnostic)
        {
            if (tile == null)
            {
                return;
            }

            string message = "AdventureMapTile diagnostic: position="
                + tile.Position.x
                + ","
                + tile.Position.y
                + "; fog="
                + fog
                + "; visible="
                + tile.IsVisible
                + "; explored="
                + tile.IsExplored
                + "; commander="
                + DescribeCommander(tile.Commander)
                + "; mapEntity="
                + DescribeMapEntity(tile.MapEntity)
                + "; mapEntityName="
                + (tile.MapEntityName ?? string.Empty)
                + "; interactionPoint="
                + tile.IsInteractionPoint
                + "; reachable="
                + tile.IsReachable
                + "; blocked="
                + tile.IsBlocked
                + "; environment="
                + string.Join("|", tile.Environment.ToArray())
                + "; terrain="
                + (tile.Terrain.HasValue ? tile.Terrain.Value.ToString() : string.Empty)
                + "; entityResolution="
                + (entityResolutionDiagnostic ?? string.Empty)
                + "; speech=\""
                + tile.ToSpeech()
                + "\"";
            SoqAccessPlugin.Instance?.LogInfo(message);
        }

        private static string DescribeMapEntity(IMapEntity entity)
        {
            if (entity == null)
            {
                return "<null>";
            }

            return "id="
                + entity.Id
                + ",name="
                + FirstNonEmpty(entity.Name, entity.NameKey)
                + ",category="
                + entity.Category
                + ",position="
                + entity.Position.x
                + ","
                + entity.Position.y
                + ",canHover="
                + entity.CanHover()
                + ",disposed="
                + entity.IsDisposed
                + ",visibleInGame="
                + entity.IsVisibleInGame;
        }

        private static string DescribeCommander(ICommanderState commander)
        {
            if (commander == null)
            {
                return "<null>";
            }

            return "id="
                + commander.Id
                + ",team="
                + commander.TeamId
                + ",position="
                + commander.Position.x
                + ","
                + commander.Position.y
                + ",alive="
                + commander.IsAlive;
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + "," + tile.y;
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
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
            AddEnvironment(tile, GetDecorationName(position, GetLayerValue(position, LayerKind.Decoration)));
            AddEnvironment(tile, GetStandaloneDecorationName(GetLayerValue(position, LayerKind.StandaloneDecoration)));
            AddEnvironment(tile, GetEffectName(GetLayerValue(position, LayerKind.Effect)));
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
                    case LayerKind.Decoration:
                        return _facade.Level.GetDecoration(position);
                    case LayerKind.StandaloneDecoration:
                        return _facade.Level.GetStandaloneDecoration(position);
                    case LayerKind.Effect:
                        return _facade.Level.GetEffect(position);
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
                    return "Dirt road";
                case 2:
                    return "Cobblestone road";
                default:
                    return road > 0 ? "Road" : string.Empty;
            }
        }

        private string GetBridgeName(byte bridge)
        {
            if (bridge == 0)
            {
                return string.Empty;
            }

            BrushSet brush = _cartographyVisualManifest.GetBridgeBrush(bridge);
            return FormatBrushName(brush.name, "Bridge");
        }

        private string GetWaterName(byte water)
        {
            switch (water)
            {
                case 1:
                    return "Shallow water";
                case 2:
                    return "Deep water";
                case 3:
                    return "Water edge";
                default:
                    return water > 0 ? "Water" : string.Empty;
            }
        }

        private string GetDecorationName(Vector2Int position, byte decoration)
        {
            if (decoration == 0)
            {
                return string.Empty;
            }

            switch (decoration)
            {
                case 1:
                    return "Arid trees";
                case 2:
                    return "Trees";
                case 3:
                    return "Mountains";
                case 4:
                    // Generic blocker brushes are editor/pathing markers rather than player-facing objects.
                    // The tile still announces "blocked" from the game's travel-cost result.
                    return string.Empty;
                case 5:
                    return "Light";
                case 6:
                    return "Wall";
                case 7:
                    return "Deforestation";
                case 8:
                    return "Farmland";
                case 9:
                case 10:
                case 11:
                    // Generic blocker brushes are editor/pathing markers rather than player-facing objects.
                    // The tile still announces "blocked" from the game's travel-cost result.
                    return string.Empty;
            }

            try
            {
                byte theme = _facade.Level.GetTheme(position);
                IDecorationVisualManifest manifest = _cartographyVisualManifest.GetTheme(theme).GetDecoration(decoration);
                return FormatBrushName(manifest.SoundKey, "Decoration");
            }
            catch (Exception)
            {
                return "Decoration";
            }
        }

        private string GetStandaloneDecorationName(byte decoration)
        {
            if (decoration == 0)
            {
                return string.Empty;
            }

            BrushSet brush = _cartographyVisualManifest.GetStandaloneDecorationBrush(decoration);
            return FormatBrushName(brush.name, "Decoration");
        }

        private string GetEffectName(byte effect)
        {
            if (effect == 0)
            {
                return string.Empty;
            }

            BrushSet brush = _cartographyVisualManifest.GetEffectBrush(effect);
            return FormatBrushName(brush.name, "Effect");
        }

        private static string FormatBrushName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "none")
            {
                return fallback;
            }

            string normalized = value.Replace('_', ' ').Replace('-', ' ').Replace('/', ' ');
            return FormatEnumName(normalized);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read map entity tooltip details: " + exception.Message);
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
            Water,
            Decoration,
            StandaloneDecoration,
            Effect
        }
    }
}
