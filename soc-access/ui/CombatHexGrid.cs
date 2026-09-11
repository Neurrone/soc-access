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
    /// has nothing to announce and this class says each landing itself, queued rather than
    /// interrupting, exactly as the widget engine's focus commit said it.
    ///
    /// THE GAME'S CLICKS ARE THE NODE'S, not this class's: Enter (confirm a spell or ability target
    /// while aiming) and Backslash (the right click a troop acts with) are declared on the node by
    /// the screen, and Escape reaches <see cref="HandleBack"/> through the screen's Back.
    /// </summary>
    public sealed class CombatHexGrid
    {
        private const string ScannerWrapCueKey = "Common_ClickUnfold";
        private static readonly Vector2Int CenterTile = new Vector2Int(6, 4);

        private readonly CombatAdapter _adapter;
        private CombatSnapshot _snapshot;
        private Vector2Int _cursor;
        private CombatInspectContext _inspectContext;
        private bool _componentWarningSpoken;
        private readonly ScannerController _scanner;
        private readonly ScannerJumpAnchor _jumpAnchor = new ScannerJumpAnchor();

        public CombatHexGrid(CombatAdapter adapter)
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = _adapter != null ? _adapter.GetInitialTile() : Vector2Int.zero;
            _scanner = new ScannerController(
                origin => ScannerCustomCategorySynthesizer.ApplyFromSettings(
                    _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null),
                () => _cursor,
                (result, cursorHint) => _adapter != null
                    ? _adapter.TryRefreshScannerResult(result, cursorHint)
                    : ScannerResultRefresh.Invalid,
                JumpToScannerResult,
                (result, directions, index, count, includeItemName) => new CombatScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    _adapter,
                    directions,
                    index,
                    count,
                    includeItemName),
                ScannerDirectionMode.Hex);
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
        /// else it does nothing.</summary>
        public bool ConfirmTarget()
        {
            if (_adapter == null)
            {
                return false;
            }

            CombatTargetingMode mode = _adapter.GetTargetingMode();
            return mode == CombatTargetingMode.Spell
                ? _adapter.ConfirmSpellTarget(_cursor)
                : mode == CombatTargetingMode.Ability && _adapter.ConfirmAbilityTarget(_cursor);
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
            return actionKey == AccessibilityActions.HexGridWest.Key
                || actionKey == AccessibilityActions.HexGridEast.Key
                || actionKey == AccessibilityActions.HexGridNorthWest.Key
                || actionKey == AccessibilityActions.HexGridNorthEast.Key
                || actionKey == AccessibilityActions.HexGridSouthWest.Key
                || actionKey == AccessibilityActions.HexGridSouthEast.Key
                || actionKey == AccessibilityActions.HexGridFocusCenterTile.Key
                || actionKey == AccessibilityActions.HexGridSkipWest.Key
                || actionKey == AccessibilityActions.HexGridSkipEast.Key
                || actionKey == AccessibilityActions.HexGridSkipNorthWest.Key
                || actionKey == AccessibilityActions.HexGridSkipNorthEast.Key
                || actionKey == AccessibilityActions.HexGridSkipSouthWest.Key
                || actionKey == AccessibilityActions.HexGridSkipSouthEast.Key
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
                || IsScannerAction(actionKey);
        }

        public bool HandleAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (HandleScannerAction(action))
            {
                return true;
            }

            if (action.Key == AccessibilityActions.HexGridWest.Key)
            {
                return Move(-1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridSkipWest.Key)
            {
                return SkipMove(point => new Vector2Int(point.x - 1, point.y));
            }

            if (action.Key == AccessibilityActions.HexGridEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridSkipEast.Key)
            {
                return SkipMove(point => new Vector2Int(point.x + 1, point.y));
            }

            if (action.Key == AccessibilityActions.HexGridNorthWest.Key)
            {
                return MoveDiagonal(north: true, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSkipNorthWest.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: true, east: false));
            }

            if (action.Key == AccessibilityActions.HexGridNorthEast.Key)
            {
                return MoveDiagonal(north: true, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridSkipNorthEast.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: true, east: true));
            }

            if (action.Key == AccessibilityActions.HexGridSouthWest.Key)
            {
                return MoveDiagonal(north: false, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSkipSouthWest.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: false, east: false));
            }

            if (action.Key == AccessibilityActions.HexGridSouthEast.Key)
            {
                return MoveDiagonal(north: false, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridFocusCenterTile.Key)
            {
                return MoveToCenterTile();
            }

            if (action.Key == AccessibilityActions.HexGridSkipSouthEast.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: false, east: true));
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
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.NavigateLocalActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousActingTroop.Key)
            {
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.NavigateLocalActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusActingTroop.Key)
            {
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.FocusActingTroop();
            }

            if (action.Key == AccessibilityActions.CombatNextEnemyTroop.Key)
            {
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.NavigateEnemyActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousEnemyTroop.Key)
            {
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.NavigateEnemyActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusTimeline.Key)
            {
                CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
                return screen != null && screen.FocusTimeline();
            }

            if (action.Key == AccessibilityActions.ReadThreat.Key)
            {
                return ReadThreat();
            }

            return false;
        }

        private bool CanNavigateLocalActingTroops()
        {
            CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
            return screen != null && screen.CanNavigateLocalActingTroops();
        }

        private bool CanNavigateEnemyActingTroops()
        {
            CombatScreen screen = SocAccessMod.Instance?.ScreenManager?.Current as CombatScreen;
            return screen != null && screen.CanNavigateEnemyActingTroops();
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

        /// <summary>The remote tile's own cues carry the direction to it; out of range plays nothing.</summary>
        private void PlayDirectionalTileCues(Vector2Int origin, Vector2Int target)
        {
            float pan;
            float semitones;
            float gainScale;
            if (!DirectionalCueMath.TryCompute(origin, target, CueGridGeometry.Hex, out pan, out semitones, out gainScale))
            {
                return;
            }

            PlayTileCuesFor(target, pan, gainScale, semitones);
        }

        private bool MoveToCenterTile()
        {
            RefreshSnapshot();
            if (_snapshot == null || !_snapshot.IsValidTile(CenterTile))
            {
                return true;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            if (_cursor == CenterTile)
            {
                FocusCurrentTile(updateNativeFocus: true);
                PlayTileCues();
                return true;
            }

            _cursor = CenterTile;
            FocusCurrentTile(updateNativeFocus: true);
            SpeakTile();
            PlayTileCues();
            return true;
        }

        private bool Move(int xDelta, int yDelta)
        {
            return SetCursor(new Vector2Int(_cursor.x + xDelta, _cursor.y + yDelta));
        }

        private bool MoveDiagonal(bool north, bool east)
        {
            return SetCursor(GetDiagonalNeighbor(_cursor, north, east));
        }

        private bool SkipMove(System.Func<Vector2Int, Vector2Int> step)
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

            SpeakSkipped(result.SkippedCount);
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

        private static Vector2Int GetDiagonalNeighbor(Vector2Int point, bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((point.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return new Vector2Int(point.x + xDelta, point.y + yDelta);
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

            CombatInspectContext context = _adapter != null ? _adapter.BeginInspect(_cursor) : null;
            if (context == null)
            {
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

        private bool JumpToScannerResult(Vector2Int point)
        {
            if (point == _cursor)
            {
                SpeakHere();
                return true;
            }

            Vector2Int origin = _cursor;
            SetCursor(point);
            return _jumpAnchor.RememberIfMoved(origin, _cursor);
        }

        /// <summary>
        /// A jump onto the tile the cursor already occupies moves nothing, so no
        /// tile announcement follows it. Say where the player is rather than
        /// letting the key fall silent.
        /// </summary>
        private static void SpeakHere()
        {
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Spatial.Here), interrupt: false));
        }

        private bool ReturnFromJump()
        {
            Vector2Int anchor;
            if (!_jumpAnchor.TryTake(out anchor))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Scanner.NoTileToReturnTo),
                    interrupt: false));
                return true;
            }

            return SetCursor(anchor);
        }

        private bool HandleScannerAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerPreviousCategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveCategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextCategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveCategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousSubcategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveSubcategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextSubcategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveSubcategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousItem.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveItem(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextItem.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveItem(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousInstance.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveInstance(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextInstance.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveInstance(1));
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteSpeakDistanceAndDirection());
            }

            if (action.Key == AccessibilityActions.ScannerReturnFromJump.Key)
            {
                return ReturnFromJump();
            }

            return false;
        }

        private bool HandleScannerNavigationResult(ScannerCommandResult result)
        {
            if (result != null && result.Status == ScannerCommandStatus.Result && result.Wrapped)
            {
                NativeSoundUtility.PostEvent(ScannerWrapCueKey);
            }

            if (result != null && result.Status == ScannerCommandStatus.Result && result.Result != null)
            {
                PlayDirectionalTileCues(_cursor, result.Result.Position);
            }

            _scanner.Output(result);
            return true;
        }

        private static bool IsScannerAction(string actionKey)
        {
            return actionKey == AccessibilityActions.ScannerPreviousCategory.Key
                || actionKey == AccessibilityActions.ScannerNextCategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousSubcategory.Key
                || actionKey == AccessibilityActions.ScannerNextSubcategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousItem.Key
                || actionKey == AccessibilityActions.ScannerNextItem.Key
                || actionKey == AccessibilityActions.ScannerPreviousInstance.Key
                || actionKey == AccessibilityActions.ScannerNextInstance.Key
                || actionKey == AccessibilityActions.ScannerJumpToResult.Key
                || actionKey == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key
                || actionKey == AccessibilityActions.ScannerReturnFromJump.Key;
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

        private static void SpeakInspectStarted(CombatInspectContext context)
        {
            string target = context != null ? context.TargetLabel : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                target = ModText.Get(ModStrings.UI.Target);
            }

            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Inspecting, target), interrupt: false));
        }

        private static void SpeakSkipped(int skippedCount)
        {
            if (skippedCount <= 0)
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(
                ModText.Plural(ModStrings.Spatial.SkippedTileCount, skippedCount, skippedCount),
                interrupt: false));
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

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

    }
}
