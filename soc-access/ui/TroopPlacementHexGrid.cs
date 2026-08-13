using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TroopPlacementHexGrid : Widget
    {
        private const string ScannerWrapCueKey = "Common_ClickUnfold";
        private static readonly Vector2Int CenterTile = new Vector2Int(6, 4);

        private readonly PreBattleMenuAdapter _adapter;
        private TroopPlacementSnapshot _snapshot;
        private Vector2Int _cursor;
        private Vector2Int? _dragSource;
        private readonly ScannerController _scanner;
        private bool _tileCuesArmed;
        private bool _tileCuesHandled;
        private readonly ScannerJumpAnchor _jumpAnchor = new ScannerJumpAnchor();

        public TroopPlacementHexGrid(PreBattleMenuAdapter adapter)
            : base("pre-battle-hex-grid")
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = GetInitialCursor();
            _scanner = new ScannerController(
                origin => _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null,
                () => _cursor,
                result => _adapter != null && _adapter.ValidateScannerResult(result),
                JumpToScannerResult,
                (result, directions, index, count) => new TroopPlacementScannerSpeechContext(
                    result,
                    GetScannerTile(result),
                    _snapshot,
                    directions,
                    index,
                    count),
                ScannerDirectionMode.Hex);
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetAnnouncementKey()
        {
            return _cursor.ToString();
        }

        public override string GetLabel()
        {
            TroopPlacementTile tile = GetFocusedTile();
            return new TroopPlacementTileSpeechFormatter(_snapshot).DescribeTile(tile);
        }

        public override string GetStatus()
        {
            TroopPlacementTile tile = GetFocusedTile();
            if (tile == null)
            {
                return string.Empty;
            }

            if (_dragSource.HasValue)
            {
                return tile.Point == _dragSource.Value ? ModText.Get(ModStrings.UI.StatusDragging) : string.Empty;
            }

            return IsOwnTroop(tile) ? ModText.Get(ModStrings.UI.StatusDraggable) : string.Empty;
        }

        public override Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetTileTooltip(GetFocusedTile()) : null;
        }

        public override bool ClaimsAction(string actionKey)
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
                || actionKey == AccessibilityActions.StartDrag.Key
                || actionKey == AccessibilityActions.Activate.Key
                || (_dragSource.HasValue && actionKey == AccessibilityActions.Cancel.Key)
                || IsScannerAction(actionKey);
        }

        public override bool HasClaimInTree(string actionKey)
        {
            return ClaimsAction(actionKey);
        }

        public override bool HandleAction(InputAction action)
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
                return SetCursor(CenterTile);
            }

            if (action.Key == AccessibilityActions.HexGridSkipSouthEast.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: false, east: true));
            }

            if (action.Key == AccessibilityActions.StartDrag.Key)
            {
                return StartDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return Drop();
            }

            if (action.Key == AccessibilityActions.Cancel.Key && _dragSource.HasValue)
            {
                return CancelDrag();
            }

            return false;
        }

        public void RebuildAfterPlacementChanged()
        {
            Vector2Int previousCursor = _cursor;
            _dragSource = null;
            RefreshSnapshot();
            if (_snapshot != null && _snapshot.IsValidTile(previousCursor))
            {
                _cursor = previousCursor;
            }
            else
            {
                _cursor = GetInitialCursor();
            }

            if (_tileCuesArmed)
            {
                FocusCurrentTile();
                PlayTileCues();
                return;
            }

            // The game can fire a deployment change while the screen is still arming. That rebuild
            // stays silent, including the focus it claims below.
            _tileCuesHandled = true;
            FocusCurrentTile();
        }

        private void PlayTileCues()
        {
            _tileCuesHandled = true;
            PlayTileCuesFor(_cursor, 0f, 1f, 0f);
        }

        private void PlayTileCuesFor(Vector2Int point, float panOffset, float gainScale, float semitoneOffset)
        {
            TroopPlacementTile tile = _snapshot != null ? _snapshot.Get(point) : null;
            if (tile == null)
            {
                return;
            }

            CueLibrary.PlayCues(
                TileCueSelector.ForTroopPlacementTile(tile, IsOwnTroop(tile)),
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

        protected override void OnFocus()
        {
            FocusCurrentTile();
            _tileCuesArmed = true;

            // Focus arrival announces the current tile, so it gets a cue too. Paths that already
            // cued while claiming focus mark the arrival handled so it only sounds once.
            if (!_tileCuesHandled)
            {
                PlayTileCues();
            }
        }

        protected override void OnUnfocus()
        {
            _tileCuesHandled = false;
            if (_dragSource.HasValue)
            {
                NativeSoundUtility.PostEvent("Common_SpellbookEndDragCancel");
            }

            ClearDrag();
            _adapter?.HideNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
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
                point => _snapshot != null && _snapshot.IsValidTile(point),
                point => TroopPlacementTileSkipSignature.FromTile(_snapshot != null ? _snapshot.Get(point) : null));
            if (result.Target == _cursor)
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            SpeakSkipped(result.SkippedCount);
            return SetCursor(result.Target);
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

        private bool StartDrag()
        {
            TroopPlacementTile tile = GetFocusedTile();
            if (!IsOwnTroop(tile))
            {
                return true;
            }

            _dragSource = _cursor;
            Speak(ModText.Get(ModStrings.UI.DragStartedTroopPlacement, tile.TroopLabel, HexCoordinateFormatter.Format(_cursor)));
            return true;
        }

        private bool Drop()
        {
            if (!_dragSource.HasValue)
            {
                Speak(ModText.Get(ModStrings.UI.PressSpaceToDrag));
                return true;
            }

            Vector2Int source = _dragSource.Value;
            if (_adapter != null && _adapter.TryMoveTroop(source, _cursor))
            {
                _dragSource = null;
                Speak(ModText.Get(ModStrings.UI.DragComplete));
                return true;
            }

            Speak(ModText.Get(ModStrings.UI.InvalidDestination));
            return true;
        }

        private bool CancelDrag()
        {
            if (!_dragSource.HasValue)
            {
                return false;
            }

            ClearDrag();
            NativeSoundUtility.PostEvent("Common_SpellbookEndDragCancel");
            Speak(ModText.Get(ModStrings.UI.DragCancelled));
            return true;
        }

        private void ClearDrag()
        {
            _dragSource = null;
        }

        private bool SetCursor(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            if (point == _cursor)
            {
                return true;
            }

            _cursor = point;
            FocusCurrentTile();
            PlayTileCues();
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            _jumpAnchor.Remember(_cursor);
            return SetCursor(point);
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

        private TroopPlacementTile GetScannerTile(ScannerResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (_snapshot == null)
            {
                RefreshSnapshot();
            }

            return _snapshot != null ? _snapshot.Get(result.Position) : null;
        }

        private bool HandleScannerAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerRefresh.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteRefresh());
            }

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

            if (action.Key == AccessibilityActions.ScannerPreviousResult.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveResult(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextResult.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveResult(1));
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakOrientation.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteSpeakOrientation());
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
            return actionKey == AccessibilityActions.ScannerRefresh.Key
                || actionKey == AccessibilityActions.ScannerPreviousCategory.Key
                || actionKey == AccessibilityActions.ScannerNextCategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousSubcategory.Key
                || actionKey == AccessibilityActions.ScannerNextSubcategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousResult.Key
                || actionKey == AccessibilityActions.ScannerNextResult.Key
                || actionKey == AccessibilityActions.ScannerJumpToResult.Key
                || actionKey == AccessibilityActions.ScannerSpeakOrientation.Key
                || actionKey == AccessibilityActions.ScannerReturnFromJump.Key;
        }

        private void FocusCurrentTile()
        {
            _adapter?.FocusTile(GetFocusedTile());
            _adapter?.SetFocusedTileOverlay(_cursor);
            UIManager.SetFocusedWidget(this);
        }

        private void RefreshSnapshot()
        {
            _snapshot = _adapter != null ? _adapter.BuildSnapshot() : null;
        }

        private Vector2Int GetInitialCursor()
        {
            if (_snapshot == null)
            {
                return Vector2Int.zero;
            }

            List<TroopPlacementTile> ownSpawns = _snapshot.GetSpawnPoints(own: true);
            for (int i = 0; i < ownSpawns.Count; i++)
            {
                if (IsOwnTroop(ownSpawns[i]))
                {
                    return ownSpawns[i].Point;
                }
            }

            if (ownSpawns.Count > 0)
            {
                return ownSpawns[0].Point;
            }

            foreach (TroopPlacementTile tile in _snapshot.Tiles)
            {
                return tile.Point;
            }

            return Vector2Int.zero;
        }

        private TroopPlacementTile GetFocusedTile()
        {
            return _snapshot != null ? _snapshot.Get(_cursor) : null;
        }

        private bool IsOwnTroop(TroopPlacementTile tile)
        {
            if (tile == null || !tile.TroopSide.HasValue || _snapshot == null || !_snapshot.OwnSide.HasValue)
            {
                return false;
            }

            return tile.TroopSide.Value == _snapshot.OwnSide.Value;
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
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
    }
}
