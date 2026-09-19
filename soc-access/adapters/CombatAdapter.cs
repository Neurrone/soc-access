using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Lavapotion.Pathfinding;
using SongsOfConquest;
using SongsOfConquest.Client;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquest.Common.Spells;
using SongsOfConquest.Server.Adventure.Map.Provider;
using SongsOfConquest.Server.Battle;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquest.Utilities;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    public enum CombatTargetingMode
    {
        None,
        Spell,
        Ability
    }

    public enum CombatTroopSideFilter
    {
        CurrentPlayer,
        Enemy
    }

    /// <summary>A stack on the battlefield as the facts a spoken row is made of: what the game calls
    /// it, how many are in it, the health it has left of its maximum, and whether it is an enemy or
    /// the one acting. <see cref="UI.CombatTroopText"/> does the wording.</summary>
    public struct CombatTroopFacts
    {
        public CombatTroopFacts(
            string name,
            int size,
            int currentHealth,
            int maxHealth,
            bool isEnemy,
            bool isActing,
            bool isReloading = false,
            IReadOnlyList<string> restrictionNames = null,
            IReadOnlyList<string> effectNames = null,
            string activeAbilityName = null)
        {
            Name = name ?? string.Empty;
            Size = size;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsEnemy = isEnemy;
            IsActing = isActing;
            IsReloading = isReloading;
            RestrictionNames = restrictionNames ?? EmptyNames;
            EffectNames = effectNames ?? EmptyNames;
            ActiveAbilityName = activeAbilityName ?? string.Empty;
        }

        private static readonly string[] EmptyNames = new string[0];

        /// <summary>The game's own name for the stack at this size, cleaned, and empty where the
        /// game gives none: the general word a blank name falls back to is wording and belongs to
        /// whoever composes the row.</summary>
        public string Name { get; private set; }

        public int Size { get; private set; }

        public int CurrentHealth { get; private set; }

        public int MaxHealth { get; private set; }

        public bool IsEnemy { get; private set; }

        public bool IsActing { get; private set; }

        /// <summary>Whether the stack carries the game's Reloading restriction. A flag rather than a
        /// name because the game's own string for it is a sentence ("Can't perform ranged attacks"),
        /// and the word a readout wants there is one the mod owns.</summary>
        public bool IsReloading { get; private set; }

        /// <summary>The game's own names for the restrictions the stack itself carries, apart from
        /// Reloading: empty where it carries none.</summary>
        public IReadOnlyList<string> RestrictionNames { get; private set; }

        /// <summary>The game's own names for the buff and the nerf its status icons show, buffs
        /// first, exactly as the icons' details head them - the game's "x2" for a doubled effect
        /// included.</summary>
        public IReadOnlyList<string> EffectNames { get; private set; }

        /// <summary>The game's own name for the ability the stack has active now - a Spearwall set,
        /// an Ambush waiting, a song being sung - and empty where it has none. That it is active is
        /// the row's to word.</summary>
        public string ActiveAbilityName { get; private set; }
    }

    /// <summary>A thing on the battlefield that can be attacked, as the facts a spoken row is made
    /// of: the game's name for it and the health it has, where it has any.</summary>
    public struct CombatEntityFacts
    {
        public CombatEntityFacts(string name, bool hasHealth, int healthLeft, int maxHealth)
        {
            Name = name ?? string.Empty;
            HasHealth = hasHealth;
            HealthLeft = healthLeft;
            MaxHealth = maxHealth;
        }

        public string Name { get; private set; }

        public bool HasHealth { get; private set; }

        public int HealthLeft { get; private set; }

        public int MaxHealth { get; private set; }
    }

    /// <summary>What confirming a spell target DID, as facts rather than words: whether the native
    /// click ran at all, how many times the tile was a selected target before and after it, and
    /// whether the game is still aiming. <see cref="Screens.CombatScreen"/> words it.</summary>
    public struct CombatSpellTargetSelection
    {
        private CombatSpellTargetSelection(bool clicked, int previousCount, int count, bool stillTargeting)
        {
            Clicked = clicked;
            PreviousCount = previousCount;
            Count = count;
            StillTargeting = stillTargeting;
        }

        public bool Clicked { get; private set; }

        public int PreviousCount { get; private set; }

        public int Count { get; private set; }

        public bool StillTargeting { get; private set; }

        public static CombatSpellTargetSelection None
        {
            get { return new CombatSpellTargetSelection(false, 0, 0, false); }
        }

        public static CombatSpellTargetSelection Confirmed(int previousCount, int count, bool stillTargeting)
        {
            return new CombatSpellTargetSelection(true, previousCount, count, stillTargeting);
        }
    }

    public sealed partial class CombatAdapter : IPresent, IDisposable
    {
        private readonly object _sourceKey;
        private readonly DiContainer _container;
        private readonly IClientBattleFacade _facade;
        private readonly IBattleCursorManager _cursorManager;
        private readonly IBattleGridManager _gridManager;
        private readonly IBattlePathManager _pathManager;
        private readonly IBattleHighlightManager _highlightManager;
        private readonly IBattleViewManager _battleViewManager;
        private readonly IBattleAttackPreviewHandler _attackPreviewHandler;
        private readonly IBattleTooltipUtility _tooltipUtility;
        private readonly IInputManager _inputManager;
        private readonly ILocalizationHandler _localization;
        private readonly HashSet<string> _unknownCombatInstructions = new HashSet<string>(StringComparer.Ordinal);
        // Every recovery below says so the first time it happens; see FaultLog.
        private readonly FaultLog _faults = new FaultLog("CombatAdapter");
        private Dictionary<string, TileInstruction> _combatInstructionKinds;
        private ILanguageDefinition _combatInstructionKindsLanguage;
        private bool _combatInstructionKindsProbed;
        private readonly ICameraLookup _cameraLookup;
        private readonly object _cartographyConverter;
        private readonly IHumanBattleControllerFacade _humanBattleController;
        private readonly MouseKeyboardHumanBattleControllerModule _mouseKeyboardInputModule;
        private readonly IHumanBattleSpellController _battleSpellController;
        private readonly MouseKeyboardHumanBattleSpellModule _mouseKeyboardSpellInputModule;
        private readonly IBattleHudSignals _battleHudSignals;
        private readonly ISpellsLookup _spellsLookup;
        private readonly ITroopAbilityUtility _abilityUtility;
        private readonly MethodInfo _pointToWorldMethod;
        private readonly MethodInfo _primaryClickMethod;
        private readonly MethodInfo _secondaryClickMethod;
        private readonly MethodInfo _spellPrimaryClickMethod;
        private readonly MethodInfo _updateCurrentTileMethod;
        private readonly MethodInfo _updateAttackPreviewsMethod;
        private readonly FieldInfo _tooltipBehaviorField;
        private readonly FieldInfo _attackPreviewPoolField;
        private readonly FieldInfo _attackPreviewDamageContainerField;
        private readonly FieldInfo _attackPreviewDamageTextField;
        private readonly FieldInfo _attackPreviewKillsContainerField;
        private readonly FieldInfo _attackPreviewKillsTextField;
        private readonly FieldInfo _attackPreviewAdditionalTextField;
        private readonly FocusedTileOverlay _cursorOverlay = new FocusedTileOverlay("SongsOfConquestAccess_CombatCursor");
        private Action<ISpellDefinition, string> _targetInstructionHandler;
        // The screen's: it is handed the spell name and the instruction and does the wording.
        private Action<string, string> _spellTargetInstructionHandler;
        // The screen's: the narration asks for the cursor when a new turn begins.
        private Action<int> _actingTroopFocusHandler;
        private Action _spellTargetingEndHandler;
        private Action<ISpellDefinition> _beginCastHandler;
        // The delegate this adapter handed the battle HUD's signals, held here rather than on the
        // screen: it is a subscription to THIS battle, so it lives as long as the adapter does and
        // is let go in Dispose (AGENTS.md, Screen Resolution).
        private Action<TroopAbilityTargeting> _beginAbilityTargetingHandler;
        private Action<bool> _endAbilityTargetingHandler;
        private bool _hasBeenPresent;
        private bool _combatEnded;
        // The layout this battle is fought on, composed once per map. An adapter lives exactly as
        // long as the battle it wraps, but the map itself is replaced under it mid-battle by a forced
        // state (ClientBattleFacade.ReplaceState calls Level.ReplaceMap, which keeps the LEVEL object
        // and swaps the MapFormat), so the map object the game hands over is what all three of these
        // are keyed on.
        private string _battlefieldKey;
        private MapFormat _battlefieldKeyMap;
        // The ground of that map, analysed once and keyed on it, so a map replaced under the adapter
        // is read again. Terrain does not change during a fight; obstacles an ability creates are
        // entities and are not this.
        private BattlefieldTerrain _terrain;
        private MapFormat _terrainMap;
        private bool _warnedUnknownRegion;
        // The three adventure tiles this battle's scenery was built from, read once per map: a battle
        // is fought in one place, and the miss is kept too so a battle without them costs one look.
        private BattlefieldSurroundings _surroundings;
        private MapFormat _surroundingsMap;
        private bool _surroundingsProbed;
        // This frame's answer to "which enemies reach that tile", for the one tile it was asked
        // about. See BuildEnemyInfluenceSources.
        private List<CombatInfluenceSource> _influenceSources;
        private int _influenceFrame = -1;
        private Vector2Int _influencePoint;
        private int _influenceOccupantId = -1;

        public CombatAdapter(BattleSceneInstaller installer)
            : this(
                installer,
                Reflect.InstallerContainer(installer),
                Reflect.Resolve<IClientBattleFacade>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleCursorManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleGridManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattlePathManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleHighlightManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleViewManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleAttackPreviewHandler>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleTooltipUtility>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IInputManager>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ILocalizationHandler>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ICameraLookup>(Reflect.InstallerContainer(installer)),
                ResolveByTypeName(Reflect.InstallerContainer(installer), "Lavapotion.Cartography.ICartographyConverter"),
                Reflect.Resolve<IHumanBattleControllerFacade>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<MouseKeyboardHumanBattleControllerModule>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IHumanBattleSpellController>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<MouseKeyboardHumanBattleSpellModule>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<IBattleHudSignals>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ISpellsLookup>(Reflect.InstallerContainer(installer)),
                Reflect.Resolve<ITroopAbilityUtility>(Reflect.InstallerContainer(installer)))
        {
        }

        public CombatAdapter(
            object sourceKey,
            DiContainer container,
            IClientBattleFacade facade,
            IBattleCursorManager cursorManager,
            IBattleGridManager gridManager,
            IBattlePathManager pathManager,
            IBattleHighlightManager highlightManager,
            IBattleViewManager battleViewManager,
            IBattleAttackPreviewHandler attackPreviewHandler,
            IBattleTooltipUtility tooltipUtility,
            IInputManager inputManager,
            ILocalizationHandler localization,
            ICameraLookup cameraLookup,
            object cartographyConverter,
            IHumanBattleControllerFacade humanBattleController,
            MouseKeyboardHumanBattleControllerModule mouseKeyboardInputModule,
            IHumanBattleSpellController battleSpellController,
            MouseKeyboardHumanBattleSpellModule mouseKeyboardSpellInputModule,
            IBattleHudSignals battleHudSignals,
            ISpellsLookup spellsLookup,
            ITroopAbilityUtility abilityUtility)
        {
            _sourceKey = sourceKey;
            _container = container;
            _facade = facade;
            _cursorManager = cursorManager;
            _gridManager = gridManager;
            _pathManager = pathManager;
            _highlightManager = highlightManager;
            _battleViewManager = battleViewManager;
            _attackPreviewHandler = attackPreviewHandler;
            _tooltipUtility = tooltipUtility;
            _inputManager = inputManager;
            _localization = localization;
            _cameraLookup = cameraLookup;
            _cartographyConverter = cartographyConverter;
            _humanBattleController = humanBattleController;
            _mouseKeyboardInputModule = mouseKeyboardInputModule;
            _battleSpellController = battleSpellController;
            _mouseKeyboardSpellInputModule = mouseKeyboardSpellInputModule;
            _battleHudSignals = battleHudSignals;
            _spellsLookup = spellsLookup;
            _abilityUtility = abilityUtility;
            Hud = new BattleHudAdapter(container, facade, localization);
            _pointToWorldMethod = cartographyConverter != null
                ? AccessTools.Method(cartographyConverter.GetType(), "PointToWorld", new[] { typeof(int2), typeof(int) })
                : null;
            _secondaryClickMethod = mouseKeyboardInputModule != null
                ? AccessTools.Method(mouseKeyboardInputModule.GetType(), "PreHandleSecondaryClick")
                : null;
            _primaryClickMethod = mouseKeyboardInputModule != null
                ? AccessTools.Method(mouseKeyboardInputModule.GetType(), "PreHandlePrimaryClick")
                : null;
            _spellPrimaryClickMethod = mouseKeyboardSpellInputModule != null
                ? AccessTools.Method(mouseKeyboardSpellInputModule.GetType(), "HandlePrimaryClick")
                : null;
            _updateCurrentTileMethod = mouseKeyboardInputModule != null
                ? AccessTools.Method(mouseKeyboardInputModule.GetType(), "UpdateCurrentTile")
                : null;
            _updateAttackPreviewsMethod = mouseKeyboardInputModule != null
                ? AccessTools.Method(mouseKeyboardInputModule.GetType(), "UpdateAttackPreviews")
                : null;
            _tooltipBehaviorField = tooltipUtility != null
                ? AccessTools.Field(tooltipUtility.GetType(), "_tooltipBehavior")
                : null;
            _attackPreviewPoolField = attackPreviewHandler != null
                ? AccessTools.Field(attackPreviewHandler.GetType(), "_attackPreviewPool")
                : null;
            _attackPreviewDamageContainerField = AccessTools.Field(typeof(BattleAttackPreview), "_damageContainer");
            _attackPreviewDamageTextField = AccessTools.Field(typeof(BattleAttackPreview), "_damageText");
            _attackPreviewKillsContainerField = AccessTools.Field(typeof(BattleAttackPreview), "_killsContainer");
            _attackPreviewKillsTextField = AccessTools.Field(typeof(BattleAttackPreview), "_killsText");
            _attackPreviewAdditionalTextField = AccessTools.Field(typeof(BattleAttackPreview), "_additionalText");
        }

        public object SourceKey
        {
            get { return _sourceKey; }
        }

        public BattleHudAdapter Hud { get; private set; }

        /// <summary>The layout this battle is fought on, as
        /// <see cref="Battlefields.BattlefieldDescriptions"/> names it. <c>GetMap</c> is on
        /// <c>IMapProvider</c>, which the level facade implements, so it is a plain call and not a
        /// reflected one - a field read (<c>BattleMapProvider.GetMap</c>), which is why the map it
        /// answers is the key rather than the battle.</summary>
        public string BattlefieldKey
        {
            get
            {
                MapFormat map = CurrentMap();
                if (map == null)
                {
                    return _battlefieldKey;
                }

                if (_battlefieldKey == null || !ReferenceEquals(map, _battlefieldKeyMap))
                {
                    _battlefieldKeyMap = map;
                    _battlefieldKey = BattlefieldKeys.For(map);
                }

                return _battlefieldKey;
            }
        }

        /// <summary>The map this battle is being fought on right now: one property read and one field
        /// read, and the identity everything read off the layout is keyed on.</summary>
        private MapFormat CurrentMap()
        {
            return _facade != null && _facade.Level != null ? _facade.Level.GetMap() : null;
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
                // Constructor-time and once per battle, so it says so every time.
                SocAccessMod.Instance?.LogWarning(
                    "CombatAdapter could not resolve " + typeName + ": " + exception.Message);
                return null;
            }
        }

        public bool IsPresent()
        {
            bool present = _sourceKey != null
                && _facade != null
                && _facade.IsBattleActive
                && !_facade.IsBattleFinalized
                && _facade.Level != null
                && _facade.Level.Size.x > 0
                && _facade.Level.Size.y > 0;
            // The scene exists before the battle does (the installer binds, then the game sends its
            // start command), so "not present" only means the battle is OVER once it has been seen
            // going. EndCombat reads this rather than a hook telling it the fight finished.
            _hasBeenPresent = _hasBeenPresent || present;
            return present;
        }

        /// <summary>The battle is over: speak what the narration was still holding and put it back to
        /// rest. Called by the screen the frame <see cref="IsPresent"/> first answers false with the
        /// scene still up, and by <see cref="Dispose"/> for a battle the scene took away before that
        /// (a disconnect, a quit). Runs once, and does nothing at all for a scene whose battle never
        /// started.</summary>
        public void EndCombat()
        {
            if (_combatEnded || !_hasBeenPresent)
            {
                return;
            }

            _combatEnded = true;
            CombatEventNarrator.FlushPendingEventsForCombatEnd();
            CombatEventNarrator.EndNarration(this);
        }

        public void Dispose()
        {
            DetachAbilityTargetingBegin();
            ClearFocusedTileOverlay();
            ReleaseHoverOwnership();
            EndCombat();
        }

        public int GetCurrentTroopId()
        {
            return BattleFacadeState.CurrentTroopId(_facade);
        }

        public bool IsLocalTurn()
        {
            try
            {
                return _facade != null && _facade.Teams != null && _facade.Teams.IsCurrentLocal;
            }
            catch (Exception exception)
            {
                _faults.Report("IsLocalTurn", exception);
                return false;
            }
        }

        public string GetReadinessDiagnostic()
        {
            if (_sourceKey == null)
            {
                return "missing source key";
            }

            if (_facade == null)
            {
                return "missing IClientBattleFacade";
            }

            if (!_facade.IsBattleActive)
            {
                return "battle facade is not active";
            }

            if (_facade.IsBattleFinalized)
            {
                return "battle is finalized";
            }

            if (_facade.Level == null || _facade.Level.Size.x <= 0 || _facade.Level.Size.y <= 0)
            {
                return "battle level is not ready";
            }

            return "ready";
        }

        public Vector2Int GetInitialTile()
        {
            CombatTargetingMode mode = GetTargetingMode();
            if (mode == CombatTargetingMode.Spell)
            {
                Vector2Int hoverTile = _battleSpellController.CurrentHoverTile;
                if (IsValidTile(hoverTile))
                {
                    return hoverTile;
                }
            }
            else if (mode == CombatTargetingMode.Ability && _humanBattleController != null)
            {
                Vector2Int hoverTile = _humanBattleController.CurrentHoverTile;
                if (IsValidTile(hoverTile))
                {
                    return hoverTile;
                }
            }

            Vector2Int localTroopPosition;
            if (TryGetInitialLocalTroopPosition(out localTroopPosition))
            {
                return localTroopPosition;
            }

            return Vector2Int.zero;
        }

        private bool TryGetInitialLocalTroopPosition(out Vector2Int position)
        {
            position = Vector2Int.zero;
            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return false;
            }

            IBattleTroopState current = GetCurrentTroop();
            if (current != null
                && current.TeamId == localTeamId
                && current.GetIsAlive()
                && IsValidTile(current.Position))
            {
                position = current.Position;
                return true;
            }

            if (_facade == null || _facade.Troops == null || _facade.Troops.All == null)
            {
                return false;
            }

            foreach (IBattleTroopState troop in _facade.Troops.All)
            {
                if (troop == null
                    || troop.TeamId != localTeamId
                    || !troop.GetIsAlive()
                    || !IsValidTile(troop.Position))
                {
                    continue;
                }

                position = troop.Position;
                return true;
            }

            return false;
        }

        public CombatTile GetTile(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return null;
            }

            CombatTile tile = new CombatTile(point);
            tile.Elevation = SafeGetElevation(point);
            tile.IsReachable = IsReachable(point);
            tile.IsImpassable = IsImpassable(point);
            tile.IsBlocked = IsBlocked(point);
            tile.Troop = GetTroopAt(point);
            tile.TroopId = tile.Troop != null ? tile.Troop.Id : -1;
            tile.IsTroopAttackable = IsAttackable(tile.Troop);
            tile.Entity = GetAttackableEntityAt(point);
            tile.EntityId = tile.Entity != null ? tile.Entity.Id : -1;
            tile.IsEntityAttackable = IsAttackable(tile.Entity);
            AddDangerousMapEffects(point, tile);
            AddRootEffects(point, tile);
            tile.DecorativeFeature = GetDecorativeFeatureAt(point, tile.Entity);
            BattlefieldTerrain terrain = GetTerrain();
            tile.Kind = terrain != null ? terrain.GetKind(point) : BattlefieldCellKind.OffGrid;
            tile.Obstacle = terrain != null ? terrain.GetObstacle(point) : null;
            tile.IsChokePoint = terrain != null && terrain.IsChokePoint(point);
            return tile;
        }

        /// <summary>An authored description pointed at a cell no group of ground covers. Said once
        /// for the life of this adapter - one menu, one battle - because a description is read
        /// again every time the node is, and an authoring mistake is worth one line in the log and
        /// not one a frame.</summary>
        public void WarnUnknownRegion(string placeholder)
        {
            if (_warnedUnknownRegion)
            {
                return;
            }

            _warnedUnknownRegion = true;
            SocAccessMod.Instance?.LogWarning(
                "The description of " + BattlefieldKey + " points at " + placeholder + ", which is no group of ground");
        }

        /// <summary>The ground this battle is fought on, analysed once per map: terrain does not
        /// change during a fight, so the answer is kept against the map object the game gave and
        /// read again only if that object is replaced.</summary>
        public BattlefieldTerrain GetTerrain()
        {
            if (_facade == null || _facade.Level == null)
            {
                return null;
            }

            MapFormat map = CurrentMap();
            if (_terrain != null && ReferenceEquals(_terrainMap, map))
            {
                return _terrain;
            }

            try
            {
                _terrainMap = map;
                _terrain = BattlefieldTerrain.Analyse(
                    _facade.Level.Size,
                    ReadTerrainCells(),
                    map != null && map.Metadata.Type.IsSiege(),
                    namesObstacles: true,
                    spawnPoints: BattlefieldSpawnCells.For(map));
            }
            catch (Exception exception)
            {
                _faults.Report("GetTerrain", exception);
                _terrain = BattlefieldTerrain.Analyse(new Vector2Int(0, 0), null, false);
            }

            return _terrain;
        }

        /// <summary>What lies around the board: the west, north and east adventure tiles the game
        /// sampled when it started this battle, as ground the mod has a meaning for. Null where the
        /// battle carries none - a skirmish built from a seed rather than from a map.</summary>
        public BattlefieldSurroundings GetSurroundings()
        {
            MapFormat map = CurrentMap();
            if (_surroundingsProbed && ReferenceEquals(_surroundingsMap, map))
            {
                return _surroundings;
            }

            _surroundingsProbed = true;
            _surroundingsMap = map;
            _surroundings = null;
            try
            {
                BattleSurroundings surroundings = _facade != null && _facade.Level != null
                    ? _facade.Level.Surroundings
                    : null;
                if (surroundings != null && surroundings.isValid)
                {
                    _surroundings = new BattlefieldSurroundings(
                        Surrounding(surroundings.west),
                        Surrounding(surroundings.north),
                        Surrounding(surroundings.east));
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetSurroundings", exception);
            }

            return _surroundings;
        }

        /// <summary>One sampled tile as adventure ground: its decoration where it has one - the
        /// game keeps only trees and mountains on these - and water otherwise.</summary>
        private static AdventureTerrainKind Surrounding(BattleSurroundingTile tile)
        {
            AdventureTerrainKind decoration = AdventureMapAdapter.GetDecorationTerrain(tile.decoration);
            if (decoration != AdventureTerrainKind.Unknown)
            {
                return decoration;
            }

            return tile.water > 0 ? AdventureTerrainKind.Water : AdventureTerrainKind.Unknown;
        }

        private List<BattlefieldCell> ReadTerrainCells()
        {
            Vector2Int size = _facade.Level.Size;
            List<BattlefieldCell> cells = new List<BattlefieldCell>(Math.Max(0, size.x * size.y));
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    bool onGrid = _facade.Level.IsPointWithinMap(point);
                    // The theme is the PAINTED one: a fight repaints every cell from the adventure
                    // ground it was joined on, so this is the boulders the player is looking at and
                    // not the layout's own.
                    cells.Add(new BattlefieldCell(
                        point,
                        onGrid,
                        onGrid ? _facade.Level.GetElevation(point) : 0,
                        !onGrid || !_facade.Level.IsWalkableStatic(point),
                        onGrid ? _facade.Level.GetDecoration(point) : 0,
                        onGrid ? _facade.Level.GetEffect(point) : 0,
                        onGrid && _facade.Level.GetWater(point) > 0,
                        onGrid ? _facade.Level.GetTheme(point) : 0));
                }
            }

            return cells;
        }

        /// <summary>Who stands on a tile, as the three facts that change what the tile reads as while
        /// the cursor stands still: the id of the troop there - -1 for an empty tile, and for a dead
        /// one, which the game's point lookup already answers null for - the health its stack has
        /// lost, and how many bacterias and restrictions it carries, which is what a wielder's spell
        /// changes on a stack that takes no damage (the game's own dossier reads both lists,
        /// ClientBattleTroopFacade.GetDetails). The game's own point cache answers it and the two
        /// counts are list reads, so this is the cheap read a per-frame cache keys on;
        /// <see cref="GetTile"/> composes the whole tile and is not that.</summary>
        public void GetTileTroopState(Vector2Int point, out int troopId, out int healthLost, out int statusCount)
        {
            IBattleTroopState troop = GetTroopAt(point);
            troopId = troop != null ? troop.Id : -1;
            healthLost = troop != null ? troop.HealthLost : 0;
            statusCount = troop != null
                ? (troop.Bacterias != null ? troop.Bacterias.Count : 0)
                    + (troop.Restrictions != null ? troop.Restrictions.Count : 0)
                : 0;
        }

        public bool IsValidTile(Vector2Int point)
        {
            return _facade != null
                && _facade.Level != null
                && _facade.Level.IsPointWithinMap(point);
        }

        public Vector2Int ClampToMap(Vector2Int point)
        {
            if (_facade == null || _facade.Level == null)
            {
                return Vector2Int.zero;
            }

            Vector2Int size = _facade.Level.Size;
            int x = Math.Max(0, Math.Min(size.x - 1, point.x));
            int y = Math.Max(0, Math.Min(size.y - 1, point.y));
            return new Vector2Int(x, y);
        }

        public void FocusTile(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            PathNode[] path = GetPathTo(point);
            if (GetTargetingMode() != CombatTargetingMode.None || IsAnySpellCastingStateActive())
            {
                SetNativeCursorTile(point, path);
                return;
            }

            // The keyboard cursor is where the game's hover is now, and stays there until the
            // physical pointer moves; no inspection is pinning it any more.
            ClearHoverPin();
            TakeHoverOwnership();
            _attackPreviewHandler?.Hide();
            // The hover sync below points the four managers at the tile ONCE and then sets their
            // states, which is the order the game's own update keeps. Pointing them here first, or
            // resetting them to the current troop first, re-entered the path manager's state: the
            // game's state machine has no same-state guard, and re-entering CurrentTroop repeats its
            // "Blocked" notification for a target the shot cannot reach.
            // The tile and the path travel down with the point: nothing between here and the hover
            // sync changes what either of them answers, and reading them again cost a second
            // whole-board path search per cursor step.
            CombatTile tile = GetTile(point);
            if (tile != null && (tile.Troop != null || tile.Entity != null))
            {
                SynchronizeNativeHoverForPreview(point, tile, path);
            }
            else
            {
                // An empty tile has no preview to ask for, but the hover still has to FOLLOW the
                // cursor onto it: the game's own update, which used to move the hover here, does not
                // run while the keyboard owns it.
                SynchronizeNativeHoverForInput(point, tile, path);
            }
        }

        private void BuildStackRanges(IBattleTroopState troop, CombatInspectContext context)
        {
            context.Add(troop.Position, CombatRangeIndicator.Source);

            PathNode[] movement = _facade.Level.PointsWithinReach(troop, troop.Position, troop.Stats.Movement.GetValue());
            for (int i = 0; i < movement.Length; i++)
            {
                Vector2Int point = ToVector2Int(movement[i].point);
                if (IsValidTile(point))
                {
                    context.Add(point, CombatRangeIndicator.Movement);
                }
            }

            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    AddAttackIndicators(troop, point, context);
                }
            }

            IBattleTroopState current = GetCurrentTroop();
            if (current != null)
            {
                for (int y = 0; y < _facade.Level.Size.y; y++)
                {
                    for (int x = 0; x < _facade.Level.Size.x; x++)
                    {
                        Vector2Int point = new Vector2Int(x, y);
                        if (point != troop.Position && IsInZoneOfControl(current, troop, point))
                        {
                            context.Add(point, CombatRangeIndicator.ZoneOfControl);
                        }
                    }
                }
            }
        }

        private void AddAttackIndicators(IBattleTroopState troop, Vector2Int point, CombatInspectContext context)
        {
            HashSet<CombatRangeIndicator> indicators = BuildAttackIndicators(troop, point);
            foreach (CombatRangeIndicator indicator in indicators)
            {
                context.Add(point, indicator);
            }
        }

        private HashSet<CombatRangeIndicator> BuildAttackIndicators(IBattleTroopState troop, Vector2Int point)
        {
            HashSet<CombatRangeIndicator> indicators = new HashSet<CombatRangeIndicator>();
            if (!IsValidTile(point))
            {
                return indicators;
            }

            try
            {
                if (_facade.Troops.GetIsWithinAttackRange(troop, point, DamageType.Ranged))
                {
                    indicators.Add(CombatRangeIndicator.Attack);
                }

                if (_facade.Troops.GetIsWithinDeadlyRange(troop, point, DamageType.Ranged))
                {
                    indicators.Add(CombatRangeIndicator.Deadly);
                }
            }
            catch (Exception exception)
            {
                _faults.Report("BuildAttackIndicators.Ranged", exception);
            }

            try
            {
                if (_facade.Troops.GetIsWithinAttackRange(troop, point, DamageType.Melee))
                {
                    indicators.Add(CombatRangeIndicator.Melee);
                }
            }
            catch (Exception exception)
            {
                _faults.Report("BuildAttackIndicators.Melee", exception);
            }

            return indicators;
        }

        private HashSet<CombatRangeIndicator> BuildInfluenceIndicators(IBattleTroopState troop, Vector2Int point)
        {
            HashSet<CombatRangeIndicator> indicators = BuildAttackIndicators(troop, point);
            if (troop == null || point == troop.Position)
            {
                return indicators;
            }

            try
            {
                PathNode[] movement = _facade.Level.PointsWithinReach(troop, troop.Position, troop.Stats.Movement.GetValue());
                for (int i = 0; i < movement.Length; i++)
                {
                    if (ToVector2Int(movement[i].point) == point)
                    {
                        indicators.Add(CombatRangeIndicator.Movement);
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("BuildInfluenceIndicators", exception);
            }

            IBattleTroopState current = GetCurrentTroop();
            if (current != null && IsInZoneOfControl(current, troop, point))
            {
                indicators.Add(CombatRangeIndicator.ZoneOfControl);
            }

            return indicators;
        }

        private bool IsInZoneOfControl(IBattleTroopState movingTroop, IBattleTroopState controllingTroop, Vector2Int point)
        {
            if (movingTroop == null || controllingTroop == null || _facade == null || _facade.Level == null)
            {
                return false;
            }

            try
            {
                return ZoneOfControlTriggerSystem.ExertsZoneOfControl(movingTroop, controllingTroop, point, _facade.Level);
            }
            catch (Exception exception)
            {
                _faults.Report("IsInZoneOfControl", exception);
                return false;
            }
        }

        public void AddEnemyInfluenceForSpeech(Vector2Int point, IBattleTroopState occupyingTroop, List<string> parts)
        {
            string influence = DescribeEnemyInfluenceForSpeech(point, occupyingTroop);
            if (!string.IsNullOrWhiteSpace(influence))
            {
                parts.Add(influence);
            }
        }

        public string DescribeEnemyInfluenceForSpeech(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            return CombatInfluenceFormatter.Format(BuildEnemyInfluenceSources(point, occupyingTroop));
        }

        public bool IsThreatenedByEnemy(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            return BuildEnemyInfluenceSources(point, occupyingTroop).Count > 0;
        }

        /// <summary>Which enemy stacks reach a tile, and how. Every stack is asked for its own
        /// movement and attack ranges, so this is a pathfind per enemy per call - and one cursor step
        /// asks for it three times over: the cue that warns about a threatened tile, the tile's
        /// readout, and the graph rebuilt in the same frame. Held for the frame it was worked out in
        /// and for the tile it was worked out for, which is as long as the answer cannot have
        /// changed; the frame count is the game's own, so nothing has to remember to drop it.
        /// </summary>
        private List<CombatInfluenceSource> BuildEnemyInfluenceSources(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            int frame = Time.frameCount;
            int occupantId = occupyingTroop != null ? occupyingTroop.Id : -1;
            if (_influenceSources != null
                && _influenceFrame == frame
                && _influencePoint == point
                && _influenceOccupantId == occupantId)
            {
                return _influenceSources;
            }

            _influenceFrame = frame;
            _influencePoint = point;
            _influenceOccupantId = occupantId;
            _influenceSources = BuildEnemyInfluenceSourcesCore(point, occupyingTroop);
            return _influenceSources;
        }

        private List<CombatInfluenceSource> BuildEnemyInfluenceSourcesCore(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            int perspectiveTeamId = GetLocalTeamId();
            if (_facade == null || _facade.Troops == null || _facade.Teams == null || perspectiveTeamId < 0)
            {
                return new List<CombatInfluenceSource>();
            }

            List<CombatInfluenceSource> sources = new List<CombatInfluenceSource>();
            foreach (IBattleTroopState troop in _facade.Troops.All)
            {
                if (troop == null
                    || troop.TeamId == perspectiveTeamId
                    || !troop.GetIsAlive()
                    || (occupyingTroop != null && troop.Id == occupyingTroop.Id)
                    || troop.Position == point)
                {
                    continue;
                }

                HashSet<CombatRangeIndicator> indicators = BuildInfluenceIndicators(troop, point);
                if (indicators.Count > 0)
                {
                    sources.Add(new CombatInfluenceSource(CreateTroopRef(troop), indicators));
                }
            }

            return sources;
        }

        private PathNode[] GetPathToEntity(IMapEntity entity)
        {
            IBattleTroopState current = GetCurrentTroop();
            if (current == null || entity == null)
            {
                return Array.Empty<PathNode>();
            }

            if (current.GetCanAttackMapEntityAtAll(entity)
                && current.GetCanAttackMelee()
                && !current.GetCanAttackRanged()
                && !_facade.Level.CanMeleeFromCurrentPosition(current, entity.Position))
            {
                return ToArray(_facade.Level.GetPathToFirstMeleeAttackPosition(current, entity.Position));
            }

            return GetPathTo(entity.Position);
        }

        private PathNode[] GetPathTo(Vector2Int point)
        {
            IBattleTroopState current = GetCurrentTroop();
            if (current == null || _facade == null || _facade.Level == null)
            {
                return Array.Empty<PathNode>();
            }

            return _facade.Level.PointsInPath(current, current.Position, point);
        }

        private bool IsReachable(Vector2Int point)
        {
            IBattleTroopState current = GetCurrentTroop();
            return current != null && _facade.Commands != null && _facade.Commands.CanMove(current.Id, point);
        }

        private bool IsImpassable(Vector2Int point)
        {
            if (_facade == null || _facade.Level == null)
            {
                return false;
            }

            return !_facade.Level.IsWalkableStatic(point);
        }

        private bool IsBlocked(Vector2Int point)
        {
            IBattleTroopState current = GetCurrentTroop();
            if (current == null || _facade == null || _facade.Level == null)
            {
                return false;
            }

            return !IsImpassable(point) && !_facade.Level.IsWalkable(current.TeamId, point);
        }

        private byte SafeGetElevation(Vector2Int point)
        {
            try
            {
                return _facade.Level.GetElevation(point);
            }
            catch (Exception exception)
            {
                _faults.Report("SafeGetElevation", exception);
                return 0;
            }
        }

        private IBattleTroopState GetCurrentTroop()
        {
            return _facade != null && _facade.Troops != null ? _facade.Troops.Current : null;
        }

        private bool IsFriendlyTroop(IBattleTroopState troop)
        {
            int localTeamId = GetLocalTeamId();
            return troop != null && (localTeamId < 0 || troop.TeamId == localTeamId);
        }

        private bool IsFriendlyMapEntity(IMapEntity entity)
        {
            if (entity == null || _facade == null || _facade.MapEntities == null)
            {
                return false;
            }

            try
            {
                int owningTeamId = _facade.MapEntities.GetOwningTeamId(entity.Id);
                int localTeamId = GetLocalTeamId();
                return localTeamId >= 0 && owningTeamId == localTeamId;
            }
            catch (Exception exception)
            {
                _faults.Report("IsFriendlyMapEntity", exception);
                return false;
            }
        }

        private bool IsAttackable(IBattleTroopState troop)
        {
            try
            {
                IBattleTroopState current = GetCurrentTroop();
                if (troop == null
                    || current == null
                    || _facade == null
                    || _facade.Commands == null
                    || _facade.Level == null
                    || !IsLocalTurn()
                    || !current.GetCanAttackOtherTroopAtAll(troop))
                {
                    return false;
                }

                return _facade.Commands.CanAttack(current.Id, troop.Position)
                    || (IsMeleeOnly(current) && _facade.Level.AllEnemiesWithinMeleeReach(current).Contains(troop));
            }
            catch (Exception exception)
            {
                _faults.Report("IsAttackable.Troop", exception);
                return false;
            }
        }

        private bool IsAttackable(IMapEntity entity)
        {
            try
            {
                IBattleTroopState current = GetCurrentTroop();
                if (entity == null
                    || current == null
                    || _facade == null
                    || _facade.Commands == null
                    || _facade.Level == null
                    || !IsLocalTurn()
                    || !current.GetCanAttackMapEntityAtAll(entity))
                {
                    return false;
                }

                return _facade.Commands.CanAttack(current.Id, entity.Position)
                    || (IsMeleeOnly(current) && _facade.Level.AllMapEntitiesWithinMeleeReach(current).Contains(entity));
            }
            catch (Exception exception)
            {
                _faults.Report("IsAttackable.Entity", exception);
                return false;
            }
        }

        private static bool IsMeleeOnly(IBattleTroopState troop)
        {
            return troop != null && troop.GetCanAttackMelee() && !troop.GetCanAttackRanged();
        }

        private int GetCurrentMovesLeft()
        {
            IBattleTroopState current = GetCurrentTroop();
            return current != null ? current.MovesLeft : 0;
        }

        private IBattleTroopState GetTroopAt(Vector2Int point)
        {
            if (_facade == null || _facade.Troops == null || _facade.Teams == null || _facade.Teams.Current == null)
            {
                return null;
            }

            IBattleTroopState troop = null;
            if (_facade.Troops.TryGetAtPoint(_facade.Teams.Current.Id, point, out troop))
            {
                return troop;
            }

            int otherTeamId = _facade.Teams.GetOtherTeamId(_facade.Teams.Current.Id);
            return _facade.Troops.TryGetAtPoint(otherTeamId, point, out troop) ? troop : null;
        }

        private IMapEntity GetAttackableEntityAt(Vector2Int point)
        {
            IMapEntity entity = _facade != null && _facade.MapEntities != null ? _facade.MapEntities.GetAt(point) : null;
            return entity != null && entity.IsEnabled && entity.HasComponent<IHealthComponent>() ? entity : null;
        }

        private bool HasDangerousMapEntityEffect(Vector2Int point)
        {
            try
            {
                return _facade != null
                    && _facade.MapEntities != null
                    && _facade.MapEntities.GetDangerousAuraMapEntityEffects(point).Any();
            }
            catch (Exception exception)
            {
                _faults.Report("HasDangerousMapEntityEffect", exception);
                return false;
            }
        }

        /// <summary>
        /// The roots a Seed or a Root of the Mother spreads over the tiles around it, which the game
        /// DRAWS on the ground and says in words only in the move tooltip of a reachable empty tile
        /// (<c>BattleGridManager.HandleInspectTileEnter</c>): an unreachable root tile, and one with a
        /// troop standing on it, said nothing at all. The same two questions the game asks there are
        /// asked here for every tile, from the local player's side rather than the acting team's,
        /// because the roots are there on the enemy's turn too: an enemy spreader that carries
        /// Mother's Scorn damages the player's troops standing on its roots at the start of its
        /// turn, and the player's own spreader with Mother's Embrace absorbs damage for them. Named
        /// by the game's own names for those two traits.
        /// </summary>
        private void AddRootEffects(Vector2Int point, CombatTile tile)
        {
            if (tile == null || _facade == null || _facade.Troops == null || _facade.Teams == null)
            {
                return;
            }

            try
            {
                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return;
                }

                AddScornEffects(point, _facade.Teams.GetOtherTeamId(localTeamId), tile.HostileRootEffects);
                AddEmbraceEffects(point, localTeamId, tile.FriendlyRootEffects);

                // A troop on the tile is under the roots its OWN side decides, which is the question
                // the game's troop details ask (BattleTroopDetails.RegisterRootSpreadSidePanels): an
                // enemy troop on its own Seed's roots is embraced, not scorned.
                if (tile.Troop != null)
                {
                    AddScornEffects(point, _facade.Teams.GetOtherTeamId(tile.Troop.TeamId), tile.OccupantRootEffects);
                    if (tile.Troop.Reference.CanBeAffectedByMothersLove())
                    {
                        AddEmbraceEffects(point, tile.Troop.TeamId, tile.OccupantRootEffects);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("AddRootEffects", exception);
            }
        }

        private void AddScornEffects(Vector2Int point, int spreaderTeamId, List<string> into)
        {
            IBattleTroopState[] spreaders = _facade.Troops.GetRootSpreadersForTile(point, spreaderTeamId);
            for (int i = 0; i < spreaders.Length; i++)
            {
                if (spreaders[i].IsHatingMother())
                {
                    AddRootEffect(
                        into,
                        spreaders[i].HasBacteria(BacteriaTypes.TraitMothersHateUpgraded)
                            ? BacteriaTypes.TraitMothersHateUpgraded
                            : BacteriaTypes.TraitMothersHate);
                }
            }
        }

        private void AddEmbraceEffects(Vector2Int point, int spreaderTeamId, List<string> into)
        {
            IBattleTroopState[] spreaders = _facade.Troops.GetRootSpreadersForTile(point, spreaderTeamId);
            for (int i = 0; i < spreaders.Length; i++)
            {
                if (spreaders[i].HasBacteria(BacteriaTypes.TraitMothersLoveProvider))
                {
                    AddRootEffect(into, BacteriaTypes.TraitMothersLoveProvider);
                }
            }
        }

        private void AddRootEffect(List<string> into, BacteriaTypes trait)
        {
            string name = RootEffectName(trait);
            if (!string.IsNullOrWhiteSpace(name) && !into.Contains(name))
            {
                into.Add(name);
            }
        }

        private string RootEffectName(BacteriaTypes trait)
        {
            return SpokenLines.Clean(LocalizeText(BacteriaReferenceUtility.GetLocalizationNameKey(trait)));
        }

        /// <summary>
        /// What the roots of <paramref name="troop"/> do for or against the local player, under the
        /// game's name for it, and how many tiles they cover with the troop standing at
        /// <paramref name="position"/>. The roots are no growth: they are every tile within the
        /// troop's root spread of where it stands (<c>GetRootSpreadFromTroop</c>), so when the troop
        /// moves the whole patch moves with it, and the game shows that only by redrawing the
        /// ground. False for a troop with no roots, and for roots that do nothing to the player
        /// (an enemy's Embrace, the player's own Scorn).
        /// </summary>
        public bool TryGetRootCoverage(IBattleTroopState troop, Vector2Int position, out string effectName, out int tileCount)
        {
            effectName = string.Empty;
            tileCount = 0;
            try
            {
                if (troop == null || _facade == null || _facade.Troops == null || troop.Stats.RootSpread.GetValue() <= 0)
                {
                    return false;
                }

                if (IsEnemyTroop(troop))
                {
                    if (!troop.IsHatingMother())
                    {
                        return false;
                    }

                    effectName = RootEffectName(troop.HasBacteria(BacteriaTypes.TraitMothersHateUpgraded)
                        ? BacteriaTypes.TraitMothersHateUpgraded
                        : BacteriaTypes.TraitMothersHate);
                }
                else
                {
                    if (!troop.HasBacteria(BacteriaTypes.TraitMothersLoveProvider))
                    {
                        return false;
                    }

                    effectName = RootEffectName(BacteriaTypes.TraitMothersLoveProvider);
                }

                tileCount = _facade.Troops.GetRootSpreadFromTroop(troop, position).Count();
                return tileCount > 0 && !string.IsNullOrWhiteSpace(effectName);
            }
            catch (Exception exception)
            {
                _faults.Report("TryGetRootCoverage", exception);
                return false;
            }
        }

        private void AddDangerousMapEffects(Vector2Int point, CombatTile tile)
        {
            if (tile == null || !HasDangerousMapEntityEffect(point))
            {
                return;
            }

            IMapEntity entity = _facade != null && _facade.MapEntities != null
                ? _facade.MapEntities.GetAtIncludingNonBlockers(point)
                : null;
            if (entity == null || !entity.IsEnabled || !entity.IsVisibleInGame)
            {
                return;
            }

            string name = GetMapEntityName(entity);
            if (!string.IsNullOrWhiteSpace(name) && !tile.MapEffects.Contains(name))
            {
                tile.MapEffects.Add(name);
            }

            if (!tile.DangerousMapEffectEntityIds.Contains(entity.Id))
            {
                tile.DangerousMapEffectEntityIds.Add(entity.Id);
            }
        }

        private string GetDecorativeFeatureAt(Vector2Int point, IMapEntity attackableEntity)
        {
            IMapEntity entity = _facade != null && _facade.MapEntities != null
                ? _facade.MapEntities.GetAtIncludingNonBlockers(point)
                : null;
            if (entity == null || !entity.IsEnabled)
            {
                return string.Empty;
            }

            if (attackableEntity != null && entity.Id == attackableEntity.Id)
            {
                return string.Empty;
            }

            return IsDebris(entity) ? ModText.Get(ModStrings.Spatial.Debris) : string.Empty;
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            if (IsDebris(entity))
            {
                return ModText.Get(ModStrings.Spatial.Debris);
            }

            string customNameKey;
            if (entity.TryGetCustomNameKey(out customNameKey))
            {
                string customName = GameText.Get(_localization, customNameKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return SpokenLines.Clean(customName);
                }
            }

            string localizedName = GameText.Get(_localization, entity.NameKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return SpokenLines.Clean(localizedName);
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return SpokenLines.Clean(entity.Name);
            }

            return SpokenLines.Clean(entity.NameKey);
        }

        private static bool IsDebris(IMapEntity entity)
        {
            return entity != null && (entity.BlueprintId == 24 || entity.BlueprintId == 25);
        }

        private IDetails BuildEntityDetails(IMapEntity entity)
        {
            IHealthComponent health = entity != null ? entity.GetComponent<IHealthComponent>() : null;
            if (health == null)
            {
                return null;
            }

            return new BattleEntityDetails
            {
                HealthLeft = health.HealthLeft,
                HealthMax = health.MaxHealth.GetValue()
            };
        }

        private static PathNode[] ToArray(IEnumerable<PathNode> nodes)
        {
            if (nodes == null)
            {
                return Array.Empty<PathNode>();
            }

            List<PathNode> result = new List<PathNode>();
            foreach (PathNode node in nodes)
            {
                result.Add(node);
            }

            return result.ToArray();
        }

        private static List<Vector2Int> ConvertPath(PathNode[] path)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            if (path == null)
            {
                return points;
            }

            for (int i = 0; i < path.Length; i++)
            {
                Vector2Int point = ToVector2Int(path[i].point);
                if (points.Count == 0 || points[points.Count - 1] != point)
                {
                    points.Add(point);
                }
            }

            return points;
        }

        private static Vector2Int ToVector2Int(int2 point)
        {
            return new Vector2Int(point.x, point.y);
        }

        public static Vector2Int[] GetNeighbors(Vector2Int point)
        {
            if ((point.y & 1) == 0)
            {
                return new[]
                {
                    new Vector2Int(point.x - 1, point.y),
                    new Vector2Int(point.x + 1, point.y),
                    new Vector2Int(point.x - 1, point.y + 1),
                    new Vector2Int(point.x, point.y + 1),
                    new Vector2Int(point.x - 1, point.y - 1),
                    new Vector2Int(point.x, point.y - 1)
                };
            }

            return new[]
            {
                new Vector2Int(point.x - 1, point.y),
                new Vector2Int(point.x + 1, point.y),
                new Vector2Int(point.x, point.y + 1),
                new Vector2Int(point.x + 1, point.y + 1),
                new Vector2Int(point.x, point.y - 1),
                new Vector2Int(point.x + 1, point.y - 1)
            };
        }

    }
}
