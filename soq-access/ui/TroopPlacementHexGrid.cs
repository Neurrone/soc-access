using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TroopPlacementHexGrid : Widget
    {
        private readonly PreBattleMenuAdapter _adapter;
        private TroopPlacementSnapshot _snapshot;
        private Vector2Int _cursor;
        private Vector2Int? _dragSource;

        public TroopPlacementHexGrid(PreBattleMenuAdapter adapter)
            : base("pre-battle-hex-grid")
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = GetInitialCursor();
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetRole()
        {
            return "hex grid";
        }

        public override string GetLabel()
        {
            TroopPlacementTile tile = GetFocusedTile();
            return tile != null ? Describe(tile) : "Troop placement grid";
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
                || actionKey == AccessibilityActions.TroopPlacementNextOwnSpawn.Key
                || actionKey == AccessibilityActions.TroopPlacementPreviousOwnSpawn.Key
                || actionKey == AccessibilityActions.TroopPlacementNextEnemySpawn.Key
                || actionKey == AccessibilityActions.TroopPlacementPreviousEnemySpawn.Key
                || actionKey == AccessibilityActions.TroopPlacementStartDrag.Key
                || actionKey == AccessibilityActions.Activate.Key
                || actionKey == AccessibilityActions.TroopPlacementCancelDrag.Key;
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

            if (action.Key == AccessibilityActions.TroopPlacementNextOwnSpawn.Key)
            {
                return CycleSpawnPoint(own: true, delta: 1);
            }

            if (action.Key == AccessibilityActions.TroopPlacementPreviousOwnSpawn.Key)
            {
                return CycleSpawnPoint(own: true, delta: -1);
            }

            if (action.Key == AccessibilityActions.TroopPlacementNextEnemySpawn.Key)
            {
                return CycleSpawnPoint(own: false, delta: 1);
            }

            if (action.Key == AccessibilityActions.TroopPlacementPreviousEnemySpawn.Key)
            {
                return CycleSpawnPoint(own: false, delta: -1);
            }

            if (action.Key == AccessibilityActions.TroopPlacementStartDrag.Key)
            {
                return StartDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return Drop();
            }

            if (action.Key == AccessibilityActions.TroopPlacementCancelDrag.Key)
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

        private bool CycleSpawnPoint(bool own, int delta)
        {
            if (_snapshot == null)
            {
                RefreshSnapshot();
            }

            List<TroopPlacementTile> spawnPoints = _snapshot != null ? _snapshot.GetSpawnPoints(own) : null;
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                return true;
            }

            int currentIndex = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i].Point == _cursor)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = Mod(currentIndex + delta, spawnPoints.Count);
            return SetCursor(spawnPoints[nextIndex].Point);
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

        private string Describe(TroopPlacementTile tile)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tile.TroopLabel))
            {
                parts.Add(tile.TroopLabel);
            }

            if (tile.SpawnSide.HasValue)
            {
                bool enemySpawn = _snapshot != null
                    && _snapshot.OwnSide.HasValue
                    && tile.SpawnSide.Value != _snapshot.OwnSide.Value;
                parts.Add(enemySpawn ? "enemy spawn point" : "spawn point");
            }

            if (!string.IsNullOrWhiteSpace(tile.EntityLabel))
            {
                parts.Add(tile.EntityLabel);
            }

            if (tile.IsBlocked)
            {
                parts.Add("Blocked");
            }

            if (tile.IsHighGround)
            {
                parts.Add("High ground");
            }

            parts.Add(FormatPoint(tile.Point));
            return string.Join(", ", parts.ToArray());
        }

        private static string FormatPoint(Vector2Int point)
        {
            return "(" + point.x + ", " + point.y + ")";
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }
    }
}
