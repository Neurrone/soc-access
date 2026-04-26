using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class AdventureMapGrid : Widget
    {
        private readonly AdventureMapAdapter _adapter;
        private Vector2Int _cursorTile;

        public AdventureMapGrid(AdventureMapAdapter adapter)
            : base("adventure_map_grid")
        {
            _adapter = adapter;
            _cursorTile = adapter != null ? adapter.GetInitialTile() : Vector2Int.zero;
        }

        public override string GetRole()
        {
            return "world grid";
        }

        public override string GetFocusMessage()
        {
            AdventureMapTile tile = _adapter != null ? _adapter.GetTile(_cursorTile) : null;
            return tile != null ? tile.ToSpeech() : "Adventure map";
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
                || actionKey == AccessibilityActions.MapSecondaryAction.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null || _adapter == null)
            {
                return false;
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
    }
}
