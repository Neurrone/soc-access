using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TroopPlacementHexGrid : Widget
    {
        private readonly PreBattleMenuAdapter _adapter;
        private TroopPlacementSnapshot _snapshot;
        private Vector2Int _cursor;
        private Vector2Int? _dragSource;
        private readonly ScannerController _scanner;

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
                return tile.Point == _dragSource.Value ? "dragging" : string.Empty;
            }

            return IsOwnTroop(tile) ? "draggable" : string.Empty;
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

            if (action.Key == AccessibilityActions.HexGridEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridNorthWest.Key)
            {
                return MoveDiagonal(north: true, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridNorthEast.Key)
            {
                return MoveDiagonal(north: true, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridSouthWest.Key)
            {
                return MoveDiagonal(north: false, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSouthEast.Key)
            {
                return MoveDiagonal(north: false, east: true);
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

            FocusCurrentTile();
        }

        protected override void OnFocus()
        {
            FocusCurrentTile();
        }

        protected override void OnUnfocus()
        {
            CancelDrag();
            _adapter?.HideNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private bool Move(int xDelta, int yDelta)
        {
            return SetCursor(new Vector2Int(_cursor.x + xDelta, _cursor.y + yDelta));
        }

        private bool MoveDiagonal(bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((_cursor.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return Move(xDelta, yDelta);
        }

        private bool StartDrag()
        {
            TroopPlacementTile tile = GetFocusedTile();
            if (!IsOwnTroop(tile))
            {
                return true;
            }

            _dragSource = _cursor;
            Speak("Started drag for " + tile.TroopLabel + " at " + FormatPoint(_cursor) + ". Press enter to drop on destination spawn point, or press escape to cancel.");
            return true;
        }

        private bool Drop()
        {
            if (!_dragSource.HasValue)
            {
                Speak("Press space to select a troop stack first.");
                return true;
            }

            Vector2Int source = _dragSource.Value;
            if (_adapter != null && _adapter.TryMoveTroop(source, _cursor))
            {
                _dragSource = null;
                Speak("Drag complete.");
                return true;
            }

            Speak("Invalid destination.");
            return true;
        }

        private bool CancelDrag()
        {
            if (!_dragSource.HasValue)
            {
                return false;
            }

            _dragSource = null;
            Speak("Drag cancelled.");
            return true;
        }

        private bool SetCursor(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return true;
            }

            if (point == _cursor)
            {
                return true;
            }

            _cursor = point;
            FocusCurrentTile();
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            return SetCursor(point);
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
                return _scanner.Refresh();
            }

            if (action.Key == AccessibilityActions.ScannerPreviousCategory.Key)
            {
                return _scanner.MoveCategory(-1);
            }

            if (action.Key == AccessibilityActions.ScannerNextCategory.Key)
            {
                return _scanner.MoveCategory(1);
            }

            if (action.Key == AccessibilityActions.ScannerPreviousSubcategory.Key)
            {
                return _scanner.MoveSubcategory(-1);
            }

            if (action.Key == AccessibilityActions.ScannerNextSubcategory.Key)
            {
                return _scanner.MoveSubcategory(1);
            }

            if (action.Key == AccessibilityActions.ScannerPreviousResult.Key)
            {
                return _scanner.MoveResult(-1);
            }

            if (action.Key == AccessibilityActions.ScannerNextResult.Key)
            {
                return _scanner.MoveResult(1);
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakOrientation.Key)
            {
                return _scanner.SpeakOrientation();
            }

            return false;
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
                || actionKey == AccessibilityActions.ScannerSpeakOrientation.Key;
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

        private static string FormatPoint(Vector2Int point)
        {
            return "(" + point.x + ", " + point.y + ")";
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }
    }
}
