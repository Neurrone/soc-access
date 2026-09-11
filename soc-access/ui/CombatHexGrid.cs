using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The battlefield's TILE CURSOR - the cursor of a mode whose cursor is not the focus cursor
    /// (<c>ui-graph-plan.md</c> phase E), the same shape as <see cref="AdventureMapGrid"/> and
    /// <see cref="TroopPlacementHexGrid"/>. <see cref="Screens.CombatScreen"/> declares ONE node for
    /// the whole board and hands this class every key that walks it: the six hex moves and their
    /// skips, the centre tile, inspect, the troop cycles, the relevant-tile walk, the threat readout
    /// and the scanner. Because the node's identity never changes as the cursor walks, the navigator
    /// has nothing to announce and this class says each landing itself, queued rather than
    /// interrupting, exactly as the widget engine's focus commit said it.
    ///
    /// THE GAME'S CLICKS ARE THE NODE'S, not this class's: Enter (confirm a spell or ability target
    /// while aiming) and Backslash (the right click a troop acts with) are declared on the node by
    /// the screen, and Escape reaches <see cref="HandleBack"/> through the screen's Back.
    /// </summary>
    public sealed class CombatHexGrid
    {
        private readonly CombatAdapter _adapter;

        // The screen that owns this grid, handed over when it builds it: the troop cycles and the
        // turn-order jump are the screen's, and the board's keys reach them through here.
        private readonly CombatScreen _screen;
        private CombatSnapshot _snapshot;
        private Vector2Int _cursor;
        private CombatInspectContext _inspectContext;
        private bool _componentWarningSpoken;
        private readonly HexGridScanner _scanner;

        public CombatHexGrid(CombatAdapter adapter, CombatScreen screen)
        {
            _adapter = adapter;
            _screen = screen;
            RefreshSnapshot();
            _cursor = _adapter != null ? _adapter.GetInitialTile() : Vector2Int.zero;
            _scanner = new HexGridScanner(
                origin => ScannerCustomCategorySynthesizer.ApplyFromSettings(
                    _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null),
                (result, cursorHint) => _adapter != null
                    ? _adapter.TryRefreshScannerResult(result, cursorHint)
                    : ScannerResultRefresh.Invalid,
                (result, directions, index, count, includeItemName) => new CombatScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    _adapter,
                    directions,
                    index,
                    count,
                    includeItemName),
                () => _cursor,
                SetCursor,
                PlayTileCuesFor);
        }

        public string GetLabel()
        {
            CombatTile tile = GetFocusedTile();
            bool selectedForSpellcast = _adapter != null
                && _adapter.GetTargetingMode() == CombatTargetingMode.Spell
                && _adapter.IsSpellTargetSelected(_cursor);
            return _adapter != null
                ? _adapter.DescribeTile(tile, GetEffectiveInspectContext(), selectedForSpellcast)
                : ModText.Get(ModStrings.UI.Battlefield);
        }

        public Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetInspectTooltip(_inspectContext, _cursor) : null;
        }

        /// <summary>Where the cursor stands - the tile the node's clicks act on.</summary>
        public Vector2Int CursorTile
        {
            get { return _cursor; }
        }

        /// <summary>Whether the inspect sub-mode is on, which is one of the two states in which the
        /// screen takes Escape away from the game.</summary>
        public bool IsInspecting
        {
            get { return _inspectContext != null; }
        }

        /// <summary>Draw the game's own highlight on the tile the cursor stands on and give the tile
        /// the game's focus - what the screen does when the board's node takes the cursor. While
        /// inspecting the native focus is left where the inspection pinned it.</summary>
        public void ShowOverlay()
        {
            FocusCurrentTile(updateNativeFocus: _inspectContext == null);
        }

        /// <summary>Take the highlight off again: the cursor has gone to a HUD stop or off the
        /// screen.</summary>
        public void HideOverlay()
        {
            _adapter?.ClearFocusedTileOverlay();
        }

        /// <summary>Enter on the board: while a spell or an ability is being aimed it confirms the
        /// target under the cursor. The game binds no confirm key in battle otherwise, so anywhere
        /// else it does nothing. What a spell confirmation DID is handed back for the screen to say.
        /// </summary>
        public CombatSpellTargetSelection ConfirmTarget()
        {
            if (_adapter == null)
            {
                return CombatSpellTargetSelection.None;
            }

            CombatTargetingMode mode = _adapter.GetTargetingMode();
            if (mode == CombatTargetingMode.Spell)
            {
                return _adapter.ConfirmSpellTarget(_cursor);
            }

            if (mode == CombatTargetingMode.Ability)
            {
                _adapter.ConfirmAbilityTarget(_cursor);
            }

            return CombatSpellTargetSelection.None;
        }

        /// <summary>Escape while the board has the cursor: it gives up whatever sub-mode is on - the
        /// spell being aimed, the ability being aimed, or the inspection - and answers false when
        /// none of them is, which is what leaves the key to the game's pause menu.</summary>
        public bool HandleBack()
        {
            if (_adapter != null && _adapter.GetTargetingMode() == CombatTargetingMode.Spell && _adapter.CancelSpellTargeting())
            {
                return true;
            }

            if (_adapter != null && _adapter.GetTargetingMode() == CombatTargetingMode.Ability && _adapter.CancelAbilityTargeting())
            {
                return true;
            }

            // Cued here rather than inside ExitInspect: the other callers exit inspect as a
            // prelude to their own cursor move and would double up.
            if (!ExitInspect())
            {
                return false;
            }

            SpeakTile();
            PlayTileCues();
            return true;
        }

        public bool ClaimsAction(string actionKey)
        {
            return HexGridMoves.ClaimsAction(actionKey)
                || actionKey == AccessibilityActions.CombatInspect.Key
                || (CanNavigateLocalActingTroops()
                    && (actionKey == AccessibilityActions.CombatNextActingTroop.Key
                        || actionKey == AccessibilityActions.CombatPreviousActingTroop.Key
                        || actionKey == AccessibilityActions.CombatFocusActingTroop.Key))
                || (CanNavigateEnemyActingTroops()
                    && (actionKey == AccessibilityActions.CombatNextEnemyTroop.Key
                        || actionKey == AccessibilityActions.CombatPreviousEnemyTroop.Key))
                || actionKey == AccessibilityActions.CombatFocusTimeline.Key
                || actionKey == AccessibilityActions.ReadThreat.Key
                || HexGridScanner.ClaimsAction(actionKey);
        }

        public bool HandleAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (_scanner.HandleAction(action))
            {
                return true;
            }

            bool skip;
            Func<Vector2Int, Vector2Int> step = HexGridMoves.TryGetStep(action.Key, out skip);
            if (step != null)
            {
                return skip ? SkipMove(step) : SetCursor(step(_cursor));
            }

            if (action.Key == AccessibilityActions.HexGridFocusCenterTile.Key)
            {
                return MoveToCenterTile();
            }

            if (action.Key == AccessibilityActions.CombatInspect.Key)
            {
                if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
                {
                    return true;
                }

                return EnterInspect();
            }

            if (action.Key == AccessibilityActions.CombatNextActingTroop.Key)
            {
                return _screen != null && _screen.NavigateLocalActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousActingTroop.Key)
            {
                return _screen != null && _screen.NavigateLocalActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusActingTroop.Key)
            {
                return _screen != null && _screen.FocusActingTroop();
            }

            if (action.Key == AccessibilityActions.CombatNextEnemyTroop.Key)
            {
                return _screen != null && _screen.NavigateEnemyActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousEnemyTroop.Key)
            {
                return _screen != null && _screen.NavigateEnemyActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusTimeline.Key)
            {
                return _screen != null && _screen.FocusTimeline();
            }

            if (action.Key == AccessibilityActions.ReadThreat.Key)
            {
                return ReadThreat();
            }

            return false;
        }

        private bool CanNavigateLocalActingTroops()
        {
            return _screen != null && _screen.CanNavigateLocalActingTroops();
        }

        private bool CanNavigateEnemyActingTroops()
        {
            return _screen != null && _screen.CanNavigateEnemyActingTroops();
        }

        /// <summary>Put the cursor on a troop's tile and read it, as a move does: the queue's Enter,
        /// the troop cycles and the narrator's "it is your turn" all land this way.</summary>
        public bool MoveToTroop(Vector2Int point)
        {
            RefreshSnapshot();
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return false;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: true);
            SpeakTile();
            PlayTileCues();
            return true;
        }

        /// <summary>The tile description, said the way the widget engine's focus commit said it:
        /// queued behind whatever the same keypress has already said, so a skip's "Skipped 3 tiles"
        /// is heard before the tile it landed on.</summary>
        private void SpeakTile()
        {
            string label = GetLabel();
            if (!string.IsNullOrWhiteSpace(label))
            {
                SpeechPipeline.Output(new SpeechRequest(label, interrupt: false));
            }
        }

        private void PlayTileCues()
        {
            PlayTileCuesFor(_cursor, 0f, 1f, 0f);
        }

        private void PlayTileCuesFor(Vector2Int point, float panOffset, float gainScale, float semitoneOffset)
        {
            CombatTile tile = _snapshot != null ? _snapshot.Get(point) : null;
            if (tile == null)
            {
                return;
            }

            // Same condition as the speech formatter, so the warning sounds exactly when the
            // threatened wording would be spoken.
            bool isThreatened = tile.Troop == null
                && tile.TroopId < 0
                && _adapter != null
                && _adapter.IsThreatenedByEnemy(point, tile.Troop);
            CueLibrary.PlayCues(
                TileCueSelector.ForCombatTile(
                    tile,
                    _adapter != null && _adapter.IsEnemyTroop(tile.Troop),
                    _adapter != null && _adapter.IsActingTroop(tile.Troop),
                    isThreatened),
                panOffset,
                gainScale,
                semitoneOffset);
        }

        private bool MoveToCenterTile()
        {
            RefreshSnapshot();
            if (_snapshot == null || !_snapshot.IsValidTile(HexGridMoves.CenterTile))
            {
                return true;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            if (_cursor == HexGridMoves.CenterTile)
            {
                FocusCurrentTile(updateNativeFocus: true);
                PlayTileCues();
                return true;
            }

            _cursor = HexGridMoves.CenterTile;
            FocusCurrentTile(updateNativeFocus: true);
            SpeakTile();
            PlayTileCues();
            return true;
        }

        private bool SkipMove(Func<Vector2Int, Vector2Int> step)
        {
            RefreshSnapshot();
            TileSkipResult result = TileSkipNavigator.FindTarget(
                _cursor,
                step,
                IsValidSkipTile,
                point => CombatTileSkipSignature.FromTile(_snapshot != null ? _snapshot.Get(point) : null));
            if (result.Target == _cursor)
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            TileSkipNavigator.SpeakSkipped(result.SkippedCount);
            return SetCursor(result.Target);
        }

        private bool IsValidSkipTile(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return false;
            }

            return _inspectContext == null || _inspectContext.Contains(point);
        }

        private bool ReadThreat()
        {
            CombatTile tile = GetFocusedTile();
            string text = _adapter != null && tile != null
                ? _adapter.DescribeEnemyInfluenceForSpeech(tile.Point, tile.Troop)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }

            return true;
        }

        private bool EnterInspect()
        {
            if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
            {
                return true;
            }

            if (_inspectContext != null)
            {
                return true;
            }

            bool notInMovementRange = false;
            CombatInspectContext context = _adapter != null ? _adapter.BeginInspect(_cursor, out notInMovementRange) : null;
            if (context == null)
            {
                if (notInMovementRange)
                {
                    SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.NotInMovementRange), interrupt: false));
                }

                return true;
            }

            _inspectContext = context;
            _inspectContext.FinalizeOrdering();
            _componentWarningSpoken = false;
            SpeakInspectStarted(_inspectContext);
            MaybeSpeakDisconnectedWarning();
            FocusCurrentTile(updateNativeFocus: false);
            return true;
        }

        private bool ExitInspect()
        {
            if (_inspectContext == null)
            {
                return false;
            }

            Vector2Int inspectedTile = _inspectContext.PinnedTile;
            _cursor = inspectedTile;
            _inspectContext = null;
            _componentWarningSpoken = false;
            _adapter?.ExitInspect(_cursor);
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.ExitedInspectMode), interrupt: false));
            FocusCurrentTile(updateNativeFocus: true);
            return true;
        }

        private bool SetCursor(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            if (_inspectContext != null
                && (_adapter == null || _adapter.GetTargetingMode() == CombatTargetingMode.None)
                && !_inspectContext.Contains(point))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            if (point == _cursor)
            {
                return true;
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: _inspectContext == null);
            SpeakTile();
            PlayTileCues();
            return true;
        }

        private void FocusCurrentTile(bool updateNativeFocus)
        {
            RefreshSnapshot();
            if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
            {
                if (_inspectContext != null)
                {
                    ExitInspect();
                    return;
                }

                _adapter.FocusTargetTile(_cursor);
            }
            else if (updateNativeFocus)
            {
                _adapter?.FocusTile(_cursor);
            }

            _adapter?.SetFocusedTileOverlay(_cursor);
        }

        public void HandleTargetingBegin()
        {
            if (_inspectContext != null)
            {
                ExitInspect();
            }

            FocusCurrentTile(updateNativeFocus: true);
        }

        private void MaybeSpeakDisconnectedWarning()
        {
            if (_componentWarningSpoken || _inspectContext == null || _inspectContext.Mode != CombatInspectMode.Stack)
            {
                return;
            }

            if (_inspectContext.CountConnectedComponents() > 1)
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.CombatDisconnectedTiles), interrupt: false));
                _componentWarningSpoken = true;
            }
        }

        private void SpeakInspectStarted(CombatInspectContext context)
        {
            string target = DescribeInspectTarget(context);
            if (string.IsNullOrWhiteSpace(target))
            {
                target = ModText.Get(ModStrings.UI.Target);
            }

            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Inspecting, target), interrupt: false));
        }

        /// <summary>What the inspection has just pinned itself to: the stack standing there, the
        /// thing that can be attacked there, or - on an empty tile the acting troop can walk to -
        /// the tile itself.</summary>
        private string DescribeInspectTarget(CombatInspectContext context)
        {
            CombatTile tile = context != null && _snapshot != null ? _snapshot.Get(context.PinnedTile) : null;
            if (tile == null || _adapter == null)
            {
                return string.Empty;
            }

            if (tile.Troop != null)
            {
                return CombatTroopText.Stack(_adapter.GetTroopFacts(tile.Troop));
            }

            return tile.Entity != null
                ? CombatTroopText.Entity(_adapter.GetEntityFacts(tile.Entity))
                : _adapter.DescribeTile(tile, null);
        }

        private void RefreshSnapshot()
        {
            _snapshot = _adapter != null ? _adapter.BuildSnapshot() : null;
        }

        private CombatTile GetFocusedTile()
        {
            return _snapshot != null ? _snapshot.Get(_cursor) : null;
        }

        private CombatInspectContext GetEffectiveInspectContext()
        {
            return _adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None ? null : _inspectContext;
        }
    }
}
