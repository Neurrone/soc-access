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
using SongsOfConquest.Common.Spells;
using SongsOfConquest.Server.Battle;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquest.Utilities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal enum CombatTargetingMode
    {
        None,
        Spell,
        Ability
    }

    internal sealed class CombatAdapter
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(BattleSceneInstaller), "Container");
        private static readonly Dictionary<BattleAttackPreview, string> AttackPreviewAdditionalTexts =
            new Dictionary<BattleAttackPreview, string>();
        private readonly object _sourceKey;
        private readonly DiContainer _container;
        private readonly IClientBattleFacade _facade;
        private readonly IBattleCursorManager _cursorManager;
        private readonly IBattleGridManager _gridManager;
        private readonly IBattlePathManager _pathManager;
        private readonly IBattleHighlightManager _highlightManager;
        private readonly IBattleAttackPreviewHandler _attackPreviewHandler;
        private readonly IBattleTooltipUtility _tooltipUtility;
        private readonly IInputManager _inputManager;
        private readonly ILocalizationHandler _localization;
        private readonly ICameraLookup _cameraLookup;
        private readonly object _cartographyConverter;
        private readonly IHumanBattleControllerFacade _humanBattleController;
        private readonly MouseKeyboardHumanBattleControllerModule _mouseKeyboardInputModule;
        private readonly IHumanBattleSpellController _battleSpellController;
        private readonly MouseKeyboardHumanBattleSpellModule _mouseKeyboardSpellInputModule;
        private readonly IBattleHudSignals _battleHudSignals;
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
        private GameObject _cursorOverlay;
        private RectTransform[] _cursorOverlaySegments;
        private Action<ISpellDefinition, string> _targetInstructionHandler;
        private Action _spellTargetingEndHandler;
        private Action<ISpellDefinition> _beginCastHandler;
        private Action<bool> _endAbilityTargetingHandler;

        public CombatAdapter(BattleSceneInstaller installer)
            : this(
                installer,
                GetContainer(installer),
                Resolve<IClientBattleFacade>(GetContainer(installer)),
                Resolve<IBattleCursorManager>(GetContainer(installer)),
                Resolve<IBattleGridManager>(GetContainer(installer)),
                Resolve<IBattlePathManager>(GetContainer(installer)),
                Resolve<IBattleHighlightManager>(GetContainer(installer)),
                Resolve<IBattleAttackPreviewHandler>(GetContainer(installer)),
                Resolve<IBattleTooltipUtility>(GetContainer(installer)),
                Resolve<IInputManager>(GetContainer(installer)),
                Resolve<ILocalizationHandler>(GetContainer(installer)),
                Resolve<ICameraLookup>(GetContainer(installer)),
                ResolveByTypeName(GetContainer(installer), "Lavapotion.Cartography.ICartographyConverter"),
                Resolve<IHumanBattleControllerFacade>(GetContainer(installer)),
                Resolve<MouseKeyboardHumanBattleControllerModule>(GetContainer(installer)),
                Resolve<IHumanBattleSpellController>(GetContainer(installer)),
                Resolve<MouseKeyboardHumanBattleSpellModule>(GetContainer(installer)),
                Resolve<IBattleHudSignals>(GetContainer(installer)),
                Resolve<ITroopAbilityUtility>(GetContainer(installer)))
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
            ITroopAbilityUtility abilityUtility)
        {
            _sourceKey = sourceKey;
            _container = container;
            _facade = facade;
            _cursorManager = cursorManager;
            _gridManager = gridManager;
            _pathManager = pathManager;
            _highlightManager = highlightManager;
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

        public bool Matches(ClientBattleCommandsFacade commands)
        {
            return commands != null
                && _facade != null
                && ReferenceEquals(_facade.Commands, commands);
        }

        private static DiContainer GetContainer(BattleSceneInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
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

        internal static void CaptureAttackPreviewAdditionalText(BattleAttackPreview preview, string text)
        {
            if (preview == null)
            {
                return;
            }

            text = SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                AttackPreviewAdditionalTexts.Remove(preview);
                return;
            }

            AttackPreviewAdditionalTexts[preview] = text;
        }

        internal static void ClearAttackPreviewAdditionalText(BattleAttackPreview preview)
        {
            if (preview != null)
            {
                AttackPreviewAdditionalTexts.Remove(preview);
            }
        }

        public bool IsPresent()
        {
            return _sourceKey != null
                && _facade != null
                && _facade.IsBattleActive
                && !_facade.IsBattleFinalized
                && _facade.Level != null
                && _facade.Level.Size.x > 0
                && _facade.Level.Size.y > 0;
        }

        public int GetCurrentTroopId()
        {
            try
            {
                return _facade != null && _facade.Troops != null && _facade.Troops.Current != null
                    ? _facade.Troops.Current.Id
                    : -1;
            }
            catch
            {
                return -1;
            }
        }

        public bool IsLocalTurn()
        {
            try
            {
                return _facade != null && _facade.Teams != null && _facade.Teams.IsCurrentLocal;
            }
            catch
            {
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

        public CombatSnapshot BuildSnapshot()
        {
            Vector2Int size = _facade != null && _facade.Level != null ? _facade.Level.Size : Vector2Int.zero;
            return new CombatSnapshot(size, this);
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
            tile.Entity = GetAttackableEntityAt(point);
            AddDangerousMapEffects(point, tile);
            tile.DecorativeFeature = GetDecorativeFeatureAt(point, tile.Entity);
            return tile;
        }

        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            if (_facade == null || _facade.Level == null)
            {
                return snapshot;
            }

            InitializeCombatScannerCategories(snapshot);
            AddCombatTroopScannerResults(snapshot);
            AddCombatEntityScannerResults(snapshot);
            AddCombatTerrainScannerResults(snapshot);
            return snapshot;
        }

        private static void InitializeCombatScannerCategories(ScannerSnapshot snapshot)
        {
            ScannerCategory troops = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Troops));
            troops.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.All));
            troops.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Friendly));
            troops.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Enemy));

            ScannerCategory entities = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Entities));
            entities.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.All));
            entities.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.FriendlyGates));
            entities.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.EnemyGates));
            entities.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Attackable));
            entities.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Dangerous));

            ScannerCategory terrain = snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Terrain));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.ElevatedGround, 1));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.ElevatedGround, 2));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.ElevatedGround, 3));
            terrain.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.ImpassableTerrain));

            snapshot.GetOrAddCategory(ModText.Get(ModStrings.Scanner.Obstacles))
                .GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.Blocked));
        }

        private void AddCombatTroopScannerResults(ScannerSnapshot snapshot)
        {
            AddCombatTroopScannerResults(snapshot, friendly: true);
            AddCombatTroopScannerResults(snapshot, friendly: false);
        }

        private void AddCombatTroopScannerResults(ScannerSnapshot snapshot, bool friendly)
        {
            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    if (tile.Troop != null && IsFriendlyTroop(tile.Troop) == friendly)
                    {
                        ScannerResult result = new ScannerResult(
                            ScannerTileKey(friendly ? "troop:friendly" : "troop:enemy", point),
                            FormatTroopGridLabel(tile.Troop),
                            point);
                        snapshot.Add(ModText.Get(ModStrings.Scanner.Troops), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                        snapshot.Add(ModText.Get(ModStrings.Scanner.Troops), friendly ? ModText.Get(ModStrings.Scanner.Friendly) : ModText.Get(ModStrings.Scanner.Enemy), result);
                    }
                }
            }
        }

        private void AddCombatEntityScannerResults(ScannerSnapshot snapshot)
        {
            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    IMapEntity mapEntity = _facade.MapEntities != null ? _facade.MapEntities.GetAtIncludingNonBlockers(point) : null;
                    if (mapEntity != null && mapEntity.IsEnabled && mapEntity.IsVisibleInGame)
                    {
                        if (mapEntity.Category == MapEntityCategory.TownWallGate)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKey(IsFriendlyMapEntity(mapEntity) ? "gate:friendly" : "gate:enemy", point),
                                GetMapEntityName(mapEntity),
                                point);
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), IsFriendlyMapEntity(mapEntity) ? ModText.Get(ModStrings.Scanner.FriendlyGates) : ModText.Get(ModStrings.Scanner.EnemyGates), result);
                        }
                        else if (tile.Entity != null)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKey("entity:attackable", point),
                                GetMapEntityName(tile.Entity),
                                point);
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), ModText.Get(ModStrings.Scanner.Attackable), result);
                        }
                        else if (tile.MapEffects.Count > 0)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKey("entity:dangerous", point),
                                GetMapEntityName(mapEntity),
                                point);
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), ModText.Get(ModStrings.Scanner.All), CloneResult(result));
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Entities), ModText.Get(ModStrings.Scanner.Dangerous), result);
                        }
                    }
                }
            }
        }

        private void AddCombatTerrainScannerResults(ScannerSnapshot snapshot)
        {
            for (int elevation = 1; elevation <= 3; elevation++)
            {
                for (int y = 0; y < _facade.Level.Size.y; y++)
                {
                    for (int x = 0; x < _facade.Level.Size.x; x++)
                    {
                        Vector2Int point = new Vector2Int(x, y);
                        CombatTile tile = GetTile(point);
                        if (tile == null)
                        {
                            continue;
                        }

                        if (tile.Elevation == elevation)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKey("terrain:elevated:" + elevation, point),
                                ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, elevation),
                                point)
                            {
                                Kind = ScannerResultKind.TerrainPoint
                            };
                            snapshot.Add(ModText.Get(ModStrings.Scanner.Terrain), ModText.Get(ModStrings.Scanner.ElevatedGround, elevation), result);
                        }
                    }
                }
            }

            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    if (tile.IsImpassable)
                    {
                        ScannerResult result = new ScannerResult(
                            ScannerTileKey("terrain:impassable", point),
                            ModText.Get(ModStrings.Spatial.Impassable),
                            point)
                        {
                            Kind = ScannerResultKind.TerrainPoint
                        };
                        snapshot.Add(ModText.Get(ModStrings.Scanner.Terrain), ModText.Get(ModStrings.Scanner.ImpassableTerrain), result);
                    }

                    if (tile.IsBlocked)
                    {
                        snapshot.Add(ModText.Get(ModStrings.Scanner.Obstacles), ModText.Get(ModStrings.Scanner.Blocked),
                            new ScannerResult(ScannerTileKey("obstacle:blocked", point), ModText.Get(ModStrings.Spatial.Blocked), point));
                    }
                }
            }
        }

        private static string ScannerTileKey(string prefix, Vector2Int point)
        {
            return prefix + ":" + point.x + ":" + point.y;
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

        public bool ValidateScannerResult(ScannerResult result)
        {
            return result != null && IsValidTile(result.Position);
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
            _cursorManager?.SetCurrentTile(point);
            _gridManager?.SetCurrentTile(point, path);
            _pathManager?.SetCurrentTile(point, path);
            _highlightManager?.SetCurrentTile(point);
            if (GetTargetingMode() != CombatTargetingMode.None || IsAnySpellCastingStateActive())
            {
                return;
            }

            _cursorManager?.SetState(BattleCursorManager.State.CurrentTroop);
            _gridManager?.SetState(BattleGridManager.State.CurrentTroop);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.CurrentTroop);
            _attackPreviewHandler?.Hide();
            CombatTile tile = GetTile(point);
            if (tile != null && tile.Troop != null)
            {
                SynchronizeNativeHoverForPreview(point);
            }
            else if (tile != null && tile.Entity != null)
            {
                SynchronizeNativeHoverForPreview(point);
            }
        }

        public void AttachSpellTargetingNarration()
        {
            if (_battleHudSignals == null || _targetInstructionHandler != null)
            {
                return;
            }

            _targetInstructionHandler = HandleTargetInstruction;
            _spellTargetingEndHandler = HandleSpellTargetingEnd;
            _battleHudSignals.OnRequestTargetInstruction =
                (Action<ISpellDefinition, string>)Delegate.Combine(_battleHudSignals.OnRequestTargetInstruction, _targetInstructionHandler);
            _battleHudSignals.OnControllerCancelCast =
                (Action)Delegate.Combine(_battleHudSignals.OnControllerCancelCast, _spellTargetingEndHandler);
            _battleHudSignals.OnSpellbookCancelCast =
                (Action)Delegate.Combine(_battleHudSignals.OnSpellbookCancelCast, _spellTargetingEndHandler);
            _battleHudSignals.OnSpellEffectComplete =
                (Action)Delegate.Combine(_battleHudSignals.OnSpellEffectComplete, _spellTargetingEndHandler);
        }

        public void DetachSpellTargetingNarration()
        {
            if (_battleHudSignals == null || _targetInstructionHandler == null)
            {
                return;
            }

            _battleHudSignals.OnRequestTargetInstruction =
                (Action<ISpellDefinition, string>)Delegate.Remove(_battleHudSignals.OnRequestTargetInstruction, _targetInstructionHandler);
            if (_spellTargetingEndHandler != null)
            {
                _battleHudSignals.OnControllerCancelCast =
                    (Action)Delegate.Remove(_battleHudSignals.OnControllerCancelCast, _spellTargetingEndHandler);
                _battleHudSignals.OnSpellbookCancelCast =
                    (Action)Delegate.Remove(_battleHudSignals.OnSpellbookCancelCast, _spellTargetingEndHandler);
                _battleHudSignals.OnSpellEffectComplete =
                    (Action)Delegate.Remove(_battleHudSignals.OnSpellEffectComplete, _spellTargetingEndHandler);
            }
            _targetInstructionHandler = null;
            _spellTargetingEndHandler = null;
        }

        public void AttachSpellCastBegin(Action handler)
        {
            if (_battleHudSignals == null || handler == null || _beginCastHandler != null)
            {
                return;
            }

            _beginCastHandler = _ => handler();
            _battleHudSignals.OnBeginCast =
                (Action<ISpellDefinition>)Delegate.Combine(_battleHudSignals.OnBeginCast, _beginCastHandler);
        }

        public void DetachSpellCastBegin()
        {
            if (_battleHudSignals == null || _beginCastHandler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginCast =
                (Action<ISpellDefinition>)Delegate.Remove(_battleHudSignals.OnBeginCast, _beginCastHandler);
            _beginCastHandler = null;
        }

        public void AttachAbilityTargetingBegin(Action<TroopAbilityTargeting> handler)
        {
            if (_battleHudSignals == null || handler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Combine(_battleHudSignals.OnBeginAbilityTargeting, handler);
        }

        public void DetachAbilityTargetingBegin(Action<TroopAbilityTargeting> handler)
        {
            if (_battleHudSignals == null || handler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Remove(_battleHudSignals.OnBeginAbilityTargeting, handler);
        }

        public void AttachAbilityTargetingEnd(Action handler)
        {
            if (_battleHudSignals == null || handler == null || _endAbilityTargetingHandler != null)
            {
                return;
            }

            _endAbilityTargetingHandler = _ => handler();
            _battleHudSignals.OnEndAbilityTargeting =
                (Action<bool>)Delegate.Combine(_battleHudSignals.OnEndAbilityTargeting, _endAbilityTargetingHandler);
        }

        public void DetachAbilityTargetingEnd()
        {
            if (_battleHudSignals == null || _endAbilityTargetingHandler == null)
            {
                return;
            }

            _battleHudSignals.OnEndAbilityTargeting =
                (Action<bool>)Delegate.Remove(_battleHudSignals.OnEndAbilityTargeting, _endAbilityTargetingHandler);
            _endAbilityTargetingHandler = null;
        }

        public void AnnounceVisibleSpellTargetInstruction()
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell)
            {
                return;
            }

            string text = Hud != null ? Hud.TargetingInstructionText : string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        public string BuildAbilityTargetInstruction(TroopAbilityTargeting targeting)
        {
            IBattleTroopState current = GetCurrentTroop();
            ITroopAbilityDefinition ability = current != null && _abilityUtility != null
                ? _abilityUtility.GetAbilityDefinition(current)
                : null;
            string abilityName = ability != null ? SpeechTextSanitizer.Normalize(Localize(ability.NameKey)) : string.Empty;
            string instruction = SpeechTextSanitizer.Normalize(Localize("Battle/AbilityTargeting/" + targeting));
            if (!string.IsNullOrWhiteSpace(abilityName) && !string.IsNullOrWhiteSpace(instruction))
            {
                return abilityName + ": " + instruction;
            }

            return !string.IsNullOrWhiteSpace(abilityName) ? abilityName : instruction;
        }

        public CombatTargetingMode GetTargetingMode()
        {
            if (_battleSpellController != null)
            {
                HumanBattleSpellController.State state = _battleSpellController.CurrentState;
                if (state == HumanBattleSpellController.State.CastingBacteriaSpell
                    || state == HumanBattleSpellController.State.CastingTeleportSpell
                    || state == HumanBattleSpellController.State.CastingSummonSpell)
                {
                    return CombatTargetingMode.Spell;
                }
            }

            if (_humanBattleController != null
                && _humanBattleController.StateMachine != null
                && _humanBattleController.StateMachine.CurrentStateType == HumanBattleController.State.ChoosingAbilityTarget)
            {
                return CombatTargetingMode.Ability;
            }

            return CombatTargetingMode.None;
        }

        private bool IsAnySpellCastingStateActive()
        {
            if (GetTargetingMode() == CombatTargetingMode.Spell)
            {
                return true;
            }

            return _humanBattleController != null
                && _humanBattleController.StateMachine != null
                && _humanBattleController.StateMachine.CurrentStateType == HumanBattleController.State.CastingSpell;
        }

        public bool IsSpellTargetSelected(Vector2Int point)
        {
            return CountSpellTargetSelections(point) > 0;
        }

        private int CountSpellTargetSelections(Vector2Int point)
        {
            if (_battleSpellController == null || _battleSpellController.SelectedTargets == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _battleSpellController.SelectedTargets.Count; i++)
            {
                if (_battleSpellController.SelectedTargets[i] == point)
                {
                    count++;
                }
            }

            return count;
        }

        public void FocusTargetTile(Vector2Int point)
        {
            CombatTargetingMode mode = GetTargetingMode();
            if (mode == CombatTargetingMode.None || !IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForInput(point);
            if (mode == CombatTargetingMode.Spell)
            {
                _battleSpellController?.SetCurrentTile(point);
            }
            else if (mode == CombatTargetingMode.Ability)
            {
                UpdateNativeAttackPreviews();
            }
        }

        public bool ConfirmSpellTarget(Vector2Int point)
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell || !IsValidTile(point))
            {
                return false;
            }

            int previousSelectionCount = CountSpellTargetSelections(point);
            FocusTargetTile(point);
            if (_spellPrimaryClickMethod == null || _mouseKeyboardSpellInputModule == null)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter cannot confirm spell target because native HandlePrimaryClick was not found");
                return false;
            }

            try
            {
                _spellPrimaryClickMethod.Invoke(_mouseKeyboardSpellInputModule, null);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to invoke native spell primary click: " + exception.Message);
                return false;
            }

            int selectionCount = CountSpellTargetSelections(point);
            if (selectionCount > previousSelectionCount)
            {
                SpeechPipeline.Output(new SpeechRequest(
                    selectionCount > 1
                        ? ModText.Get(ModStrings.UI.SelectedCount, selectionCount)
                        : ModText.Get(ModStrings.UI.Selected),
                    interrupt: false));
            }
            else if (selectionCount < previousSelectionCount && GetTargetingMode() == CombatTargetingMode.Spell)
            {
                SpeechPipeline.Output(new SpeechRequest(
                    selectionCount > 0
                        ? ModText.Get(ModStrings.UI.SelectedCount, selectionCount)
                        : ModText.Get(ModStrings.UI.Unselected),
                    interrupt: false));
            }

            return true;
        }

        public bool ConfirmAbilityTarget(Vector2Int point)
        {
            if (GetTargetingMode() != CombatTargetingMode.Ability || !IsValidTile(point))
            {
                return false;
            }

            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(point, out screenInputOverride))
            {
                return false;
            }

            try
            {
                FocusTargetTile(point);
                return InvokeNativeClick(_primaryClickMethod, "primary ability target");
            }
            finally
            {
                screenInputOverride.Restore();
            }
        }

        public bool CancelSpellTargeting()
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell || _battleSpellController == null)
            {
                return false;
            }

            _battleSpellController.HandleSpellCastCancelled();
            return true;
        }

        public bool CancelAbilityTargeting()
        {
            if (GetTargetingMode() != CombatTargetingMode.Ability || _battleHudSignals == null)
            {
                return false;
            }

            IBattleTroopState current = GetCurrentTroop();
            if (current == null || (_abilityUtility != null && !_abilityUtility.CanBeAborted(current)))
            {
                return false;
            }

            _battleHudSignals.OnEndAbilityTargeting?.Invoke(false);
            return true;
        }

        public CombatInspectContext BeginInspect(Vector2Int point)
        {
            CombatTile tile = GetTile(point);
            if (tile == null)
            {
                return null;
            }

            if (tile.Troop != null)
            {
                return BeginStackInspect(tile.Troop);
            }

            if (tile.Entity != null)
            {
                return BeginEntityInspect(tile.Entity);
            }

            return BeginPathInspect(point);
        }

        public void HandleSecondaryAction(Vector2Int point)
        {
            if (CancelSpellTargeting())
            {
                return;
            }

            InvokeNativeClickWithHover(point, _secondaryClickMethod, "secondary");
        }

        public void ExitInspect(Vector2Int point)
        {
            ClearNativeTooltip();
            FocusTile(point);
        }

        public void ClearNativeTooltip()
        {
            _tooltipUtility?.ClearSpecific();
            _attackPreviewHandler?.Hide();
        }

        public Tooltip GetInspectTooltip(CombatInspectContext context, Vector2Int focusedTile)
        {
            if (context != null && context.TooltipDetails != null && focusedTile == context.PinnedTile)
            {
                return CreateDetailsTooltip(
                    context.TooltipDetails,
                    context.PinnedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: IsEntityInspectMode(context.Mode));
            }

            if (context != null)
            {
                return null;
            }

            CombatTile tile = GetTile(focusedTile);
            if (tile == null)
            {
                return null;
            }

            if (tile.Troop != null && _tooltipUtility != null)
            {
                return CreateDetailsTooltip(
                    _tooltipUtility.GetInspectTroopDetails(tile.Troop),
                    focusedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: false);
            }

            if (tile.Entity != null)
            {
                return CreateDetailsTooltip(
                    BuildEntityDetails(tile.Entity),
                    focusedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: true);
            }

            if (tile.IsReachable)
            {
                return CreateDetailsTooltip(BuildTileDetails(focusedTile), focusedTile);
            }

            return null;
        }

        private Tooltip CreateDetailsTooltip(
            IDetails details,
            Vector2Int tile,
            bool includeAttackPreview = false,
            bool attackPreviewTargetIsEntity = false)
        {
            if (details == null)
            {
                return null;
            }

            DetailsTextUtility captured = DetailsTextUtility.Capture(details, _localization);
            List<string> textLines = new List<string>(captured.TextLines);
            List<TooltipAction> actions = BuildCombatTooltipActions(tile, captured.InstructionRows, textLines);
            return new Tooltip(
                () => includeAttackPreview ? BuildTooltipLinesWithAttackPreview(textLines, attackPreviewTargetIsEntity) : textLines,
                CreateScreenPointTooltipMetadata(details, tile),
                actions);
        }

        private IReadOnlyList<string> BuildTooltipLinesWithAttackPreview(IReadOnlyList<string> detailsLines, bool targetIsEntity)
        {
            List<string> previewLines = CaptureAttackPreviewLines(targetIsEntity);
            if (previewLines.Count == 0)
            {
                return detailsLines;
            }

            List<string> lines = new List<string>();
            lines.Add(ModText.Get(ModStrings.Spatial.AttackPreview));
            lines.AddRange(previewLines);
            if (detailsLines != null)
            {
                lines.AddRange(detailsLines);
            }

            return lines;
        }

        private static bool IsEntityInspectMode(CombatInspectMode mode)
        {
            return mode == CombatInspectMode.EntityPath || mode == CombatInspectMode.EntityOnly;
        }

        private List<string> CaptureAttackPreviewLines(bool targetIsEntity)
        {
            List<string> lines = new List<string>();
            List<BattleAttackPreview> previews = GetActiveAttackPreviews();
            for (int i = 0; i < previews.Count; i++)
            {
                BattleAttackPreview preview = previews[i];
                string damage = GetPreviewText(preview, _attackPreviewDamageTextField);
                string kills = GetPreviewText(preview, _attackPreviewKillsTextField);
                string additional = GetCapturedAdditionalText(preview);
                if (string.IsNullOrWhiteSpace(additional))
                {
                    additional = GetPreviewText(preview, _attackPreviewAdditionalTextField);
                }
                bool hasDamage = IsPreviewContainerVisible(preview, _attackPreviewDamageContainerField)
                    && !string.IsNullOrWhiteSpace(damage);
                bool hasKills = IsPreviewContainerVisible(preview, _attackPreviewKillsContainerField)
                    && !string.IsNullOrWhiteSpace(kills);

                List<string> parts = new List<string>();
                string prefix = previews.Count > 1
                    ? (i == 0 ? ModText.Get(ModStrings.Spatial.PrimaryPrefix) : ModText.Get(ModStrings.Spatial.ExtraTargetPrefix))
                    : string.Empty;
                if (hasDamage)
                {
                    parts.Add(ModText.Get(ModStrings.Spatial.DamagePreview, prefix, damage));
                }

                if (hasKills)
                {
                    parts.Add(targetIsEntity ? FormatEntityDestruction(kills) : ModText.Get(ModStrings.Spatial.Kills, kills));
                }

                if (parts.Count > 0)
                {
                    lines.Add(string.Join(", ", parts.ToArray()) + ".");
                }

                if (!string.IsNullOrWhiteSpace(additional))
                {
                    lines.Add(additional + ".");
                }
            }

            return lines;
        }

        private List<BattleAttackPreview> GetActiveAttackPreviews()
        {
            List<BattleAttackPreview> previews = new List<BattleAttackPreview>();
            if (_attackPreviewHandler == null || _attackPreviewPoolField == null)
            {
                return previews;
            }

            try
            {
                object pool = _attackPreviewPoolField.GetValue(_attackPreviewHandler);
                if (pool == null)
                {
                    return previews;
                }

                MethodInfo getActive = AccessTools.Method(pool.GetType(), "GetActive");
                IEnumerable active = getActive != null ? getActive.Invoke(pool, null) as IEnumerable : null;
                if (active == null)
                {
                    return previews;
                }

                foreach (object item in active)
                {
                    BattleAttackPreview preview = item as BattleAttackPreview;
                    if (preview != null)
                    {
                        previews.Add(preview);
                    }
                }
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to capture attack preview text: " + exception.Message);
            }

            return previews;
        }

        private bool IsPreviewContainerVisible(BattleAttackPreview preview, FieldInfo field)
        {
            GameObject container = preview != null && field != null ? field.GetValue(preview) as GameObject : null;
            return container != null && container.activeSelf;
        }

        private string GetPreviewText(BattleAttackPreview preview, FieldInfo field)
        {
            UITextMesh text = preview != null && field != null ? field.GetValue(preview) as UITextMesh : null;
            return text != null ? TrimSentence(SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text))) : string.Empty;
        }

        private static string GetCapturedAdditionalText(BattleAttackPreview preview)
        {
            string text;
            return preview != null && AttackPreviewAdditionalTexts.TryGetValue(preview, out text)
                ? TrimSentence(text)
                : string.Empty;
        }

        private static string FormatEntityDestruction(string killsText)
        {
            int min;
            int max;
            if (TryParseRange(killsText, out min, out max))
            {
            return min <= 0 && max > 0
                ? ModText.Get(ModStrings.Spatial.MayDestroy)
                : ModText.Get(ModStrings.Spatial.Destroys);
            }

            return ModText.Get(ModStrings.Spatial.Destroys);
        }

        private static bool TryParseRange(string text, out int min, out int max)
        {
            min = 0;
            max = 0;
            MatchCollection matches = Regex.Matches(text ?? string.Empty, "\\d+");
            if (matches.Count == 0)
            {
                return false;
            }

            min = int.Parse(matches[0].Value);
            max = matches.Count > 1 ? int.Parse(matches[1].Value) : min;
            return true;
        }

        private static string TrimSentence(string text)
        {
            text = text != null ? text.Trim() : string.Empty;
            while (text.EndsWith(".", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text;
        }

        private VisualTooltipMetadata CreateScreenPointTooltipMetadata(IDetails details, Vector2Int tile)
        {
            ITooltipable tooltipable = GetBattleTooltipable();
            if (tooltipable == null)
            {
                return null;
            }

            return new VisualTooltipMetadata(tooltipable, GetScreenPoint(tile), details);
        }

        private ITooltipable GetBattleTooltipable()
        {
            if (_tooltipBehaviorField == null || _tooltipUtility == null)
            {
                return null;
            }

            try
            {
                return _tooltipBehaviorField.GetValue(_tooltipUtility) as ITooltipable;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to resolve battle tooltip behavior: " + exception.Message);
                return null;
            }
        }

        private List<TooltipAction> BuildCombatTooltipActions(
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

                if (!IsSecondaryCombatInstruction(row.InputType))
                {
                    continue;
                }

                Vector2Int capturedTile = tile;
                RemoveExactLine(textLines, row.Text);
                actions.Add(new TooltipAction(row.Text, () => InvokeSecondaryTooltipAction(capturedTile)));
            }

            return actions;
        }

        private bool IsSecondaryCombatInstruction(InputType inputType)
        {
            if (_inputManager != null)
            {
                return inputType == InputType.GetRightMouseClickOrCursorConfirm(_inputManager);
            }

            return inputType == InputType.RightMouseClickOrCursorConfirm;
        }

        private bool InvokeSecondaryTooltipAction(Vector2Int tile)
        {
            if (_secondaryClickMethod == null || _mouseKeyboardInputModule == null)
            {
                return false;
            }

            HandleSecondaryAction(tile);
            return true;
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

        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsPresent())
            {
                return;
            }

            try
            {
                EnsureCursorOverlay();
                if (_cursorOverlay == null || _cursorOverlaySegments == null)
                {
                    return;
                }

                SetScreenOverlayPosition(GetScreenPoint(tile));
                _cursorOverlay.SetActive(true);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to set focused tile overlay: " + exception.Message);
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
                _cursorOverlaySegments = null;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to clear focused tile overlay: " + exception.Message);
            }
        }

        public string DescribeTile(CombatTile tile, CombatInspectContext context)
        {
            return new CombatTileSpeechFormatter(this, context).DescribeTile(tile);
        }

        private void SynchronizeNativeHoverForInput(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            CombatTile tile = GetTile(point);
            PathNode[] path = GetPathTo(point);

            _cursorManager?.SetCurrentTile(point);
            _gridManager?.SetCurrentTile(point, path);
            _pathManager?.SetCurrentTile(point, path);
            _highlightManager?.SetCurrentTile(point);

            if (_humanBattleController == null || tile == null)
            {
                return;
            }

            _humanBattleController.CurrentHoverTile = point;
            _humanBattleController.CurrentTroopAtPosition = tile.Troop;
            _humanBattleController.TroopToInspect = tile.Troop;
            _humanBattleController.EntityToInspect = tile.Entity;
            _humanBattleController.TileToInspect = new int2(point.x, point.y);
            _humanBattleController.PathToCurrentTile = (!tile.IsImpassable && !tile.IsBlocked) ? path : null;
            _humanBattleController.EnemiesWithinMeleeReach = _facade.Level.AllEnemiesWithinMeleeReach(_facade.Troops.Current).ToList();
            _humanBattleController.MapEntitiesWithinMeleeReach = _facade.Level.AllMapEntitiesWithinMeleeReach(_facade.Troops.Current).ToList();

            HumanBattleController.State currentState = _humanBattleController.StateMachine.CurrentStateType;
            if (currentState == HumanBattleController.State.ChoosingAbilityTarget)
            {
                return;
            }

            if (IsAnySpellCastingStateActive())
            {
                return;
            }

            // This is native hover synchronization for mouse-equivalent input.
            // It is intentionally separate from CombatHexGrid's accessibility inspect mode.
            if (tile.Troop != null)
            {
                if (currentState == HumanBattleController.State.InspectTroop
                    && _humanBattleController.TroopToInspect != null
                    && _humanBattleController.TroopToInspect.Id == tile.Troop.Id)
                {
                    return;
                }

                _gridManager?.SetInspectedTroop(tile.Troop);
                _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
                _gridManager?.SetState(BattleGridManager.State.InspectTroop);
                _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
                _pathManager?.SetState(BattlePathManager.State.InspectTroop);
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.InspectTroop);
            }
            else if (tile.Entity != null)
            {
                if (currentState == HumanBattleController.State.InspectEntity
                    && _humanBattleController.EntityToInspect != null
                    && _humanBattleController.EntityToInspect.Id == tile.Entity.Id)
                {
                    return;
                }

                _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
                _gridManager?.SetState(BattleGridManager.State.InspectEntity);
                _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
                _pathManager?.SetState(BattlePathManager.State.InspectEntity);
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.InspectEntity);
            }
            else
            {
                if (currentState == HumanBattleController.State.InspectTile && IsNativeTileToInspect(point))
                {
                    return;
                }

                _cursorManager?.SetState(BattleCursorManager.State.CurrentTroop);
                _gridManager?.SetState(BattleGridManager.State.CurrentTroop);
                _highlightManager?.SetState(BattleHighlightManager.State.CurrentTroop);
                _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.ShowCurrentTroop);
            }
        }

        private void HandleTargetInstruction(ISpellDefinition spell, string instruction)
        {
            string spellName = spell != null ? SpeechTextSanitizer.Normalize(Localize(spell.NameKey)) : string.Empty;
            instruction = SpeechTextSanitizer.Normalize(instruction);
            string text = !string.IsNullOrWhiteSpace(spellName) && !string.IsNullOrWhiteSpace(instruction)
                ? spellName + ": " + instruction
                : (!string.IsNullOrWhiteSpace(spellName) ? spellName : instruction);
            Hud?.SetSpellTargetInstructionText(text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        private void HandleSpellTargetingEnd()
        {
            Hud?.ClearSpellTargetInstructionText();
        }

        private bool IsNativeTileToInspect(Vector2Int point)
        {
            if (_humanBattleController == null)
            {
                return false;
            }

            int2 tile = _humanBattleController.TileToInspect;
            return tile.x == point.x && tile.y == point.y;
        }

        private void InvokeNativeClickWithHover(Vector2Int point, MethodInfo clickMethod, string clickName)
        {
            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(point, out screenInputOverride))
            {
                return;
            }

            try
            {
                if (_updateCurrentTileMethod != null)
                {
                    _updateCurrentTileMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
                }

                SynchronizeNativeHoverForInput(point);
                InvokeNativeClick(clickMethod, clickName);
            }
            finally
            {
                screenInputOverride.Restore();
            }
        }

        private bool InvokeNativeClick(MethodInfo clickMethod, string clickName)
        {
            if (clickMethod == null || _mouseKeyboardInputModule == null)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter cannot emulate " + clickName + " click because the native mouse input module was not resolved.");
                return false;
            }

            try
            {
                clickMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
                return true;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to emulate native " + clickName + " click: " + exception.Message);
                return false;
            }
        }

        private IDetails BuildTileDetails(Vector2Int point)
        {
            PathNode[] path = GetPathTo(point);
            if (!PathfinderExtensions.GetIsValid(path))
            {
                return null;
            }

            PathNode finalNode = path[path.Length - 1];
            Vector2Int finalPoint = ToVector2Int(finalNode.point);
            bool mothersLove = _facade.Troops.GetRootSpreadersForTile(finalPoint, _facade.Teams.Current.Id)
                .Any(troop => troop.HasBacteria((BacteriaTypes)1419));
            bool mothersHate = _facade.Troops.GetRootSpreadersForTile(finalPoint, _facade.Teams.GetOtherTeamId(_facade.Teams.Current.Id))
                .Any(troop => troop.IsHatingMother());

            return new BattleTileDetails(_inputManager)
            {
                MovementLeft = _facade.Troops.Current.MovesLeft,
                TravelCost = finalNode.flooredTravelCost,
                MothersLove = mothersLove,
                MothersHate = mothersHate,
                Point = finalNode.point
            };
        }

        private bool TryBeginScreenInputOverride(Vector2Int tilePosition, out ScreenInputOverride screenInputOverride)
        {
            screenInputOverride = null;
            if (_inputManager == null || _inputManager.Screen == null || _inputManager.Screen.Primary == null)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            object response = ResolveWritableScreenInputResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
                return false;
            }

            Vector2 screenPosition = GetScreenPoint(tilePosition);
            if (screenPosition.x < 0f
                || screenPosition.y < 0f
                || screenPosition.x > Screen.width
                || screenPosition.y > Screen.height)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter could not target tile " + FormatDiagnosticPoint(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
                return false;
            }

            screenInputOverride = ScreenInputOverride.Apply(response, screenPosition);
            return screenInputOverride != null;
        }

        private static object ResolveWritableScreenInputResponse(object response)
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

        private CombatInspectContext BeginStackInspect(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return null;
            }

            _gridManager?.SetInspectedTroop(troop);
            _cursorManager?.SetCurrentTile(troop.Position);
            _gridManager?.SetCurrentTile(troop.Position, GetPathTo(troop.Position));
            _highlightManager?.SetCurrentTile(troop.Position);
            _gridManager?.SetState(BattleGridManager.State.InspectTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
            _pathManager?.SetCurrentTile(troop.Position, GetPathTo(troop.Position));
            _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
            _pathManager?.SetState(BattlePathManager.State.InspectTroop);
            SynchronizeNativeHoverForPreview(troop.Position);

            CombatInspectContext context = CombatInspectContext.ForStack(troop.Position);
            context.TargetLabel = DescribeTroopForSpeech(troop);
            BuildStackRanges(troop, context);
            context.TooltipDetails = _tooltipUtility != null ? _tooltipUtility.GetInspectTroopDetails(troop) : null;
            return context;
        }

        private CombatInspectContext BeginPathInspect(Vector2Int point)
        {
            if (!IsReachable(point))
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.NotInMovementRange), interrupt: false));
                return null;
            }

            PathNode[] path = GetPathTo(point);
            _cursorManager?.SetCurrentTile(point);
            _gridManager?.SetCurrentTile(point, path);
            _pathManager?.SetCurrentTile(point, path);
            _highlightManager?.SetCurrentTile(point);
            _cursorManager?.SetState(BattleCursorManager.State.CurrentTroop);
            _gridManager?.SetState(BattleGridManager.State.CurrentTroop);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.CurrentTroop);
            _attackPreviewHandler?.Hide();
            CombatInspectContext context = CombatInspectContext.ForPath(point, ConvertPath(path));
            context.TargetLabel = DescribeTile(GetTile(point), null);
            context.TooltipDetails = BuildTileDetails(point);
            return context;
        }

        private CombatInspectContext BeginEntityInspect(IMapEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            PathNode[] path = GetPathToEntity(entity);
            _cursorManager?.SetCurrentTile(entity.Position);
            _gridManager?.SetCurrentTile(entity.Position, path);
            _pathManager?.SetCurrentTile(entity.Position, path);
            _highlightManager?.SetCurrentTile(entity.Position);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
            _gridManager?.SetState(BattleGridManager.State.InspectEntity);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            SynchronizeNativeHoverForPreview(entity.Position);

            CombatInspectContext context = PathfinderExtensions.IsReachable(path, GetCurrentMovesLeft(), true)
                ? CombatInspectContext.ForEntityPath(entity.Position, ConvertPath(path))
                : CombatInspectContext.ForEntityOnly(entity.Position);
            context.TargetLabel = DescribeEntityForSpeech(entity);
            context.TooltipDetails = BuildEntityDetails(entity);
            return context;
        }

        private void SynchronizeNativeHoverForPreview(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            CombatTile tile = GetTile(point);
            if (tile == null)
            {
                return;
            }

            SynchronizeNativeHoverForInput(point);

            if (_humanBattleController == null)
            {
                return;
            }

            if (_facade != null && _facade.Level != null && _facade.Troops != null && _facade.Troops.Current != null)
            {
                _humanBattleController.EnemiesWithinMeleeReach = _facade.Level.AllEnemiesWithinMeleeReach(_facade.Troops.Current).ToList();
                _humanBattleController.MapEntitiesWithinMeleeReach = _facade.Level.AllMapEntitiesWithinMeleeReach(_facade.Troops.Current).ToList();
            }

            UpdateNativeAttackPreviews();
        }

        private void UpdateNativeAttackPreviews()
        {
            if (_mouseKeyboardInputModule == null || _updateAttackPreviewsMethod == null)
            {
                return;
            }

            try
            {
                _updateAttackPreviewsMethod.Invoke(_mouseKeyboardInputModule, null);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to update native attack previews: " + exception.Message);
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
            catch
            {
            }

            try
            {
                if (_facade.Troops.GetIsWithinAttackRange(troop, point, DamageType.Melee))
                {
                    indicators.Add(CombatRangeIndicator.Melee);
                }
            }
            catch
            {
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
            catch
            {
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
            catch
            {
                return false;
            }
        }

        internal void AddEnemyInfluenceForSpeech(Vector2Int point, IBattleTroopState occupyingTroop, List<string> parts)
        {
            string influence = DescribeEnemyInfluenceForSpeech(point, occupyingTroop);
            if (!string.IsNullOrWhiteSpace(influence))
            {
                parts.Add(influence);
            }
        }

        internal string DescribeEnemyInfluenceForSpeech(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            return CombatInfluenceFormatter.Format(BuildEnemyInfluenceSources(point, occupyingTroop));
        }

        internal bool IsThreatenedByEnemy(Vector2Int point, IBattleTroopState occupyingTroop)
        {
            return BuildEnemyInfluenceSources(point, occupyingTroop).Count > 0;
        }

        private List<CombatInfluenceSource> BuildEnemyInfluenceSources(Vector2Int point, IBattleTroopState occupyingTroop)
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
            catch
            {
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
            catch
            {
                return false;
            }
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
            catch
            {
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

        internal string DescribeTroopForSpeech(IBattleTroopState troop)
        {
            return FormatTroopGridLabel(troop);
        }

        internal string DescribeEntityForSpeech(IMapEntity entity)
        {
            string name = GetMapEntityName(entity);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ModText.Get(ModStrings.Combat.AttackableEntity);
            }

            IHealthComponent health = entity.GetComponent<IHealthComponent>();
            if (health == null)
            {
                return name;
            }

            return MenuButtonTextUtility.JoinParts(
                name,
                ModText.Get(ModStrings.Spatial.Health, health.HealthLeft, health.MaxHealth.GetValue()));
        }

        public string FormatTroopGridLabel(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return string.Empty;
            }

            string label = FormatTroopLabel(troop, troop.Stats.Size, includeHealth: true, includePosition: false);
            IBattleTroopState current = GetCurrentTroop();
            return current != null && current.Id == troop.Id
                ? MenuButtonTextUtility.JoinParts(ModText.Get(ModStrings.Spatial.Acting), label)
                : label;
        }

        public string FormatTroopEventLabel(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return ModText.Get(ModStrings.Combat.UnknownTroop);
            }

            return FormatTroopLabel(troop, troop.Stats.Size, includeHealth: false, includePosition: true);
        }

        public string FormatTroopEventLabel(IBattleTroopState troop, int sizeOverride)
        {
            if (troop == null)
            {
                return ModText.Get(ModStrings.Combat.UnknownTroop);
            }

            return FormatTroopLabel(troop, sizeOverride, includeHealth: false, includePosition: true);
        }

        public string FormatTroopEventLabel(IBattleTroopState troop, int sizeOverride, Vector2Int positionOverride)
        {
            if (troop == null)
            {
                return ModText.Get(ModStrings.Combat.UnknownTroop);
            }

            string label = FormatTroopLabel(troop, sizeOverride, includeHealth: false, includePosition: false);
            return ModText.Get(ModStrings.Combat.TroopAt, label, FormatPoint(positionOverride));
        }

        public string FormatEntityEventLabel(IMapEntity entity)
        {
            if (entity == null)
            {
                return ModText.Get(ModStrings.Combat.UnknownEntity);
            }

            string name = GetMapEntityName(entity);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ModText.Get(ModStrings.Combat.AttackableEntity);
            }

            return ModText.Get(ModStrings.Combat.TroopAt, name, FormatPoint(entity.Position));
        }

        public TroopRef CreateTroopRef(IBattleTroopState troop)
        {
            return CreateTroopRef(troop, troop != null ? troop.Stats.Size : 0, troop != null ? troop.Position : Vector2Int.zero);
        }

        public TroopRef CreateTroopRef(IBattleTroopState troop, int sizeOverride)
        {
            return CreateTroopRef(troop, sizeOverride, troop != null ? troop.Position : Vector2Int.zero);
        }

        public TroopRef CreateTroopRef(IBattleTroopState troop, int sizeOverride, Vector2Int positionOverride)
        {
            if (troop == null)
            {
                return new TroopRef(-1, -1, GetLocalTeamId(), ModText.Get(ModStrings.Combat.UnknownTroop), sizeOverride, positionOverride);
            }

            string name = SpeechTextSanitizer.Normalize(_facade.Troops.GetName(troop.Id, sizeOverride));
            return new TroopRef(troop.Id, troop.TeamId, GetLocalTeamId(), name, sizeOverride, positionOverride);
        }

        public EntityRef CreateEntityRef(IMapEntity entity)
        {
            if (entity == null)
            {
                return new EntityRef(-1, -1, ModText.Get(ModStrings.Combat.UnknownEntity), Vector2Int.zero);
            }

            string name = GetMapEntityName(entity);
            return new EntityRef(entity.Id, entity.BlueprintId, name, entity.Position);
        }

        public CommanderRef CreateCommanderRef(int commanderId)
        {
            try
            {
                ICommanderState commander = _facade != null && _facade.Commanders != null ? _facade.Commanders.Get(commanderId) : null;
                string name = _facade != null && _facade.Commanders != null ? _facade.Commanders.GetName(commanderId) : string.Empty;
                return new CommanderRef(commanderId, commander != null ? commander.TeamId : -1, GetLocalTeamId(), name);
            }
            catch
            {
                return new CommanderRef(commanderId, -1, GetLocalTeamId(), ModText.Get(ModStrings.Combat.Wielder));
            }
        }

        public SpellRef CreateSpellRef(SpellTypes spellType, int tier)
        {
            string name = LocalizeText("Spells/" + spellType);
            return new SpellRef(spellType, name, tier);
        }

        public AbilityRef CreateAbilityRef(TroopAbilityType abilityType)
        {
            string name = LocalizeText("TroopAbilities/" + abilityType);
            return new AbilityRef(abilityType, name);
        }

        public BacteriaRef CreateBacteriaRef(BacteriaReference bacteriaReference)
        {
            if (bacteriaReference == null)
            {
                return null;
            }

            string name = LocalizeText(BacteriaReferenceUtility.GetLocalizationNameKey(bacteriaReference.BacteriaType));
            return new BacteriaRef(bacteriaReference.Id, bacteriaReference.BacteriaType, name);
        }

        public BacteriaRef CreateBacteriaRef(BacteriaTypes bacteriaType)
        {
            string name = LocalizeText(BacteriaReferenceUtility.GetLocalizationNameKey(bacteriaType));
            return new BacteriaRef(-1, bacteriaType, name);
        }

        public int LocalTeamId
        {
            get { return GetLocalTeamId(); }
        }

        public IReadOnlyList<int> GetAliveBattleTroopIdsForSide(bool enemySide)
        {
            List<int> ids = new List<int>();
            try
            {
                if (_facade == null || _facade.Troops == null || _facade.Troops.All == null)
                {
                    return ids;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return ids;
                }

                foreach (IBattleTroopState troop in _facade.Troops.All)
                {
                    if (troop == null || !troop.GetIsAlive())
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    if (isEnemy == enemySide)
                    {
                        ids.Add(troop.Id);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        public IReadOnlyList<int> GetAliveMeleeBattleTroopIdsForSide(bool enemySide)
        {
            return GetAliveBattleTroopIdsForSide(enemySide, troop => troop.HasMeleeAttack() && !troop.HasRangedAttack());
        }

        public IReadOnlyList<int> GetAliveRangedBattleTroopIdsForSide(bool enemySide)
        {
            return GetAliveBattleTroopIdsForSide(enemySide, troop => troop.HasRangedAttack());
        }

        private IReadOnlyList<int> GetAliveBattleTroopIdsForSide(bool enemySide, Func<IBattleTroopState, bool> predicate)
        {
            List<int> ids = new List<int>();
            try
            {
                if (_facade == null || _facade.Troops == null || _facade.Troops.All == null)
                {
                    return ids;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return ids;
                }

                foreach (IBattleTroopState troop in _facade.Troops.All)
                {
                    if (troop == null || !troop.GetIsAlive())
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    if (isEnemy == enemySide && (predicate == null || predicate(troop)))
                    {
                        ids.Add(troop.Id);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        public string BuildLocalEssenceSummary()
        {
            CombatHudSide? side = GetLocalCombatHudSide();
            if (!side.HasValue)
            {
                return string.Empty;
            }

            return Hud != null && Hud.Commanders != null
                ? Hud.Commanders.BuildEssenceSummary(side.Value, requireVisible: false)
                : string.Empty;
        }

        public string BuildEnemyEssenceSummary()
        {
            CombatHudSide? side = GetEnemyCombatHudSide();
            if (!side.HasValue)
            {
                return string.Empty;
            }

            return Hud != null && Hud.Commanders != null
                ? Hud.Commanders.BuildEssenceSummary(side.Value, requireVisible: true)
                : string.Empty;
        }

        public string FormatCommanderEventLabel(int commanderId)
        {
            try
            {
                if (_facade == null || _facade.Commanders == null)
                {
                    return ModText.Get(ModStrings.Combat.Wielder);
                }

                ICommanderState commander = _facade.Commanders.Get(commanderId);
                string name = _facade.Commanders.GetName(commanderId);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = ModText.Get(ModStrings.Combat.Wielder);
                }

                int localTeamId = GetLocalTeamId();
                if (commander != null && localTeamId >= 0 && commander.TeamId != localTeamId)
                {
                    return ModText.Get(ModStrings.Screens.EnemyWielder, SpeechTextSanitizer.Normalize(name));
                }

                return SpeechTextSanitizer.Normalize(name);
            }
            catch
            {
                return ModText.Get(ModStrings.Combat.Wielder);
            }
        }

        private string FormatTroopLabel(IBattleTroopState troop, int size, bool includeHealth, bool includePosition)
        {
            string name = SpeechTextSanitizer.Normalize(_facade.Troops.GetName(troop.Id, size));
            int localTeamId = GetLocalTeamId();
            string label = localTeamId < 0 || troop.TeamId == localTeamId
                ? ModText.Get(ModStrings.Combat.TroopQuantity, size, name)
                : ModText.Get(ModStrings.Combat.EnemyTroop, size, name);

            if (includeHealth)
            {
                label = MenuButtonTextUtility.JoinParts(
                    label,
                    ModText.Get(ModStrings.Spatial.Health, troop.CurrentHealth, troop.Stats.MaxHealth.GetValue()));
            }

            if (includePosition)
            {
                label = ModText.Get(ModStrings.Combat.TroopAt, label, FormatPoint(troop.Position));
            }

            return label;
        }

        private int GetLocalTeamId()
        {
            try
            {
                if (_facade == null || _facade.Teams == null)
                {
                    return -1;
                }

                int localTeamId = _facade.Teams.LocalTeamIdInControl;
                if (localTeamId >= 0)
                {
                    return localTeamId;
                }

                return _facade.Teams.Current != null ? _facade.Teams.Current.Id : -1;
            }
            catch
            {
                return -1;
            }
        }

        private CombatHudSide? GetLocalCombatHudSide()
        {
            if (Hud == null || Hud.Commanders == null)
            {
                return null;
            }

            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return null;
            }

            if (Hud.Commanders.GetCommanderTeamId(CombatHudSide.Attacker) == localTeamId)
            {
                return CombatHudSide.Attacker;
            }

            if (Hud.Commanders.GetCommanderTeamId(CombatHudSide.Defender) == localTeamId)
            {
                return CombatHudSide.Defender;
            }

            return null;
        }

        private CombatHudSide? GetEnemyCombatHudSide()
        {
            CombatHudSide? localSide = GetLocalCombatHudSide();
            if (!localSide.HasValue)
            {
                return null;
            }

            return localSide.Value == CombatHudSide.Attacker ? CombatHudSide.Defender : CombatHudSide.Attacker;
        }

        public IBattleTroopState GetTroop(int troopId)
        {
            try
            {
                return _facade != null && _facade.Troops != null ? _facade.Troops.Get(troopId) : null;
            }
            catch
            {
                return null;
            }
        }

        public IMapEntity GetMapEntity(int entityId)
        {
            try
            {
                return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(entityId) : null;
            }
            catch
            {
                return null;
            }
        }

        public int GetCurrentRound()
        {
            try
            {
                return _facade != null && _facade.Queue != null ? _facade.Queue.CurrentRound : 0;
            }
            catch
            {
                return 0;
            }
        }

        public IReadOnlyList<int> GetLocalActingTroopIds()
        {
            return GetActingTroopIds(enemy: false);
        }

        public IReadOnlyList<int> GetEnemyActingTroopIds()
        {
            return GetActingTroopIds(enemy: true);
        }

        private IReadOnlyList<int> GetActingTroopIds(bool enemy)
        {
            List<int> troopIds = new List<int>();
            try
            {
                if (_facade == null
                    || _facade.Queue == null
                    || _facade.Troops == null
                    || _facade.Teams == null
                    || !_facade.Teams.IsCurrentLocal
                    || _facade.Queue.Count <= 0)
                {
                    return troopIds;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return troopIds;
                }

                int currentTroopId = _facade.Queue[0].Id;
                if (currentTroopId < 0)
                {
                    currentTroopId = GetCurrentTroopId();
                }

                if (currentTroopId < 0)
                {
                    return troopIds;
                }

                for (int i = 0; i < _facade.Queue.Count; i++)
                {
                    QueuedTroop queuedTroop = _facade.Queue[i];
                    if (i > 0 && queuedTroop.Id == currentTroopId)
                    {
                        break;
                    }

                    if (queuedTroop.Id < 0)
                    {
                        continue;
                    }

                    IBattleTroopState troop = GetTroop(queuedTroop.Id);
                    if (troop == null || !troop.GetIsAlive() || !IsValidTile(troop.Position))
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    if (isEnemy == enemy)
                    {
                        troopIds.Add(troop.Id);
                    }
                }
            }
            catch
            {
                return troopIds;
            }

            return troopIds;
        }

        public bool TryGetLocalActingTroopPosition(int troopId, out Vector2Int position)
        {
            return TryGetTroopPosition(troopId, out position, requireLocalCurrentTurn: true);
        }

        public bool TryGetTroopPosition(int troopId, out Vector2Int position, bool requireLocalCurrentTurn)
        {
            position = Vector2Int.zero;
            if (_facade == null || _facade.Teams == null)
            {
                return false;
            }

            if (requireLocalCurrentTurn && !_facade.Teams.IsCurrentLocal)
            {
                return false;
            }

            IBattleTroopState troop = GetTroop(troopId);
            if (troop == null)
            {
                return false;
            }

            int localTeamId = GetLocalTeamId();
            if (requireLocalCurrentTurn && localTeamId >= 0 && troop.TeamId != localTeamId)
            {
                return false;
            }

            position = troop.Position;
            return IsValidTile(position);
        }

        public string LocalizeText(string key)
        {
            return Localize(key);
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
                string customName = Localize(customNameKey);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return SpeechTextSanitizer.Normalize(customName);
                }
            }

            string localizedName = Localize(entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return SpeechTextSanitizer.Normalize(localizedName);
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return SpeechTextSanitizer.Normalize(entity.Name);
            }

            return SpeechTextSanitizer.Normalize(entity.NameKey);
        }

        private static bool IsDebris(IMapEntity entity)
        {
            return entity != null && (entity.BlueprintId == 24 || entity.BlueprintId == 25);
        }

        private string Localize(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || _localization == null)
            {
                return string.Empty;
            }

            try
            {
                string text = _localization.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? string.Empty : text;
            }
            catch
            {
                return string.Empty;
            }
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

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            Vector3 world = GetWorldCenter(tile);
            ICamera camera = _cameraLookup != null ? _cameraLookup.GetBrainCamera() : null;
            if (camera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 point = camera.WorldToScreenPoint(world);
            return new Vector2(point.x, point.y);
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
                    SocAccessPlugin.Instance?.LogWarning("CombatAdapter failed to resolve tile world position: " + exception.Message);
                }
            }

            return new Vector3(tile.x, 0f, tile.y);
        }

        private void EnsureCursorOverlay()
        {
            if (_cursorOverlay != null && _cursorOverlaySegments != null)
            {
                return;
            }

            _cursorOverlay = new GameObject("SongsOfConquestAccess_CombatCursor");
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
            if (segment == null)
            {
                return;
            }

            segment.anchoredPosition = position;
            segment.sizeDelta = size;
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
                    SocAccessPlugin.Instance?.LogWarning("CombatAdapter could not override native screen input because required writable properties were missing on " + responseType.FullName);
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

        internal static Vector2Int[] GetNeighbors(Vector2Int point)
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

        public static string FormatPoint(Vector2Int point)
        {
            return HexCoordinateFormatter.Format(point);
        }

        private static string FormatDiagnosticPoint(Vector2Int point)
        {
            return point.x + ", " + point.y;
        }

    }

    internal sealed class CombatSnapshot
    {
        private readonly CombatAdapter _adapter;

        public CombatSnapshot(Vector2Int size, CombatAdapter adapter)
        {
            Size = size;
            _adapter = adapter;
        }

        public Vector2Int Size { get; private set; }

        public bool IsValidTile(Vector2Int point)
        {
            return _adapter != null && _adapter.IsValidTile(point);
        }

        public CombatTile Get(Vector2Int point)
        {
            return _adapter != null ? _adapter.GetTile(point) : null;
        }
    }

    internal sealed class CombatTile
    {
        public CombatTile(Vector2Int point)
        {
            Point = point;
        }

        public Vector2Int Point { get; private set; }

        public byte Elevation { get; set; }

        public bool IsReachable { get; set; }

        public bool IsImpassable { get; set; }

        public bool IsBlocked { get; set; }

        public IBattleTroopState Troop { get; set; }

        public IMapEntity Entity { get; set; }

        public List<string> MapEffects { get; private set; } = new List<string>();

        public string DecorativeFeature { get; set; }
    }

    internal enum CombatRangeIndicator
    {
        Source,
        Movement,
        Attack,
        Deadly,
        Melee,
        ZoneOfControl
    }

    internal sealed class CombatInspectContext
    {
        private readonly Dictionary<Vector2Int, HashSet<CombatRangeIndicator>> _indicators =
            new Dictionary<Vector2Int, HashSet<CombatRangeIndicator>>();
        private readonly HashSet<Vector2Int> _reviewSet = new HashSet<Vector2Int>();

        private CombatInspectContext(CombatInspectMode mode, Vector2Int pinnedTile)
        {
            Mode = mode;
            PinnedTile = pinnedTile;
            AddReviewTile(pinnedTile);
        }

        public CombatInspectMode Mode { get; private set; }

        public Vector2Int PinnedTile { get; private set; }

        public List<Vector2Int> OrderedTiles { get; private set; }

        public IDetails TooltipDetails { get; set; }

        public string TargetLabel { get; set; }

        public static CombatInspectContext ForStack(Vector2Int pinnedTile)
        {
            return new CombatInspectContext(CombatInspectMode.Stack, pinnedTile);
        }

        public static CombatInspectContext ForPath(Vector2Int pinnedTile, List<Vector2Int> path)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.Path, pinnedTile);
            context.SetPath(path);
            return context;
        }

        public static CombatInspectContext ForEntityPath(Vector2Int pinnedTile, List<Vector2Int> path)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.EntityPath, pinnedTile);
            context.SetPath(path);
            return context;
        }

        public static CombatInspectContext ForEntityOnly(Vector2Int pinnedTile)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.EntityOnly, pinnedTile);
            context.OrderedTiles = new List<Vector2Int> { pinnedTile };
            return context;
        }

        public void Add(Vector2Int point, CombatRangeIndicator indicator)
        {
            AddReviewTile(point);
            HashSet<CombatRangeIndicator> set;
            if (!_indicators.TryGetValue(point, out set))
            {
                set = new HashSet<CombatRangeIndicator>();
                _indicators[point] = set;
            }

            set.Add(indicator);
        }

        public bool Contains(Vector2Int point)
        {
            return _reviewSet.Contains(point);
        }

        public void FinalizeOrdering()
        {
            if (OrderedTiles != null)
            {
                return;
            }

            OrderedTiles = new List<Vector2Int>(_reviewSet);
            OrderedTiles.Sort((left, right) =>
            {
                int y = right.y.CompareTo(left.y);
                return y != 0 ? y : left.x.CompareTo(right.x);
            });
        }

        public int CountConnectedComponents()
        {
            if (_reviewSet.Count == 0)
            {
                return 0;
            }

            HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(_reviewSet);
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            int count = 0;
            while (remaining.Count > 0)
            {
                Vector2Int start = default(Vector2Int);
                foreach (Vector2Int point in remaining)
                {
                    start = point;
                    break;
                }

                remaining.Remove(start);
                queue.Enqueue(start);
                count++;
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    Vector2Int[] neighbors = CombatAdapter.GetNeighbors(current);
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        if (remaining.Remove(neighbors[i]))
                        {
                            queue.Enqueue(neighbors[i]);
                        }
                    }
                }
            }

            return count;
        }

        public void AddIndicators(Vector2Int point, List<string> parts)
        {
            if (Mode == CombatInspectMode.Stack && point == PinnedTile)
            {
                return;
            }

            HashSet<CombatRangeIndicator> set;
            if (!_indicators.TryGetValue(point, out set))
            {
                return;
            }

            string text = FormatRangeIndicators(set);
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }

        private void SetPath(List<Vector2Int> path)
        {
            _reviewSet.Clear();
            OrderedTiles = new List<Vector2Int>();
            if (path == null || path.Count == 0)
            {
                AddReviewTile(PinnedTile);
                OrderedTiles.Add(PinnedTile);
                return;
            }

            for (int i = 0; i < path.Count; i++)
            {
                AddReviewTile(path[i]);
                OrderedTiles.Add(path[i]);
            }
        }

        private void AddReviewTile(Vector2Int point)
        {
            _reviewSet.Add(point);
        }

        public static string FormatRangeIndicators(HashSet<CombatRangeIndicator> indicators)
        {
            if (indicators == null || indicators.Count == 0)
            {
                return string.Empty;
            }

            bool hasZoneOfControl = indicators.Contains(CombatRangeIndicator.ZoneOfControl);

            string attackRangeText = string.Empty;
            if (hasZoneOfControl)
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.ZoneOfControl);
            }
            else if (indicators.Contains(CombatRangeIndicator.Deadly))
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.DeadlyRange);
            }
            else if (indicators.Contains(CombatRangeIndicator.Attack) || indicators.Contains(CombatRangeIndicator.Melee))
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.AttackRange);
            }

            bool hasMovement = indicators.Contains(CombatRangeIndicator.Movement);
            if (!hasMovement)
            {
                return attackRangeText;
            }

            if (!hasZoneOfControl && !string.IsNullOrWhiteSpace(attackRangeText))
            {
                string compactAttackText = indicators.Contains(CombatRangeIndicator.Deadly)
                    ? ModText.Get(ModStrings.Spatial.Deadly)
                    : ModText.Get(ModStrings.Spatial.Attack);
                return ModText.Get(ModStrings.Spatial.RangeAndMovement, compactAttackText);
            }

            return string.IsNullOrWhiteSpace(attackRangeText)
                ? ModText.Get(ModStrings.Spatial.MovementRange)
                : ModText.Get(ModStrings.Spatial.RangeAndMovement, attackRangeText);
        }

        private static string FormatList(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            if (values.Count == 1)
            {
                return values[0];
            }

            return ModText.JoinList(values);
        }
    }

    internal enum CombatInspectMode
    {
        Stack,
        Path,
        EntityPath,
        EntityOnly
    }
}
