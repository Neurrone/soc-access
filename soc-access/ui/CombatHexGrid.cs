using System;
using System.Collections.Generic;
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
    /// has nothing of its own to announce, so a landing here asks it for the node's readout again
    /// (<see cref="GraphNavigator.ReannounceFocused"/>, wired in as <c>readTile</c>) rather than
    /// saying the tile itself: the label alone said from here would leave out the dossier the
    /// tooltip setting asks for and the tile's usage hints.
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
        private Vector2Int _cursor;
        private CombatInspectContext _inspectContext;
        private bool _componentWarningSpoken;
        private readonly HexGridScanner _scanner;

        // How a landing is read out: the screen hands the readout to the navigator, which composes
        // the board node exactly as it composes a landing on it from the HUD. Never speech of this
        // class's own - see ReadTile.
        private readonly Action _readTile;

        public CombatHexGrid(CombatAdapter adapter, CombatScreen screen, Action readTile)
        {
            _adapter = adapter;
            _screen = screen;
            _readTile = readTile;
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

        /// <summary>The inspection, if the board it was taken from has not moved under it. What it
        /// holds - the inspected stack's reach tiles and its dossier - is a SNAPSHOT, so the state it
        /// was taken under is read back from the game (<see cref="CombatAdapter.IsInspectStateCurrent"/>)
        /// every time it is used, and an inspection the game has left behind is GIVEN UP rather than
        /// retaken: retaking would silently re-pin the game's hover and search the whole board's reach
        /// again behind the player, while giving it up says "exited inspect mode" and leaves the
        /// cursor on the tile that was pinned - which is what the player already gets from Escape.
        /// </summary>
        private CombatInspectContext InspectContext()
        {
            if (_inspectContext != null && _adapter != null && !_adapter.IsInspectStateCurrent(_inspectContext))
            {
                ExitInspect();
            }

            return _inspectContext;
        }

        public string GetLabel()
        {
            // The context first: giving up an inspection the board has left behind puts the cursor
            // back on the tile that was pinned, and the tile read below is meant to be that one.
            CombatInspectContext context = GetEffectiveInspectContext();
            CombatTile tile = GetFocusedTile();
            bool selectedForSpellcast = _adapter != null
                && _adapter.GetTargetingMode() == CombatTargetingMode.Spell
                && _adapter.IsSpellTargetSelected(_cursor);
            return _adapter != null
                ? _adapter.DescribeTile(tile, context, selectedForSpellcast)
                : ModText.Get(ModStrings.UI.Battlefield);
        }

        public Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetInspectTooltip(InspectContext(), _cursor) : null;
        }

        /// <summary>What the game says an attack on this tile would do, read when the node is read.
        /// </summary>
        public IList<string> GetAttackPreviewLines()
        {
            return _adapter != null ? _adapter.ReadAttackPreviewLines(InspectContext(), _cursor) : null;
        }

        /// <summary>What the game's buff and nerf indicators over the stack on this tile say, read
        /// when the node is read.</summary>
        public IList<string> GetTroopEffectDetailLines()
        {
            return _adapter != null ? _adapter.ReadTroopEffectDetailLines(InspectContext(), _cursor) : null;
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
            get { return InspectContext() != null; }
        }

        /// <summary>Draw the game's own highlight on the tile the cursor stands on and give the tile
        /// the game's focus - what the screen does when the board's node takes the cursor. While
        /// inspecting the native focus is left where the inspection pinned it.</summary>
        public void ShowOverlay()
        {
            // The navigator calls this again on every cursor step, because the tile's tooltip is
            // what the board node aims at, and it calls HideOverlay first, which lets the hover go.
            // Where the game's hover already stands on the tile this grid wants it on - the cursor,
            // or the pinned tile while inspecting - the keyboard only claims it back: the cursor
            // step has synced the hover once already, and a second native focus re-entered every
            // manager's state, re-pinned the inspection and re-played the aiming hover sound, none
            // of which the mouse does for a hover that has not moved.
            bool targeting = _adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None;
            CombatInspectContext context = InspectContext();
            Vector2Int wanted = context != null ? context.PinnedTile : _cursor;
            if (_adapter != null && !(targeting && context != null) && _adapter.IsNativeHoverOn(wanted))
            {
                _adapter.TakeHoverOwnership();
                _adapter.SetFocusedTileOverlay(_cursor);
                return;
            }

            FocusCurrentTile(updateNativeFocus: context == null);
        }

        /// <summary>Take the highlight off again: the cursor has gone to a HUD stop or off the
        /// screen.</summary>
        public void HideOverlay()
        {
            _adapter?.ClearFocusedTileOverlay();
            // The board no longer has the cursor, so the game's hover is the mouse's again.
            _adapter?.ReleaseHoverOwnership();
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

            ReadTile();
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
                || actionKey == AccessibilityActions.DescribeBattlefield.Key
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

            if (action.Key == AccessibilityActions.DescribeBattlefield.Key)
            {
                return SpeakDescription();
            }

            return false;
        }

        /// <summary>The authored description of this layout - the TERRAIN alone, unlabelled: where
        /// each side started is behind the player once the fight is on - and then what lies around
        /// the board, which the battle itself answers.</summary>
        private bool SpeakDescription()
        {
            SpeechPipeline.Output(new SpeechRequest(
                _adapter == null
                    ? BattlefieldText.SpokenCombat(null, null, null, null)
                    : BattlefieldText.SpokenCombat(
                        _adapter.BattlefieldKey,
                        _adapter.GetTerrain(),
                        _adapter.GetSurroundings(),
                        _adapter.WarnUnknownRegion),
                interrupt: false));
            return true;
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
            if (_adapter == null || !_adapter.IsValidTile(point))
            {
                return false;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: true);
            ReadTile();
            PlayTileCues();
            return true;
        }

        /// <summary>
        /// The tile the cursor now stands on, read out - by the NAVIGATOR, deliberately, because the
        /// board node's tooltip and its usage hints are only read where a landing is composed. The
        /// label said from here instead would be the whole readout the player got, which is what
        /// made a step onto a troop say less than pressing Escape back onto it.
        ///
        /// Called after whatever the same keypress has already said (a skip's "Skipped 3 tiles",
        /// inspect's own lines), so the order the player hears is unchanged: the readout is queued
        /// behind them rather than interrupting, exactly as before.
        /// </summary>
        private void ReadTile()
        {
            if (_readTile != null)
            {
                _readTile();
            }
        }

        private void PlayTileCues()
        {
            PlayTileCuesFor(_cursor, 0f, 1f, 0f);
        }

        private void PlayTileCuesFor(Vector2Int point, float panOffset, float gainScale, float semitoneOffset)
        {
            CombatTile tile = _adapter != null ? _adapter.GetTile(point) : null;
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
            if (_adapter == null || !_adapter.IsValidTile(HexGridMoves.CenterTile))
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
            ReadTile();
            PlayTileCues();
            return true;
        }

        private bool SkipMove(Func<Vector2Int, Vector2Int> step)
        {
            // Once for the whole walk rather than once per tile: the predicate below reads the
            // inspection the board is in NOW, and a skip crosses many tiles in one frame.
            InspectContext();
            TileSkipResult result = TileSkipNavigator.FindTarget(
                _cursor,
                step,
                IsValidSkipTile,
                point => CombatTileSkipSignature.FromTile(_adapter != null ? _adapter.GetTile(point) : null));
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
            if (_adapter == null || !_adapter.IsValidTile(point))
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

            if (InspectContext() != null)
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
            if (_adapter == null || !_adapter.IsValidTile(point))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            CombatInspectContext context = InspectContext();
            if (context != null
                && (_adapter == null || _adapter.GetTargetingMode() == CombatTargetingMode.None)
                && !context.Contains(point))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            if (point == _cursor)
            {
                return true;
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: context == null);
            ReadTile();
            PlayTileCues();
            return true;
        }

        private void FocusCurrentTile(bool updateNativeFocus)
        {
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
            else
            {
                // Inspecting: the game's hover belongs to the pinned tile, not to the cursor walking
                // the ranges - but if the mouse took it back since, this puts it on the pin again.
                _adapter?.ReassertHoverPin();
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
            CombatTile tile = context != null && _adapter != null ? _adapter.GetTile(context.PinnedTile) : null;
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

        private CombatTile GetFocusedTile()
        {
            return _adapter != null ? _adapter.GetTile(_cursor) : null;
        }

        private CombatInspectContext GetEffectiveInspectContext()
        {
            return _adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None ? null : InspectContext();
        }
    }
}
