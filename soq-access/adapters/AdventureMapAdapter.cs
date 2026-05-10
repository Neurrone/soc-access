using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using StoryMapSuppression = SongsOfConquestAccess.StoryMapSuppression;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapAdapter
    {
        private const byte ExploredButNotVisibleFogValue = 128;
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
        private readonly IMapEntityMiniMenu _mapEntityMiniMenu;
        private readonly MethodInfo _worldToPointMethod;
        private readonly MethodInfo _pointToWorldMethod;
        private readonly MethodInfo _getTooltipForTilePositionMethod;
        private readonly FieldInfo _runtimeTooltipBehaviorField;
        private readonly FieldInfo _innerTooltipManagerField;
        private readonly FieldInfo _gamepadTooltipHandleField;
        private readonly FieldInfo _miniMenuIsVisibleField;
        private readonly FieldInfo _fogHasFinishedLoadingField;
        private readonly FieldInfo _currentInputModuleField;
        private GameObject _cursorOverlay;
        private RectTransform[] _cursorOverlaySegments;
        private Vector2Int? _focusedOverlayTile;

        public AdventureMapAdapter(AdventureViewInstaller installer)
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
                Resolve<IMapEntityMiniMenu>(GetContainer(installer)))
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
            IMapEntityMiniMenu mapEntityMiniMenu = null)
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
            _mapEntityMiniMenu = mapEntityMiniMenu ?? Resolve<IMapEntityMiniMenu>(container);
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
            Type miniMenuType = _mapEntityMiniMenu != null ? _mapEntityMiniMenu.GetType() : null;
            _miniMenuIsVisibleField = miniMenuType != null
                ? AccessTools.Field(miniMenuType, "_isVisible")
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

        public IHumanAdventureControllerFacade HumanAdventureControllerFacade
        {
            get { return _humanAdventureControllerFacade; }
        }

        public ILocalizationHandler LocalizationHandler
        {
            get { return _localizationHandler; }
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

            if (StoryMapSuppression.IsActive)
            {
                return "story interaction active";
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
            return GetTile(position, logDiagnostic: true);
        }

        private AdventureMapTile GetTile(Vector2Int position, bool logDiagnostic)
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
                if (logDiagnostic)
                {
                    LogTileDiagnostic(tile, fog, "skipped entity lookup because tile is unexplored");
                }

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

            if (logDiagnostic)
            {
                LogTileDiagnostic(tile, fog, entityResolutionDiagnostic);
            }

            return tile;
        }

        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            Dictionary<Vector2Int, AdventureMapTile> tileCache = new Dictionary<Vector2Int, AdventureMapTile>();
            int localTeamId = GetLocalTeamId();
            AddWielderScannerResults(snapshot, tileCache);
            AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Town, MapEntityCategory.Settlement);
            AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Building);
            AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.BuildSite);
            AddTroopSourceScannerResults(snapshot, localTeamId, tileCache);
            AddPickupScannerResults(snapshot, tileCache);
            AddArtifactMarketScannerResults(snapshot, tileCache);
            AddObjectiveScannerResults(snapshot, tileCache);
            AddTeleportScannerResults(snapshot, tileCache);
            AddObstacleScannerResults(snapshot, localTeamId, origin, tileCache);
            AddAdventureTerrainScannerResults(snapshot, origin, tileCache);
            snapshot.SortByDistance(origin);
            return snapshot;
        }

        public bool ValidateScannerResult(ScannerResult result)
        {
            if (result == null || !IsWithinMap(result.Position))
            {
                return false;
            }

            if (result.Kind == ScannerResultKind.CommanderZoneOfControl)
            {
                return ValidateCommanderZoneOfControlResult(result);
            }

            AdventureMapTile tile = GetTile(result.Position);
            if (tile == null || !tile.IsExplored)
            {
                return false;
            }

            if (result.StableReference is int stableId)
            {
                if (tile.Commander != null && tile.Commander.Id == stableId)
                {
                    return true;
                }

                return tile.MapEntity != null && tile.MapEntity.Id == stableId;
            }

            return true;
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

                string name = FirstNonEmpty(tile.CommanderName, "wielder");
                snapshot.Add("Wielders", "All",
                    new ScannerResult(name, commander.Position) { StableReference = commander.Id });
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
                string relationship = ToTitleCase(GetMapEntityRelationship(entity, localTeamId));
                ScannerResult result = new ScannerResult(name, entity.Position) { NotVisible = notVisible, StableReference = entity.Id };

                AddStructuralMapEntityResult(snapshot, entity, relationship, result);
                AddTroopSourceResult(snapshot, entity, relationship, result);
                AddPickupResult(snapshot, entity, result);
                AddSpecialMapEntityResult(snapshot, entity, result);
            }
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

                string relationship = ToTitleCase(GetMapEntityRelationship(entity, localTeamId));
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
                    AddTroopSourceResult(snapshot, entity, ToTitleCase(GetMapEntityRelationship(entity, localTeamId)), result);
                }
            });
        }

        private void AddPickupScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ScannerCategory pickups = snapshot.GetOrAddCategory("Pickups");
            pickups.GetOrAddSubcategory("All");
            pickups.GetOrAddSubcategory("Unvisited");

            ForEachScannerEntity(tileCache, entity =>
            {
                AdventureMapTile tile = GetScannerTile(tileCache, entity.Position);
                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddPickupResult(snapshot, entity, result);
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
                    snapshot.Add("Artifact markets", "All", result);
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
                    snapshot.Add("Objectives", "All", result);
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
                    snapshot.Add("Teleport", "All", result);
                }
            });
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
                    snapshot.Add("Obstacles", "All", result);
                }
                else if (entity.HasComponent<IMagicGateCommonComponent>() || entity.HasComponent<IUnlockWithArtifactComponent>())
                {
                    snapshot.Add("Obstacles", "All", result);
                }
                else if (entity.Category == MapEntityCategory.Obstacle)
                {
                    snapshot.Add("Obstacles", "All", result);
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
                string name = FirstNonEmpty(GetCommanderName(commander), "commander");
                string tileWord = points.Count == 1 ? " tile" : " tiles";
                ScannerResult result = new ScannerResult(points.Count + tileWord + " within " + FormatPossessive(name) + " zone of control", representative)
                {
                    Kind = ScannerResultKind.CommanderZoneOfControl,
                    StableReference = commander.Id
                };
                result.Points.AddRange(points);
                snapshot.Add("Obstacles", "All", result);
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

                string name = FirstNonEmpty(GetCommanderName(commander), "commander");
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
            return new ScannerResult(name, entity.Position) { NotVisible = !tile.IsVisible, StableReference = entity.Id };
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
                    snapshot.Add("Settlements", relationship, CloneResult(result));
                    break;
                case MapEntityCategory.Building:
                    snapshot.Add("Buildings", relationship, CloneResult(result));
                    break;
                case MapEntityCategory.BuildSite:
                    snapshot.Add("Build sites", relationship, CloneResult(result));
                    break;
            }
        }

        private void AddTroopSourceResult(ScannerSnapshot snapshot, IMapEntity entity, string relationship, ScannerResult result)
        {
            if (entity.HasComponent<IRecruitmentPoolComponent>() || entity.HasComponent<ITroopDwellingComponent>())
            {
                snapshot.Add("Troop sources", relationship, CloneResult(result));
            }
        }

        private void AddPickupResult(ScannerSnapshot snapshot, IMapEntity entity, ScannerResult result)
        {
            MapEntityPreVisitDetails.PreVisitHint hint = GetPreVisitHint(entity);
            string subcategory = null;
            switch (hint)
            {
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfKnowledge:
                    subcategory = "Knowledge";
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfPower:
                    subcategory = "Power";
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfRiches:
                    subcategory = "Riches";
                    break;
            }

            if (subcategory == null)
            {
                return;
            }

            snapshot.Add("Pickups", "All", CloneResult(result));
            if (IsUnvisited(entity))
            {
                snapshot.Add("Pickups", "Unvisited", CloneResult(result));
            }

            snapshot.Add("Pickups", subcategory, CloneResult(result));
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
                snapshot.Add("Artifact markets", "All", CloneResult(result));
            }

            if (entity.Category == MapEntityCategory.Objective || entity.Category == MapEntityCategory.Story)
            {
                snapshot.Add("Objectives", "All", CloneResult(result));
            }

            if (entity.HasComponent<ITeleportComponent>() || entity.HasComponent<ITownPortalComponent>() || entity.HasComponent<ITownPortalBuildingComponent>())
            {
                snapshot.Add("Teleport", "All", CloneResult(result));
            }

            if (entity.Category == MapEntityCategory.Hostile)
            {
                snapshot.Add("Obstacles", "All", CloneResult(result));
            }
            else if (entity.HasComponent<IMagicGateCommonComponent>() || entity.HasComponent<IUnlockWithArtifactComponent>())
            {
                snapshot.Add("Obstacles", "All", CloneResult(result));
            }
            else if (entity.Category == MapEntityCategory.Obstacle)
            {
                snapshot.Add("Obstacles", "All", CloneResult(result));
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
                return GetTile(position, logDiagnostic: false);
            }

            Vector2Int clamped = ClampToMap(position);
            AdventureMapTile tile;
            if (!tileCache.TryGetValue(clamped, out tile))
            {
                tile = GetTile(clamped, logDiagnostic: false);
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

            TerrainScanCell[,] terrain = BuildTerrainScan(tileCache);
            AddTerrainGroups(snapshot, terrain, "Roads", "road", origin, cell => cell.Road);
            AddTerrainGroups(snapshot, terrain, "Bridges", "bridge", origin, cell => cell.Bridge);
            AddTerrainGroups(snapshot, terrain, "Water", "water", origin, cell => cell.Water);
            AddTerrainGroups(snapshot, terrain, "Impassable", "impassable", origin, cell => cell.Impassable);
        }

        private TerrainScanCell[,] BuildTerrainScan(Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            TerrainScanCell[,] terrain = new TerrainScanCell[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    AdventureMapTile tile = GetScannerTile(tileCache, new Vector2Int(x, y));
                    terrain[x, y] = new TerrainScanCell
                    {
                        Explored = tile != null && tile.IsExplored,
                        Road = tile != null && tile.IsExplored && HasEnvironment(tile, "road"),
                        Bridge = tile != null && tile.IsExplored && HasEnvironment(tile, "bridge"),
                        Water = tile != null && tile.IsExplored && HasEnvironment(tile, "water"),
                        Impassable = tile != null && tile.IsExplored && tile.IsBlocked
                    };
                }
            }

            return terrain;
        }

        private void AddTerrainGroups(ScannerSnapshot snapshot, TerrainScanCell[,] terrain, string subcategory, string label, Vector2Int origin, Func<TerrainScanCell, bool> predicate)
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
                    ScannerResult result = new ScannerResult(group.Count + " " + label, representative)
                    {
                        Kind = ScannerResultKind.TerrainGroup
                    };
                    result.Points.AddRange(group);
                    snapshot.Add("Terrain", subcategory, result);
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
            ScannerResult clone = new ScannerResult(result.Label, result.Position)
            {
                NotVisible = result.NotVisible,
                StableReference = result.StableReference,
                Kind = result.Kind
            };
            clone.Points.AddRange(result.Points);
            return clone;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Neutral";
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public Tooltip GetTooltip(Vector2Int tile)
        {
            if (IsSelectedMapEntityMiniMenuOpen())
            {
                HideFocusedTileTooltip();
                return null;
            }

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
            List<TooltipAction> actions = BuildMapTooltipActions(tile, captured.InstructionRows, textLines);
            return new Tooltip(
                () => textLines,
                new VisualTooltipMetadata(tooltipable, GetScreenPoint(tile), details),
                actions);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to keep focused tile in view: " + exception.Message);
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
                SoqAccessPlugin.Instance?.StartCoroutine(RefreshFocusedTileOverlayAfterCameraMove(tile));
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to move camera to focused tile: " + exception.Message);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to clear focused tile overlay: " + exception.Message);
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
                HideFocusedTileTooltipIfSelectedMiniMenuOpens();
                return true;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter primary action failed: " + exception);
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
                        ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
                        bool hadDestination = selectedCommander != null && selectedCommander.Destination.HasDestination;
                        Vector2Int previousDestination = hadDestination ? selectedCommander.Destination.Destination : Vector2Int.zero;
                        HumanAdventureController.State previousState = _humanAdventureControllerFacade.StateMachine.CurrentStateType;

                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputStart");
                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputEnded");

                        LogNativeSecondaryDiagnostic(tilePosition, selectedCommander, previousState, hadDestination, previousDestination);
                    });
                return true;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter secondary action failed: " + exception);
                PublishDenied(tilePosition, "Could not perform secondary action.");
                return true;
            }
        }

        public bool TrySelectNextWielder()
        {
            if (_humanAdventureController == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not select next wielder: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextIdleCommander();
                return true;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter next wielder action failed: " + exception);
                return true;
            }
        }

        public bool TrySelectNextSettlement()
        {
            if (_humanAdventureController == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not select next settlement: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextTown();
                return true;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter next settlement action failed: " + exception);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not invoke native " + inputName + " action because current input module was null");
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
                LogNativeHoverDiagnostic(tilePosition, screenInputOverride.ScreenPosition);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            if (_cameraController == null || _cameraController.Camera == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because the adventure camera was unavailable");
                return false;
            }

            object response = ResolveWritableScreenInputResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
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
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not target tile " + FormatTile(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
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

        private void LogNativeHoverDiagnostic(Vector2Int tilePosition, Vector2 screenPosition)
        {
            SoqAccessPlugin.Instance?.LogInfo(
                "AdventureMap native hover updated from synthetic screen input: tile="
                + FormatTile(tilePosition)
                + "; screenPosition="
                + screenPosition
                + "; currentDestination="
                + FormatTile(_humanAdventureControllerFacade.CurrentDestinationTile));
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
                    SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because required writable properties were missing on " + responseType.FullName);
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

        private void LogNativeSecondaryDiagnostic(
            Vector2Int tilePosition,
            ICommanderState selectedCommander,
            HumanAdventureController.State previousState,
            bool hadDestination,
            Vector2Int previousDestination)
        {
            string previousDestinationText = hadDestination ? FormatTile(previousDestination) : "<none>";
            string currentDestinationText = selectedCommander != null && selectedCommander.Destination.HasDestination
                ? FormatTile(selectedCommander.Destination.Destination)
                : "<none>";
            SoqAccessPlugin.Instance?.LogInfo(
                "AdventureMap native secondary result: tile="
                + FormatTile(tilePosition)
                + "; previousState="
                + previousState
                + "; currentState="
                + _humanAdventureControllerFacade.StateMachine.CurrentStateType
                + "; previousDestination="
                + previousDestinationText
                + "; currentDestination="
                + currentDestinationText
                + "; selectedCommander="
                + DescribeCommanderForDiagnostics(selectedCommander));
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
                    SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to resolve camera center tile: " + exception.Message);
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

        private bool ShouldShowFocusedTileTooltip(Vector2Int tile)
        {
            if (IsSelectedMapEntityMiniMenuOpen())
            {
                return false;
            }

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

        private bool IsSelectedMapEntityMiniMenuOpen()
        {
            if (_selectionHandler == null || _selectionHandler.SelectedMapEntity == null || _miniMenuIsVisibleField == null)
            {
                return false;
            }

            try
            {
                object value = _miniMenuIsVisibleField.GetValue(_mapEntityMiniMenu);
                return value is bool && (bool)value;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("AdventureMapAdapter failed to read map entity mini menu visibility: " + exception.Message);
                return false;
            }
        }

        private void HideFocusedTileTooltip()
        {
            _tooltipManager?.HideTileTooltip();
            NativeTooltipUtility.HideTooltip();
        }

        private void HideFocusedTileTooltipIfSelectedMiniMenuOpens()
        {
            if (IsSelectedMapEntityMiniMenuOpen())
            {
                HideFocusedTileTooltip();
            }

            SoqAccessPlugin.Instance?.StartCoroutine(HideFocusedTileTooltipIfSelectedMiniMenuOpensNextFrame());
        }

        private IEnumerator HideFocusedTileTooltipIfSelectedMiniMenuOpensNextFrame()
        {
            yield return null;

            if (IsSelectedMapEntityMiniMenuOpen())
            {
                HideFocusedTileTooltip();
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

            if (_facade.Teams != null && commander.TeamId == _facade.Teams.GetNeutralTeamId)
            {
                return "neutral";
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

            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            ICommanderState nativeCommanderAtPoint = null;
            int localTeamId = GetLocalTeamId();
            if (tile.IsVisible && localTeamId != -1)
            {
                try
                {
                    nativeCommanderAtPoint = _facade.Commanders.GetAtPoint(localTeamId, tile.Position);
                }
                catch (Exception exception)
                {
                    SoqAccessPlugin.Instance?.LogWarning("AdventureMapTile diagnostic failed native GetAtPoint lookup: " + exception.Message);
                }
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
                + "; selectedCommander="
                + DescribeCommanderForDiagnostics(selectedCommander)
                + "; selectedCommanderState="
                + DescribeCommanderStateForDiagnostics(selectedCommander)
                + "; commander="
                + DescribeCommander(tile.Commander)
                + "; nativeCommanderAtPoint="
                + DescribeCommander(nativeCommanderAtPoint)
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
                + new AdventureMapTileSpeechFormatter().DescribeTile(tile)
                + "\"";
            SoqAccessPlugin.Instance?.LogInfo(message);
        }

        private static string DescribeCommanderForDiagnostics(ICommanderState commander)
        {
            return DescribeCommander(commander);
        }

        private static string DescribeCommanderStateForDiagnostics(ICommanderState commander)
        {
            if (commander == null)
            {
                return "<null>";
            }

            string destination = "<none>";
            if (commander.Destination != null && commander.Destination.HasDestination)
            {
                destination = FormatTile(commander.Destination.Destination);
            }

            return "internalState="
                + commander.InternalState
                + ",storedBuildingId="
                + commander.StoredBuildingId
                + ",movesLeft="
                + commander.MovesLeft
                + ",destination="
                + destination;
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
            if (string.IsNullOrWhiteSpace(name))
            {
                return "commander's";
            }

            return name.EndsWith("s") || name.EndsWith("S") ? name + "'" : name + "'s";
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
