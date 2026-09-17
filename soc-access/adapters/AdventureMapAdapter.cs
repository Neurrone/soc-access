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
    public sealed partial class AdventureMapAdapter : IPresent, IDisposable
    {
        private const byte ExploredButNotVisibleFogValue = 128;

        private const ushort ObjectiveBeaconBlueprintId = 50;
        private const ushort FallenBeaconBlueprintId = 158;
        // The names ReactiveAdventureMenuSystem waits under while it shows a story page
        // (RegisterCommandWaiter composes them as "<type name>_<name>").
        private static readonly string MessageTriggerWait =
            typeof(ReactiveAdventureMenuSystem).Name + "_MessageTrigger";
        private static readonly string DialogueTriggerWait =
            typeof(ReactiveAdventureMenuSystem).Name + "_DialogueTrigger";

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
        private readonly ICommandWaiter _commandWaiter;
        private readonly ISceneLoader _sceneLoader;
        private readonly MethodInfo _worldToPointMethod;
        private readonly MethodInfo _pointToWorldMethod;
        private readonly MethodInfo _getTooltipForTilePositionMethod;
        private readonly FieldInfo _runtimeTooltipBehaviorField;
        private readonly FieldInfo _fogHasFinishedLoadingField;
        private readonly FieldInfo _currentInputModuleField;
        // The kinds the game names its map tooltip instruction rows after
        // ("Adventure/TooltipInstruction/<Kind>"), paired with what the mod calls them.
        private static readonly TileInstruction[] MapInstructionKinds =
        {
            TileInstruction.Select,
            TileInstruction.Visit,
            TileInstruction.Trade,
            TileInstruction.Repair,
            TileInstruction.Interact,
            TileInstruction.Pillage,
            TileInstruction.Attack,
            TileInstruction.Pickup,
            TileInstruction.Claim,
            TileInstruction.Teleport
        };

        private readonly HashSet<string> _unknownMapInstructions = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, TileInstruction> _mapInstructionKinds;
        private bool _mapInstructionKindsProbed;
        private readonly FocusedTileOverlay _cursorOverlay = new FocusedTileOverlay("SongsOfConquestAccess_AdventureMapCursor");
        private Vector2Int? _focusedOverlayTile;

        // The selected commander's reachable set for one frame, and the game-read values it is the
        // answer to. See GetReachableMovementCosts.
        private Dictionary<Vector2Int, float> _reachableMovementCosts;
        private int _reachableMovementFrame = -1;
        private int _reachableMovementCommanderId;
        private int _reachableMovementTeamId;
        private Vector2Int _reachableMovementOrigin;
        private float _reachableMovementMovesLeft;

        // The zones of control covering each tile for one frame. See GetZoneOfControlNames.
        private Dictionary<Vector2Int, List<string>> _zoneOfControlNames;
        private int _zoneOfControlFrame = -1;
        private int _zoneOfControlTeamId;

        // The living commanders by tile for one frame. See GetCommanderAtVisiblePoint.
        private Dictionary<Vector2Int, ICommanderState> _commandersByPoint;
        private int _commandersByPointFrame = -1;

        // WHAT THIS ADAPTER ATTACHED TO THE GAME. The map's own event listener is bound to this
        // adventure's facade, selection handler and fog manager, so it lives exactly as long as the
        // adapter over them does and is released in Dispose - never on the screen, which outlives
        // every adventure the game loads (AGENTS.md, Screen Resolution).
        private AdventureMapEventListener _eventListener;

        // What LogFailureOnce has already said, so it says each thing once. Per adapter, so it needs
        // no reset: the adapter dies with the adventure.
        private readonly HashSet<string> _loggedFailures = new HashSet<string>(StringComparer.Ordinal);

        public AdventureMapAdapter(AdventureViewInstaller installer, AdventureMapRevealedRegistry revealedRegistry = null)
            : this(
                installer,
                Reflect.InstallerContainer(installer),
                Reflect.Resolve<IClientAdventureFacade>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ISelectionHandler>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IFogManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IGrid>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ICameraController>(Reflect.InstallerContainer(installer)),
                ResolveByTypeName(Reflect.InstallerContainer(installer), "Lavapotion.Cartography.ICartographyConverter"),
                Reflect.Resolve<IAdventureTooltipManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ILocalizationHandler>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ICartographyVisualManifest>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IHumanAdventureController>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IHumanAdventureControllerFacade>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IInputManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ISystemPopups>(Reflect.InstallerContainer(installer)),
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
            _commandWaiter = Reflect.Resolve<ICommandWaiter>(container);
            _sceneLoader = ProjectContext.HasInstance && ProjectContext.Instance.Container != null
                ? ProjectContext.Instance.Container.TryResolve<ISceneLoader>()
                : null;
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
            _fogHasFinishedLoadingField = fogManager != null
                ? AccessTools.Field(fogManager.GetType(), "_hasFinishedLoading")
                : null;
            _currentInputModuleField = humanAdventureController != null
                ? AccessTools.Field(humanAdventureController.GetType(), "_currentInputModule")
                : null;
            Hud = new AdventureHudAdapter(this, _container);
        }

        /// <summary>Start listening to this adventure's events. Called from the screen's
        /// <c>OnPush</c>, never from <c>Adapt</c>: the adapter exists as soon as the adventure scene's
        /// installer does, while the loading screen is still up and the map entities and fog are
        /// still being filled in, and a listener attached then captures an EMPTY discovery baseline
        /// and announces the whole loaded map as revealed. <paramref name="onMapChanged"/> is what
        /// the screen hangs its tile memo off, so nothing the game changes under a still cursor is
        /// read from the cache.</summary>
        public void AttachEvents(Action onMapChanged)
        {
            if (_eventListener != null)
            {
                return;
            }

            _eventListener = new AdventureMapEventListener(
                _facade,
                _selectionHandler,
                _humanAdventureControllerFacade,
                _localizationHandler,
                _fogManager,
                _revealedRegistry);
            _eventListener.OnMapChanged = onMapChanged;
            _eventListener.Attach();
        }

        /// <summary>Pump the listener's per-frame half, from the screen's <c>OnUpdate</c>.</summary>
        public void UpdateEvents()
        {
            _eventListener?.Update();
        }

        /// <summary>Stop listening, from the screen's <c>OnPop</c>. The next <see cref="AttachEvents"/>
        /// builds a fresh listener with a fresh baseline, so what the game revealed while the map was
        /// off the stack (a story sequence, the loading screen) is not announced afterwards.</summary>
        public void DetachEvents()
        {
            AdventureMapEventListener listener = _eventListener;
            _eventListener = null;
            listener?.Detach();
        }

        public void Dispose()
        {
            DetachEvents();
            ClearFocusedTileOverlay();
        }

        /// <summary>
        /// A guarded game call that threw, reported ONCE per adapter instance and per subject.
        ///
        /// Almost every one of these sits on a path the map walks per tile or per frame - a scanner
        /// snapshot reads thousands of tiles - so a warning per failure would bury the log it exists
        /// to fill. Once per adapter is once per adventure, because the adapter lives exactly as long
        /// as the adventure it wraps, so a new game and a hot reload each say it again.
        /// </summary>
        private void LogFailureOnce(string subject, Exception exception)
        {
            if (!_loggedFailures.Add(subject))
            {
                return;
            }

            SocAccessMod.Instance?.LogWarning("AdventureMapAdapter: " + subject + " threw: " + exception);
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
                LogFailureOnce("reading the map file name for bookmarks", exception);
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
            catch (Exception exception)
            {
                // A binding the adventure does not have is an answer rather than a failure, but it
                // is still worth saying: this runs once per adapter, at construction.
                SocAccessMod.Instance?.LogWarning(
                    "AdventureMapAdapter could not resolve " + typeName + ": " + exception.Message);
                return null;
            }
        }

        public bool IsPresent()
        {
            return GetReadinessDiagnostic(compose: false) == null;
        }

        /// <summary>Whether the game is showing a STORY TRIGGER of the local player's - a message, a
        /// letterbox page or a dialogue - which is the whole of "the map has stood down": the camera
        /// and the keyboard belong to the story until it is dismissed.
        ///
        /// Read off the game rather than remembered.
        /// <c>ReactiveAdventureMenuSystem.HandleTrigger</c> returns before it shows anything unless
        /// the trigger is the local player's (the same test the mod used to repeat on the payload),
        /// and what it does show, it holds the command stream open for through
        /// <c>ICommandWaiter</c> under a name of its own, until the page is dismissed. So the
        /// waiter's identifiers ARE the answer, and losing sight of one is impossible: the game
        /// drops it itself when the story ends.</summary>
        public bool IsStoryTriggerRunning()
        {
            if (_commandWaiter == null || !_commandWaiter.IsWaiting)
            {
                return false;
            }

            foreach (string identifier in _commandWaiter.WaitDebugIdentifiers)
            {
                if (identifier == MessageTriggerWait || identifier == DialogueTriggerWait)
                {
                    return true;
                }
            }

            return false;
        }

        public string GetReadinessDiagnostic()
        {
            return GetReadinessDiagnostic(compose: true);
        }

        /// <param name="compose">Whether the three reasons that name a value may build the string
        /// that says it. <see cref="IsPresent"/> asks this every frame for every screen and only
        /// looks at whether there IS a reason, so it passes false and a map being swapped out costs
        /// no string per frame.</param>
        private string GetReadinessDiagnostic(bool compose)
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
                return compose
                    ? "invalid map size " + _facade.Level.Width + "x" + _facade.Level.Height
                    : "invalid map size";
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

            // THE SCENE IS BEING SWAPPED. The adventure's objects are torn down while its scene is
            // still listed as loaded, and reading the map then reads a fog renderer that has already
            // let its buffers go (2026-09-09: a null reference inside FogRenderer.GetFog on every
            // frame of a save load). The loader saying it is idle ON THE ADVENTURE is the game's own
            // "the map is the thing on screen", and is what the deleted SceneLoader.SetState hook
            // used to report.
            if (_sceneLoader == null)
            {
                return "missing scene loader";
            }

            if (_sceneLoader.State != SceneLoaderState.None)
            {
                return compose ? "scene loader busy: " + _sceneLoader.State : "scene loader busy";
            }

            if (_sceneLoader.Current != SceneType.Adventure)
            {
                return compose
                    ? "scene loader is on " + (_sceneLoader.Current == null ? "nothing" : _sceneLoader.Current.SceneName)
                    : "scene loader is elsewhere";
            }

            return null;
        }

        /// <summary>
        /// Where the tile cursor starts. Asked while the session is coming apart as well as while it
        /// runs - a resync on the way back to the main menu builds the map cursor over a view whose
        /// selection handler never resolved - so every read here is guarded and the answer for "no
        /// session left" is the origin rather than a throw (fixed 2026-09-08: this threw a null
        /// reference on every frame of the quit).
        /// </summary>
        public Vector2Int GetInitialTile()
        {
            try
            {
                if (_selectionHandler == null)
                {
                    return Vector2Int.zero;
                }

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
            catch (Exception exception)
            {
                LogFailureOnce("reading the initial tile", exception);
                return Vector2Int.zero;
            }
        }

        public bool TryGetSelectedWielderPosition(out Vector2Int position)
        {
            ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
            if (selectedCommander != null && selectedCommander.IsAlive && IsWithinMap(selectedCommander.Position))
            {
                position = selectedCommander.Position;
                return true;
            }

            position = Vector2Int.zero;
            return false;
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
            tile.IsVisible = IsPointVisible(fog, clamped);
            tile.IsExplored = IsPointExplored(fog, tile.IsVisible, clamped, localTeamId);
            PopulateZoneOfControl(tile, localTeamId);

            if (!tile.IsExplored)
            {
                return tile;
            }

            tile.Terrain = GetTerrain(clamped);
            tile.Effect = GetEffect(clamped);
            if (IsRoadTerrain(tile.Terrain))
            {
                // GetTerrain answers with the surface terrain whenever there is one, so a tile
                // named as road is exactly a tile whose surface is road. Asking here saves the
                // probe from having to rule the origin out for itself.
                tile.SetRoadDirectionsSource(() => GetRoadDirections(clamped, localTeamId));
            }
            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            tile.IsImpassable = float.IsPositiveInfinity(_facade.Level.GetStaticTravelCost(localTeamId, clamped));
            tile.IsBlocked = !tile.IsImpassable && !_facade.Level.IsValidMoveDestination(localTeamId, clamped);
            float reachableMovementCost;
            if (TryGetReachableMovementCost(selectedCommander, localTeamId, clamped, out reachableMovementCost))
            {
                ApplyReachableMovementCost(tile, reachableMovementCost);
            }

            if (tile.IsVisible)
            {
                ICommanderState commander = GetCommanderAtVisiblePoint(clamped);
                if (commander != null)
                {
                    tile.Commander = CreateCommanderInfo(commander, selectedCommander, localTeamId);
                    float commanderMovementCost;
                    if (commander != selectedCommander
                        && selectedCommander != null
                        && TryGetReachableCommanderDistance(commander, selectedCommander, selectedCommander.TeamId, out commanderMovementCost))
                    {
                        ApplyReachableMovementCost(tile, commanderMovementCost);
                    }
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

            bool canExposeMapEntityIdentity = entity != null && CanExposeKnownMapEntityIdentity(entity, clamped);
            if (entity != null && !canExposeMapEntityIdentity && tile.IsBlocked && !tile.IsImpassable)
            {
                tile.IsBlocked = false;
            }

            if (canExposeMapEntityIdentity)
            {
                tile.MapEntity = entity;
                tile.MapEntityId = entity.Id;
                tile.MapEntityName = GetMapEntityName(entity);
                if (CanExposeMapEntityTooltipDetails(entity))
                {
                    PopulateMapEntityVisited(tile, entity, selectedCommander);
                }

                string mapEntityRelationship = GetMapEntityRelationship(entity, localTeamId);
                tile.MapEntityRelationship = FormatSpatialRelationship(mapEntityRelationship);
                tile.MapEntityRelationshipKind = ScannerRelationship(mapEntityRelationship);
                if (selectedCommander != null && _facade.Level.CanMoveToAndInteract(entity.Id, selectedCommander.Id))
                {
                    float mapEntityMovementCost;
                    if (TryGetReachableMapEntityDistance(entity, selectedCommander, selectedCommander.TeamId, out mapEntityMovementCost))
                    {
                        ApplyReachableMovementCost(tile, mapEntityMovementCost);
                    }
                    else
                    {
                        tile.IsReachable = true;
                    }
                }
            }

            if (tile.IsVisible)
            {
                tile.IsInteractionPoint = _facade.MapEntities.IsInteractionPoint(localTeamId, clamped);
            }

            tile.PathIndicator = BuildPathIndicatorForTile(clamped, selectedCommander, localTeamId);
            tile.EntityCategory = tile.Commander != null
                ? AdventureEntityCategory.Wielder
                : ClassifyMapEntity(tile.MapEntity);

            return tile;
        }

        /// <summary>
        /// The same tests the scanner category builders use, so a tile and a scanner result for
        /// the same entity always agree. Categories the scanner lists elsewhere (obstacles,
        /// objectives, teleports) classify as None.
        /// </summary>
        private static AdventureEntityCategory ClassifyMapEntity(IMapEntity entity)
        {
            if (entity == null)
            {
                return AdventureEntityCategory.None;
            }

            switch (entity.Category)
            {
                case MapEntityCategory.Town:
                case MapEntityCategory.Settlement:
                case MapEntityCategory.BuildSite:
                case MapEntityCategory.Building:
                    return AdventureEntityCategory.Settlement;
                case MapEntityCategory.ResourceGenerator:
                    return AdventureEntityCategory.ResourceDeposit;
            }

            if (entity.HasComponent<IRecruitmentPoolComponent>() || entity.HasComponent<ITroopDwellingComponent>())
            {
                return AdventureEntityCategory.Settlement;
            }

            return IsScannerPickupEntity(entity) ? AdventureEntityCategory.Pickup : AdventureEntityCategory.None;
        }

        private bool TryGetReachableMovementCost(
            ICommanderState selectedCommander,
            int teamId,
            Vector2Int target,
            out float cost)
        {
            cost = 0f;
            if (selectedCommander == null
                || !selectedCommander.IsAlive
                || _facade == null
                || _facade.Level == null
                || teamId < 0
                || !IsWithinMap(target))
            {
                return false;
            }

            Dictionary<Vector2Int, float> reachable = GetReachableMovementCosts(selectedCommander, teamId);
            return reachable != null && reachable.TryGetValue(target, out cost);
        }

        /// <summary>
        /// The travel cost of every tile the selected commander can still reach this turn, as the
        /// game's own whole-map Dijkstra answers it, indexed by tile.
        ///
        /// <c>PointsWithinReach</c> reads exactly three things - the team, the commander's position
        /// and its movement left - so those three, plus the commander's id to tell one commander
        /// from another standing on the same tile with the same moves, are the key, and
        /// <c>Time.frameCount</c> closes it: within one frame nothing the game owns has moved. Every
        /// part of the key is read from the game on the call, so a step, a turn, a selection change
        /// and a hot reload all miss on their own with no hook to tell them to (AGENTS.md, "Screen
        /// Resolution"). Without this the Dijkstra ran once per tile read, and a scanner snapshot or
        /// a skip-navigator sweep reads thousands.
        /// </summary>
        private Dictionary<Vector2Int, float> GetReachableMovementCosts(ICommanderState selectedCommander, int teamId)
        {
            int frame = Time.frameCount;
            Vector2Int origin = selectedCommander.Position;
            float movesLeft = selectedCommander.MovesLeft;
            int commanderId = selectedCommander.Id;
            if (_reachableMovementCosts != null
                && _reachableMovementFrame == frame
                && _reachableMovementCommanderId == commanderId
                && _reachableMovementTeamId == teamId
                && _reachableMovementOrigin == origin
                && _reachableMovementMovesLeft == movesLeft)
            {
                return _reachableMovementCosts;
            }

            PathNode[] reachable = _facade.Level.PointsWithinReach(
                teamId,
                origin,
                movesLeft,
                (PathfinderCacheType)0);
            Dictionary<Vector2Int, float> costs = new Dictionary<Vector2Int, float>();
            if (reachable != null)
            {
                for (int i = 0; i < reachable.Length; i++)
                {
                    PathNode node = reachable[i];
                    if (float.IsInfinity(node.travelCost))
                    {
                        continue;
                    }

                    Vector2Int point = new Vector2Int(node.point.x, node.point.y);
                    if (!costs.ContainsKey(point))
                    {
                        // First finite cost for a tile wins, as the linear scan this replaces did.
                        costs.Add(point, node.travelCost);
                    }
                }
            }

            _reachableMovementCosts = costs;
            _reachableMovementFrame = frame;
            _reachableMovementCommanderId = commanderId;
            _reachableMovementTeamId = teamId;
            _reachableMovementOrigin = origin;
            _reachableMovementMovesLeft = movesLeft;
            return costs;
        }

        public static bool TryGetReachableMovementCost(PathNode[] reachable, Vector2Int target, out float cost)
        {
            cost = 0f;
            if (reachable == null)
            {
                return false;
            }

            for (int i = 0; i < reachable.Length; i++)
            {
                PathNode node = reachable[i];
                if (node.point.x == target.x
                    && node.point.y == target.y
                    && !float.IsInfinity(node.travelCost))
                {
                    cost = node.travelCost;
                    return true;
                }
            }

            return false;
        }

        public static void ApplyReachableMovementCost(AdventureMapTile tile, float cost)
        {
            if (tile == null || float.IsInfinity(cost) || float.IsNaN(cost))
            {
                return;
            }

            tile.IsReachable = true;
            if (!tile.ReachableMovementCost.HasValue)
            {
                tile.ReachableMovementCost = cost;
            }
        }

        public Tooltip GetTooltip(Vector2Int tile)
        {
            if (_tooltipManager == null || !ShouldShowFocusedTileTooltip(tile))
            {
                return null;
            }

            Vector2Int detailsTile = GetTooltipDetailsTile(tile);
            IDetails details = GetTooltipDetailsForTile(detailsTile);
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
            TileInstruction primary;
            TileInstruction secondary;
            TakeMapTooltipInstructions(captured.InstructionRows, textLines, out primary, out secondary);
            return new Tooltip(
                () => textLines,
                new VisualTooltipMetadata(tooltipable, GetScreenPoint(tile), details),
                primary,
                secondary,
                () => NativeTooltipUtility.IsLong(details));
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
                string name = GameText.Get(_localizationHandler, artifact.NameKey, string.Empty);
                string formattedName = ArtifactSpeechFormatter.FormatName(_localizationHandler, name, artifact.PowerLevelColor);
                if (string.IsNullOrWhiteSpace(name) || name == formattedName)
                {
                    continue;
                }

                ReplaceFirstTooltipLine(textLines, name, formattedName);
            }
        }

        private static bool ReplaceFirstTooltipLine(List<string> textLines, string oldText, string newText)
        {
            string normalizedOldText = SpokenLines.Clean(oldText);
            for (int i = 0; i < textLines.Count; i++)
            {
                if (SpokenLines.Clean(textLines[i]).Equals(normalizedOldText, StringComparison.OrdinalIgnoreCase))
                {
                    textLines[i] = newText;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Take the native click-instruction rows out of the tooltip text and report what each one
        /// said the click would DO. The row is stripped either way - it describes a mouse gesture the
        /// keyboard player is not making - and the screen says the kind as a usage hint on the key
        /// that performs it.
        /// </summary>
        private void TakeMapTooltipInstructions(
            IReadOnlyList<TooltipInstructionRow> instructionRows,
            List<string> textLines,
            out TileInstruction primary,
            out TileInstruction secondary)
        {
            primary = TileInstruction.None;
            secondary = TileInstruction.None;
            if (instructionRows == null || instructionRows.Count == 0)
            {
                return;
            }

            for (int i = 0; i < instructionRows.Count; i++)
            {
                TooltipInstructionRow row = instructionRows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.Text))
                {
                    continue;
                }

                if (IsPrimaryMapInstruction(row.InputType))
                {
                    TooltipLines.Remove(textLines, row.Text);
                    primary = ClassifyMapInstruction(row.Text);
                }
                else if (IsSecondaryMapInstruction(row.InputType))
                {
                    TooltipLines.Remove(textLines, row.Text);
                    secondary = ClassifyMapInstruction(row.Text);
                }
            }
        }

        /// <summary>The kind a row's text names, matched against the game's own
        /// "Adventure/TooltipInstruction/&lt;Kind&gt;" strings, which are resolved once per adapter.
        /// A wording the table does not hold is logged once so a new game kind shows up in the log
        /// rather than vanishing.</summary>
        private TileInstruction ClassifyMapInstruction(string text)
        {
            EnsureMapInstructionKinds();
            TileInstruction kind;
            if (_mapInstructionKinds != null
                && _mapInstructionKinds.TryGetValue(text.Trim(), out kind))
            {
                return kind;
            }

            if (_unknownMapInstructions.Add(text))
            {
                SocAccessMod.Instance?.LogWarning(
                    "AdventureMapAdapter saw an unrecognized tooltip instruction row: " + text);
            }

            return TileInstruction.None;
        }

        private void EnsureMapInstructionKinds()
        {
            if (_mapInstructionKindsProbed)
            {
                return;
            }

            _mapInstructionKindsProbed = true;
            if (_localizationHandler == null)
            {
                return;
            }

            Dictionary<string, TileInstruction> kinds =
                new Dictionary<string, TileInstruction>(StringComparer.Ordinal);
            for (int i = 0; i < MapInstructionKinds.Length; i++)
            {
                TileInstruction kind = MapInstructionKinds[i];
                string text = GameText.Get(_localizationHandler, "Adventure/TooltipInstruction/" + kind, string.Empty);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    kinds[text.Trim()] = kind;
                }
            }

            _mapInstructionKinds = kinds;
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

        private bool ShouldShowFocusedTileTooltip(Vector2Int tile)
        {
            int localTeamId = GetLocalTeamId();
            // GetAt cannot throw; see GetRawMapEntityAt.
            IMapEntity entity = _facade.MapEntities.GetAt(tile);
            if (entity != null)
            {
                return CanExposeMapEntityTooltipDetails(entity);
            }

            try
            {
                return _facade.Commanders.ExistsAtPoint(localTeamId, tile);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading whether a commander stands on a tile", exception);
                return false;
            }
        }

        private Vector2Int GetTooltipDetailsTile(Vector2Int focusedTile)
        {
            try
            {
                IMapEntity entity = _facade.MapEntities.GetAt(focusedTile);
                Vector2Int detailsTile;
                if (entity != null
                    && AdventureMapVisibility.TryGetActivelyVisibleMapEntityIdentityTile(
                        _facade,
                        _fogManager,
                        entity,
                        out detailsTile))
                {
                    return detailsTile;
                }
            }
            catch (Exception exception)
            {
                LogFailureOnce("finding the tile a map entity's tooltip belongs to", exception);
            }

            return focusedTile;
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
                LogFailureOnce("reading a tile tooltip details", exception);
                return null;
            }
        }

        public Vector2Int GetMapSize()
        {
            if (_facade == null || _facade.Level == null)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int(_facade.Level.Width, _facade.Level.Height);
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
                LogFailureOnce("reading fog readiness", exception);
                return false;
            }
        }

        /// <summary>
        /// The neighbouring tiles a road carries on into, given a tile already known to be road.
        /// Bridges count as road so a route does not appear to stop dead at every water crossing.
        /// </summary>
        private IReadOnlyList<ScannerDirection> GetRoadDirections(Vector2Int position, int localTeamId)
        {
            // The nine tiles this looks at each ask the fog and the level about a point, which
            // is only answerable once the fog has finished loading. Mid-load and teardown the
            // road simply carries on nowhere rather than faulting a whole tile announcement.
            if (!IsFogReady())
            {
                return RoadConnections.None;
            }

            return RoadConnections.Compute(position, tile => IsRoadTile(tile, localTeamId));
        }

        /// <summary>
        /// Whether this tile is one the player has seen and would hear named as a road. Asking
        /// through the surface terrain rather than the road byte keeps the directions honest:
        /// a tile whose decoration hides the road under it is named for the decoration, so it
        /// must not be offered as somewhere the road carries on to either.
        /// </summary>
        private bool IsRoadTile(Vector2Int position, int localTeamId)
        {
            return IsWithinMap(position)
                && IsPointExplored(position, localTeamId)
                && IsRoadTerrain(GetSurfaceTerrain(position));
        }

        private static bool IsRoadTerrain(AdventureTerrainKind terrain)
        {
            return terrain == AdventureTerrainKind.Road
                || terrain == AdventureTerrainKind.DirtRoad
                || terrain == AdventureTerrainKind.CobblestoneRoad
                || terrain == AdventureTerrainKind.Bridge;
        }

        private bool IsPointVisible(byte fog, Vector2Int position)
        {
            if (fog == byte.MaxValue)
            {
                return true;
            }

            return _fogManager.IsVisible(position);
        }

        private bool IsPointExplored(byte fog, bool visible, Vector2Int position, int localTeamId)
        {
            if (visible || fog == ExploredButNotVisibleFogValue)
            {
                return true;
            }

            return _facade.Level.GetIsPointExplored(localTeamId, position);
        }

        /// <summary>
        /// The same exploration rule GetTile applies, for callers that only need the answer.
        /// Roads leading into unexplored ground stop here, so they read as though they ended.
        /// </summary>
        private bool IsPointExplored(Vector2Int position, int localTeamId)
        {
            byte fog = GetFog(position);
            return IsPointExplored(fog, IsPointVisible(fog, position), position, localTeamId);
        }

        private byte GetFog(Vector2Int position)
        {
            try
            {
                return _fogManager.GetFog(position.x, position.y);
            }
            catch (Exception exception)
            {
                // Reachable while the adventure scene is torn down: the fog renderer lets its
                // buffers go before the scene is unloaded. IsPresent now gates on the scene loader
                // being idle, so this should stay silent; once is enough to say that it did not.
                LogFailureOnce("reading the fog over a tile", exception);
                return 0;
            }
        }

        private bool CanExposeKnownMapEntityIdentity(IMapEntity entity, Vector2Int position)
        {
            return AdventureMapVisibility.IsKnownMapEntityIdentityTile(_facade, _fogManager, entity, position);
        }

        private bool CanExposeMapEntityTooltipDetails(IMapEntity entity)
        {
            return AdventureMapVisibility.HasAnyActivelyVisibleMapEntityTile(_fogManager, entity);
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
            string relationship = GetCommanderRelationship(commander, localTeamId);
            AdventureMapTile.CommanderInfo info = new AdventureMapTile.CommanderInfo
            {
                Id = commander != null ? commander.Id : -1,
                Raw = commander,
                Name = AdventureMapEntityLabel.GetCommanderName(_facade, commander),
                IsSelected = ReferenceEquals(commander, selectedCommander),
                Relationship = FormatSpatialRelationship(relationship),
                RelationshipKind = ScannerRelationship(relationship),
                IsOwnedByLocalTeam = commander != null && commander.TeamId == localTeamId,
                MovementLabel = GameText.Get(_localizationHandler, "Commanders/Tooltip/Movement", string.Empty)
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
                LogFailureOnce("reading a commander destination path", exception);
            }
        }

        /// <summary>
        /// The commander standing on a tile, as the map speaks it: the first living commander the
        /// facade lists there, whatever team it is on.
        ///
        /// Deliberately not <c>_facade.Commanders.GetAtPoint</c>, which is indexed but answers a
        /// different question - it takes a team, drops commanders whose internal state is Hidden and
        /// picks a "best" of several - so switching to it would change what the map says. Instead
        /// the whole list is walked once per frame and indexed by tile, in the facade's own order
        /// with the first entry per tile kept, which is exactly what the per-tile walk returned.
        /// </summary>
        private ICommanderState GetCommanderAtVisiblePoint(Vector2Int position)
        {
            int frame = Time.frameCount;
            if (_commandersByPoint == null || _commandersByPointFrame != frame)
            {
                Dictionary<Vector2Int, ICommanderState> byPoint = new Dictionary<Vector2Int, ICommanderState>();
                IEnumerable<ICommanderState> commanders = _facade.Commanders.All;
                if (commanders != null)
                {
                    foreach (ICommanderState commander in commanders)
                    {
                        if (commander != null && commander.IsAlive && !byPoint.ContainsKey(commander.Position))
                        {
                            byPoint.Add(commander.Position, commander);
                        }
                    }
                }

                _commandersByPoint = byPoint;
                _commandersByPointFrame = frame;
            }

            ICommanderState found;
            return _commandersByPoint.TryGetValue(position, out found) ? found : null;
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

        // No guard: MapEntities.GetAt asks the entity cache for the id at the point
        // (AbstractMapEntityMapCache.GetIdAt:323 answers -1 for a point outside the map) and then
        // looks that id up in the same dictionary, so an off-map point answers null, not an throw.
        private IMapEntity GetRawMapEntityAt(Vector2Int position)
        {
            return _facade.MapEntities.GetAt(position);
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + "," + tile.y;
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
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

        /// <summary>Whether the selected commander has already visited what stands on this tile.
        /// </summary>
        private void PopulateMapEntityVisited(AdventureMapTile tile, IMapEntity entity, ICommanderState selectedCommander)
        {
            if (tile == null || entity == null || selectedCommander == null)
            {
                return;
            }

            try
            {
                tile.MapEntityVisited = entity.DidVisit(selectedCommander.Id);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading whether a map entity was visited", exception);
            }
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            return AdventureMapEntityLabel.GetMapEntityName(_facade, _selectionHandler, _localizationHandler, entity);
        }

    }
}
