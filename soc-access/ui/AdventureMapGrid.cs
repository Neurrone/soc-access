using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class AdventureMapGrid : Widget
    {
        private const string ScannerWrapCueKey = "Common_ClickUnfold";

        private readonly AdventureMapAdapter _adapter;
        private Vector2Int _cursorTile;
        private readonly ScannerController _scanner;

        public AdventureMapGrid(AdventureMapAdapter adapter)
            : base("adventure_map_grid")
        {
            _adapter = adapter;
            _cursorTile = adapter != null ? adapter.GetInitialTile() : Vector2Int.zero;
            _scanner = new ScannerController(
                origin => _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null,
                () => _cursorTile,
                result => _adapter != null && _adapter.ValidateScannerResult(result),
                JumpToScannerResult,
                (result, directions, index, count) => new AdventureScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    directions,
                    index,
                    count),
                ScannerDirectionMode.Square);
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetLabel()
        {
            AdventureMapTile tile = _adapter != null ? _adapter.GetTile(_cursorTile) : null;
            return new AdventureMapTileSpeechFormatter().DescribeTile(tile);
        }

        public override Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetTooltip(_cursorTile) : null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            // TODO: Enter on an already-selected wielder should eventually open an
            // accessible selected-wielder HUD screen. That is explicitly outside
            // the initial adventure-map interaction scope; see wielders.md.
            return actionKey == AccessibilityActions.MapMoveNorth.Key
                || actionKey == AccessibilityActions.MapMoveSouth.Key
                || actionKey == AccessibilityActions.MapMoveWest.Key
                || actionKey == AccessibilityActions.MapMoveEast.Key
                || actionKey == AccessibilityActions.Activate.Key
                || actionKey == AccessibilityActions.MapSecondaryAction.Key
                || actionKey == AccessibilityActions.NextWielder.Key
                || actionKey == AccessibilityActions.NextSettlement.Key
                || IsScannerAction(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null || _adapter == null)
            {
                return false;
            }

            if (HandleScannerAction(action))
            {
                return true;
            }

            if (action.Key == AccessibilityActions.MapMoveNorth.Key)
            {
                return Move(0, 1);
            }

            if (action.Key == AccessibilityActions.MapMoveSouth.Key)
            {
                return Move(0, -1);
            }

            if (action.Key == AccessibilityActions.MapMoveWest.Key)
            {
                return Move(-1, 0);
            }

            if (action.Key == AccessibilityActions.MapMoveEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return _adapter.HandlePrimaryAction(_cursorTile);
            }

            if (action.Key == AccessibilityActions.MapSecondaryAction.Key)
            {
                return _adapter.HandleSecondaryAction(_cursorTile);
            }

            if (action.Key == AccessibilityActions.NextWielder.Key)
            {
                return _adapter.TrySelectNextWielder();
            }

            if (action.Key == AccessibilityActions.NextSettlement.Key)
            {
                return _adapter.TrySelectNextSettlement();
            }

            return false;
        }

        protected override void OnFocus()
        {
            _adapter?.SetFocusedTileOverlay(_cursorTile);
        }

        protected override void OnUnfocus()
        {
            _adapter?.ClearFocusedTileOverlay();
        }

        public bool FocusTile(Vector2Int tile)
        {
            return FocusTile(tile, updateUiManager: true);
        }

        public bool FocusTileSilently(Vector2Int tile)
        {
            return FocusTile(tile, updateUiManager: false);
        }

        private bool FocusTile(Vector2Int tile, bool updateUiManager)
        {
            if (_adapter == null)
            {
                return false;
            }

            _cursorTile = tile;
            _adapter.SetFocusedTileOverlay(_cursorTile);
            if (updateUiManager)
            {
                UIManager.SetFocusedWidget(this);
            }

            return true;
        }

        private bool Move(int xDelta, int yDelta)
        {
            Vector2Int nextTile = _adapter.Move(_cursorTile, xDelta, yDelta);
            if (nextTile == _cursorTile)
            {
                return true;
            }

            _cursorTile = nextTile;
            _adapter.EnsureTileInView(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            if (_adapter == null)
            {
                return false;
            }

            _cursorTile = point;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            return true;
        }

        private bool HandleScannerAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerRefresh.Key)
            {
                return _scanner.Refresh();
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
                return _scanner.SpeakOrientation();
            }

            return false;
        }

        private bool HandleScannerNavigationResult(ScannerCommandResult result)
        {
            if (result != null && result.Status == ScannerCommandStatus.Result && result.Wrapped)
            {
                NativeSoundUtility.PostEvent(ScannerWrapCueKey);
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
                || actionKey == AccessibilityActions.ScannerSpeakOrientation.Key;
        }
    }
}
