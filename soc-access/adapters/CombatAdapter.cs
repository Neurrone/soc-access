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
        public CombatTroopFacts(string name, int size, int currentHealth, int maxHealth, bool isEnemy, bool isActing)
        {
            Name = name ?? string.Empty;
            Size = size;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsEnemy = isEnemy;
            IsActing = isActing;
        }

        /// <summary>The game's own name for the stack at this size, cleaned, and empty where the
        /// game gives none: the general word a blank name falls back to is wording and belongs to
        /// whoever composes the row.</summary>
        public string Name { get; private set; }

        public int Size { get; private set; }

        public int CurrentHealth { get; private set; }

        public int MaxHealth { get; private set; }

        public bool IsEnemy { get; private set; }

        public bool IsActing { get; private set; }
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
            if (CombatEventNarrator.IsActiveAdapter(this))
            {
                CombatEventNarrator.Reset();
            }
        }

        public void Dispose()
        {
            DetachAbilityTargetingBegin();
            ClearFocusedTileOverlay();
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
            tile.DecorativeFeature = GetDecorativeFeatureAt(point, tile.Entity);
            return tile;
        }

        /// <summary>Who stands on a tile, as the two facts that change what the tile reads as while
        /// the cursor stands still: the id of the troop there - -1 for an empty tile, and for a dead
        /// one, which the game's point lookup already answers null for - and the health its stack has
        /// lost. The game's own point cache answers it, so this is the cheap read a per-frame cache
        /// keys on; <see cref="GetTile"/> composes the whole tile and is not that.</summary>
        public void GetTileTroopState(Vector2Int point, out int troopId, out int healthLost)
        {
            IBattleTroopState troop = GetTroopAt(point);
            troopId = troop != null ? troop.Id : -1;
            healthLost = troop != null ? troop.HealthLost : 0;
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
            SetNativeCursorTile(point, path);
            if (GetTargetingMode() != CombatTargetingMode.None || IsAnySpellCastingStateActive())
            {
                return;
            }

            SetNativeCurrentTroopState();
            _attackPreviewHandler?.Hide();
            // The tile and the path travel down with the point: nothing between here and the hover
            // sync changes what either of them answers, and reading them again cost a second
            // whole-board path search per cursor step.
            CombatTile tile = GetTile(point);
            if (tile != null && (tile.Troop != null || tile.Entity != null))
            {
                SynchronizeNativeHoverForPreview(point, tile, path);
            }
        }

        /// <summary>Point the game's four battle managers at a tile, which is what the mouse moving
        /// over it does.</summary>
        private void SetNativeCursorTile(Vector2Int point, PathNode[] path)
        {
            _cursorManager?.SetCurrentTile(point);
            _gridManager?.SetCurrentTile(point, path);
            _pathManager?.SetCurrentTile(point, path);
            _highlightManager?.SetCurrentTile(point);
        }

        /// <summary>Put all four back into the state the game draws for the troop whose turn it is.
        /// </summary>
        private void SetNativeCurrentTroopState()
        {
            _cursorManager?.SetState(BattleCursorManager.State.CurrentTroop);
            _gridManager?.SetState(BattleGridManager.State.CurrentTroop);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.CurrentTroop);
        }

        /// <summary>Listen for the game asking for a spell target. The two facts it asks with - the
        /// spell's name and the instruction - are handed to the screen, which words and speaks
        /// them.</summary>
        public void AttachSpellTargetingNarration(Action<string, string> handler)
        {
            if (_battleHudSignals == null || handler == null || _targetInstructionHandler != null)
            {
                return;
            }

            _spellTargetInstructionHandler = handler;
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
            _spellTargetInstructionHandler = null;
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

        /// <summary>The screen's answer to "a new turn has begun, put the cursor on the troop". The
        /// narration asks THIS BATTLE'S adapter rather than reaching for whatever screen happens to
        /// be on top, and the screen decides whether the cursor is its to move.</summary>
        public void AttachActingTroopFocus(Action<int> handler)
        {
            _actingTroopFocusHandler = handler;
        }

        public void RequestActingTroopFocus(int troopId)
        {
            _actingTroopFocusHandler?.Invoke(troopId);
        }

        public void AttachAbilityTargetingBegin(Action<TroopAbilityTargeting> handler)
        {
            if (_battleHudSignals == null || handler == null || _beginAbilityTargetingHandler != null)
            {
                return;
            }

            _beginAbilityTargetingHandler = handler;
            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Combine(
                    _battleHudSignals.OnBeginAbilityTargeting, _beginAbilityTargetingHandler);
        }

        public void DetachAbilityTargetingBegin()
        {
            if (_battleHudSignals == null || _beginAbilityTargetingHandler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Remove(
                    _battleHudSignals.OnBeginAbilityTargeting, _beginAbilityTargetingHandler);
            _beginAbilityTargetingHandler = null;
        }

        public void AttachAbilityTargetingEnd(Action<bool> handler)
        {
            if (_battleHudSignals == null || handler == null || _endAbilityTargetingHandler != null)
            {
                return;
            }

            _endAbilityTargetingHandler = handler;
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

        /// <summary>The acting troop's ability, as the game names it.</summary>
        public string GetCurrentAbilityName()
        {
            IBattleTroopState current = GetCurrentTroop();
            ITroopAbilityDefinition ability = current != null && _abilityUtility != null
                ? _abilityUtility.GetAbilityDefinition(current)
                : null;
            return ability != null ? SpokenLines.Clean(GameText.Get(_localization, ability.NameKey, string.Empty)) : string.Empty;
        }

        /// <summary>The game's own instruction for what an ability wants aimed at.</summary>
        public string GetAbilityTargetInstruction(TroopAbilityTargeting targeting)
        {
            return SpokenLines.Clean(GameText.Get(_localization, "Battle/AbilityTargeting/" + targeting, string.Empty));
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

        public CombatSpellTargetSelection ConfirmSpellTarget(Vector2Int point)
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell || !IsValidTile(point))
            {
                return CombatSpellTargetSelection.None;
            }

            int previousSelectionCount = CountSpellTargetSelections(point);
            FocusTargetTile(point);
            if (_spellPrimaryClickMethod == null || _mouseKeyboardSpellInputModule == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter cannot confirm spell target because native HandlePrimaryClick was not found");
                return CombatSpellTargetSelection.None;
            }

            try
            {
                _spellPrimaryClickMethod.Invoke(_mouseKeyboardSpellInputModule, null);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to invoke native spell primary click: " + exception.Message);
                return CombatSpellTargetSelection.None;
            }

            return CombatSpellTargetSelection.Confirmed(
                previousSelectionCount,
                CountSpellTargetSelections(point),
                GetTargetingMode() == CombatTargetingMode.Spell);
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

        /// <summary>Pin the inspection on a tile, or answer null with the reason the caller words:
        /// an empty tile the acting troop cannot walk to has no path to inspect.</summary>
        public CombatInspectContext BeginInspect(Vector2Int point, out bool notInMovementRange)
        {
            notInMovementRange = false;
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

            if (!IsReachable(point))
            {
                notInMovementRange = true;
                return null;
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
            NativeTooltipUtility.HideTooltip();
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
            TileInstruction secondary = TakeCombatTooltipInstruction(captured.InstructionRows, textLines);
            return new Tooltip(
                () => includeAttackPreview ? BuildTooltipLinesWithAttackPreview(textLines, attackPreviewTargetIsEntity) : textLines,
                CreateScreenPointTooltipMetadata(details, tile),
                TileInstruction.None,
                secondary,
                () => NativeTooltipUtility.IsLong(details));
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
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to resolve battle tooltip behavior: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Take the native right-click instruction row out of the tooltip text and report what it
        /// said the click would DO. The board has no primary row. The row is stripped either way - it
        /// describes a mouse gesture the keyboard player is not making - and the screen says the kind
        /// as a usage hint on the key that performs it.
        /// </summary>
        private TileInstruction TakeCombatTooltipInstruction(
            IReadOnlyList<TooltipInstructionRow> instructionRows,
            List<string> textLines)
        {
            TileInstruction secondary = TileInstruction.None;
            if (instructionRows == null || instructionRows.Count == 0)
            {
                return secondary;
            }

            for (int i = 0; i < instructionRows.Count; i++)
            {
                TooltipInstructionRow row = instructionRows[i];
                if (row == null
                    || string.IsNullOrWhiteSpace(row.Text)
                    || !IsSecondaryCombatInstruction(row.InputType))
                {
                    continue;
                }

                TooltipLines.Remove(textLines, row.Text);
                secondary = ClassifyCombatInstruction(row.Text);
            }

            return secondary;
        }

        /// <summary>The kind a row's text names, matched against the game's own battle instruction
        /// strings, which are resolved once per adapter. A wording the table does not hold is logged
        /// once so a new game kind shows up in the log rather than vanishing.</summary>
        private TileInstruction ClassifyCombatInstruction(string text)
        {
            EnsureCombatInstructionKinds();
            TileInstruction kind;
            if (_combatInstructionKinds != null
                && _combatInstructionKinds.TryGetValue(text.Trim(), out kind))
            {
                return kind;
            }

            if (_unknownCombatInstructions.Add(text))
            {
                SocAccessMod.Instance?.LogWarning(
                    "CombatAdapter saw an unrecognized tooltip instruction row: " + text);
            }

            return TileInstruction.None;
        }

        private void EnsureCombatInstructionKinds()
        {
            if (_combatInstructionKindsProbed)
            {
                return;
            }

            _combatInstructionKindsProbed = true;
            if (_localization == null)
            {
                return;
            }

            Dictionary<string, TileInstruction> kinds =
                new Dictionary<string, TileInstruction>(StringComparer.Ordinal);
            AddCombatInstructionKind(kinds, "Battle/InspectTile/ClickToMove", TileInstruction.Move);
            AddCombatInstructionKind(kinds, "Battle/InspectTroop/AttackPreview/ClickToAttack", TileInstruction.Attack);
            _combatInstructionKinds = kinds;
        }

        private void AddCombatInstructionKind(
            Dictionary<string, TileInstruction> kinds,
            string key,
            TileInstruction kind)
        {
            string text = GameText.Get(_localization, key, string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
            {
                kinds[text.Trim()] = kind;
            }
        }

        private bool IsSecondaryCombatInstruction(InputType inputType)
        {
            if (_inputManager != null)
            {
                return inputType == InputType.GetRightMouseClickOrCursorConfirm(_inputManager);
            }

            return inputType == InputType.RightMouseClickOrCursorConfirm;
        }

        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsPresent())
            {
                return;
            }

            try
            {
                if (!_cursorOverlay.Ensure())
                {
                    return;
                }

                _cursorOverlay.MoveTo(GetScreenPoint(tile));
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (!_cursorOverlay.IsCreated)
            {
                return;
            }

            try
            {
                _cursorOverlay.Destroy();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to clear focused tile overlay: " + exception.Message);
            }
        }

        public string DescribeTile(CombatTile tile, CombatInspectContext context)
        {
            return DescribeTile(tile, context, selectedForSpellcast: false);
        }

        public string DescribeTile(CombatTile tile, CombatInspectContext context, bool selectedForSpellcast)
        {
            return new CombatTileSpeechFormatter(this, context, selectedForSpellcast: selectedForSpellcast).DescribeTile(tile);
        }

        private void SynchronizeNativeHoverForInput(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForInput(point, GetTile(point), GetPathTo(point));
        }

        /// <summary>The same hover sync for a caller that has already read the tile and the path this
        /// frame.</summary>
        private void SynchronizeNativeHoverForInput(Vector2Int point, CombatTile tile, PathNode[] path)
        {
            SetNativeCursorTile(point, path);

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

                SetNativeCurrentTroopState();
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.ShowCurrentTroop);
            }
        }

        private void HandleTargetInstruction(ISpellDefinition spell, string instruction)
        {
            string spellName = spell != null ? SpokenLines.Clean(GameText.Get(_localization, spell.NameKey, string.Empty)) : string.Empty;
            _spellTargetInstructionHandler?.Invoke(spellName, SpokenLines.Clean(instruction));
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
                SocAccessMod.Instance?.LogWarning("CombatAdapter cannot emulate " + clickName + " click because the native mouse input module was not resolved.");
                return false;
            }

            try
            {
                clickMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to emulate native " + clickName + " click: " + exception.Message);
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
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            object response = ScreenInputOverride.ResolveWritableResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
                return false;
            }

            Vector2 screenPosition = GetScreenPoint(tilePosition);
            if (screenPosition.x < 0f
                || screenPosition.y < 0f
                || screenPosition.x > Screen.width
                || screenPosition.y > Screen.height)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not target tile " + FormatDiagnosticPoint(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
                return false;
            }

            screenInputOverride = ScreenInputOverride.ApplyMouseClick(response, screenPosition, "CombatAdapter");
            return screenInputOverride != null;
        }

        private CombatInspectContext BeginStackInspect(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return null;
            }

            PathNode[] path = GetPathTo(troop.Position);
            _gridManager?.SetInspectedTroop(troop);
            SetNativeCursorTile(troop.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
            _gridManager?.SetState(BattleGridManager.State.InspectTroop);
            _pathManager?.SetState(BattlePathManager.State.InspectTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
            SynchronizeNativeHoverForPreview(troop.Position, GetTile(troop.Position), path);

            CombatInspectContext context = CombatInspectContext.ForStack(troop.Position);
            BuildStackRanges(troop, context);
            context.TooltipDetails = _tooltipUtility != null ? _tooltipUtility.GetInspectTroopDetails(troop) : null;
            return context;
        }

        private CombatInspectContext BeginPathInspect(Vector2Int point)
        {
            PathNode[] path = GetPathTo(point);
            SetNativeCursorTile(point, path);
            SetNativeCurrentTroopState();
            _attackPreviewHandler?.Hide();
            CombatInspectContext context = CombatInspectContext.ForPath(point, ConvertPath(path));
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
            SetNativeCursorTile(entity.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
            _gridManager?.SetState(BattleGridManager.State.InspectEntity);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            SynchronizeNativeHoverForPreview(entity.Position);

            CombatInspectContext context = PathfinderExtensions.IsReachable(path, GetCurrentMovesLeft(), true)
                ? CombatInspectContext.ForEntityPath(entity.Position, ConvertPath(path))
                : CombatInspectContext.ForEntityOnly(entity.Position);
            context.TooltipDetails = BuildEntityDetails(entity);
            return context;
        }

        private void SynchronizeNativeHoverForPreview(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForPreview(point, GetTile(point), GetPathTo(point));
        }

        /// <summary>The hover sync plus the game's attack preview, for a caller that has already read
        /// the tile and the path. The two melee sweeps the preview wants are the ones the hover sync
        /// itself puts on the controller, so they are not swept a second time here.</summary>
        private void SynchronizeNativeHoverForPreview(Vector2Int point, CombatTile tile, PathNode[] path)
        {
            if (tile == null)
            {
                return;
            }

            SynchronizeNativeHoverForInput(point, tile, path);

            if (_humanBattleController == null)
            {
                return;
            }

            UpdateNativeAttackPreviews();
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

        /// <summary>Everything a spoken stack row is made of, read from the game in one go.</summary>
        public CombatTroopFacts GetTroopFacts(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return new CombatTroopFacts(string.Empty, 0, 0, 0, false, false);
            }

            return new CombatTroopFacts(
                SpokenLines.Clean(_facade.Troops.GetName(troop.Id, troop.Stats.Size)),
                troop.Stats.Size,
                troop.CurrentHealth,
                troop.Stats.MaxHealth.GetValue(),
                IsEnemyTroop(troop),
                IsActingTroop(troop));
        }

        /// <summary>Everything a spoken row for an attackable thing is made of.</summary>
        public CombatEntityFacts GetEntityFacts(IMapEntity entity)
        {
            IHealthComponent health = entity != null ? entity.GetComponent<IHealthComponent>() : null;
            return new CombatEntityFacts(
                GetMapEntityName(entity),
                health != null,
                health != null ? health.HealthLeft : 0,
                health != null ? health.MaxHealth.GetValue() : 0);
        }

        public bool PerformsBeamAttacks(IBattleTroopState troop)
        {
            return troop != null && troop.PerformsBeamAttacks();
        }

        public BeamFacing? GetBeamFacing(IBattleTroopState troop)
        {
            if (!PerformsBeamAttacks(troop) || _battleViewManager == null)
            {
                return null;
            }

            IBattleTroopView view = _battleViewManager.GetTroopView(troop.Id);
            if (view == null)
            {
                return null;
            }

            return view.IsLookingRight ? BeamFacing.Right : BeamFacing.Left;
        }

        public BeamFacing? GetTeamSideBeamDirection(IBattleTroopState troop)
        {
            if (!PerformsBeamAttacks(troop) || _facade == null || _facade.Teams == null)
            {
                return null;
            }

            return troop.TeamId == _facade.Teams.AttackingTeam.Id ? BeamFacing.Right : BeamFacing.Left;
        }

        public int GetCommanderGeneratedEssenceAmount(int commanderId, EssenceType essenceType)
        {
            try
            {
                ICommanderState commander = _facade != null && _facade.Commanders != null ? _facade.Commanders.Get(commanderId) : null;
                return commander != null && commander.Stats != null && commander.Stats.Essences != null
                    ? commander.Stats.Essences.GetValue(essenceType)
                    : 0;
            }
            catch (Exception exception)
            {
                _faults.Report("GetCommanderGeneratedEssenceAmount", exception);
                return 0;
            }
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
            catch (Exception exception)
            {
                _faults.Report("GetAliveBattleTroopIdsForSide", exception);
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
            catch (Exception exception)
            {
                _faults.Report("GetAliveBattleTroopIdsForSide.Filtered", exception);
            }

            return ids;
        }

        public bool IsActingTroop(IBattleTroopState troop)
        {
            IBattleTroopState current = GetCurrentTroop();
            return troop != null && current != null && troop.Id == current.Id;
        }

        public bool IsEnemyTroop(IBattleTroopState troop)
        {
            int localTeamId = GetLocalTeamId();
            return troop != null && localTeamId >= 0 && troop.TeamId != localTeamId;
        }

        private int GetLocalTeamId()
        {
            return BattleFacadeState.LocalTeamId(_facade);
        }

        /// <summary>Which side's HUD column is the local player's, where either is.</summary>
        public CombatHudSide? GetLocalCombatHudSide()
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

        public CombatHudSide? GetEnemyCombatHudSide()
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
            catch (Exception exception)
            {
                _faults.Report("GetTroop", exception);
                return null;
            }
        }

        public IMapEntity GetMapEntity(int entityId)
        {
            try
            {
                return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(entityId) : null;
            }
            catch (Exception exception)
            {
                _faults.Report("GetMapEntity", exception);
                return null;
            }
        }

        /// <summary>The battle's own turn counter: <c>EndBattleTurnCommand</c> raises
        /// <c>Queue.CurrentTurn</c> by one whenever a turn ends, so it is the generation anything
        /// that changes with the turn - reach, the moves left, whose turn it is - can be keyed on.
        /// One field read.</summary>
        public int GetCurrentTurn()
        {
            try
            {
                return _facade != null && _facade.Queue != null ? _facade.Queue.CurrentTurn : 0;
            }
            catch (Exception exception)
            {
                _faults.Report("GetCurrentTurn", exception);
                return 0;
            }
        }

        public int GetCurrentRound()
        {
            return BattleFacadeState.CurrentRound(_facade);
        }

        public IReadOnlyList<int> GetLocalActingTroopIds()
        {
            return GetActingTroopIds(CombatTroopSideFilter.CurrentPlayer);
        }

        public IReadOnlyList<int> GetEnemyActingTroopIds()
        {
            return GetActingTroopIds(CombatTroopSideFilter.Enemy);
        }

        private IReadOnlyList<int> GetActingTroopIds(CombatTroopSideFilter side)
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

                for (int i = 0; i < _facade.Queue.Count; i++)
                {
                    QueuedTroop queuedTroop = _facade.Queue[i];
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
                    bool include = side == CombatTroopSideFilter.Enemy ? isEnemy : !isEnemy;
                    if (include && !troopIds.Contains(troop.Id))
                    {
                        troopIds.Add(troop.Id);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetActingTroopIds", exception);
                return troopIds;
            }

            return troopIds;
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
            return GameText.Get(_localization, key, string.Empty);
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
                    SocAccessMod.Instance?.LogWarning("CombatAdapter failed to resolve tile world position: " + exception.Message);
                }
            }

            return new Vector3(tile.x, 0f, tile.y);
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

        private static string FormatDiagnosticPoint(Vector2Int point)
        {
            return point.x + ", " + point.y;
        }

    }
}
