using System;
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
    /// <summary>
    /// The troop placement board's TILE CURSOR - the cursor of a mode whose cursor is not the focus
    /// cursor (<c>ui-graph-plan.md</c> phase E), built on the same shape as
    /// <see cref="AdventureMapGrid"/>. <see cref="Screens.PreBattleMenuScreen"/> declares ONE node for
    /// the whole board and hands this class every key that walks it: the six hex moves and their
    /// skips, the centre-tile jump and the scanner. Because the node's identity never changes as the
    /// cursor walks, the navigator has nothing to announce and this class says each landing itself,
    /// queued rather than interrupting, exactly as the widget engine's focus commit said it.
    ///
    /// THE DRAG IS GONE FROM HERE: picking a troop up and putting it down is the graph engine's carry
    /// (<c>ui/graph/Carry.cs</c>), declared on the node by the screen, so this class no longer owns
    /// Space, Enter or Escape and holds no drag state of its own.
    /// </summary>
    public sealed class TroopPlacementHexGrid
    {
        private readonly PreBattleMenuAdapter _adapter;
        private TroopPlacementSnapshot _snapshot;
        private Vector2Int _cursor;
        private readonly HexGridScanner _scanner;

        public TroopPlacementHexGrid(PreBattleMenuAdapter adapter)
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = GetInitialCursor();
            _scanner = new HexGridScanner(
                origin => ScannerCustomCategorySynthesizer.ApplyFromSettings(
                    _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null),
                (result, cursorHint) => _adapter != null
                    ? _adapter.TryRefreshScannerResult(result, cursorHint)
                    : ScannerResultRefresh.Invalid,
                (result, directions, index, count, includeItemName) => new TroopPlacementScannerSpeechContext(
                    result,
                    GetScannerTile(result),
                    _snapshot,
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
            TroopPlacementTile tile = GetFocusedTile();
            return new TroopPlacementTileSpeechFormatter(_snapshot).DescribeTile(tile);
        }

        /// <summary>Where the cursor stands - the source of a carry, and what a drop lands on.</summary>
        public Vector2Int CursorTile
        {
            get { return _cursor; }
        }

        /// <summary>Whether the tile under the cursor holds one of the player's OWN troops, which is
        /// the only thing that can be picked up here.</summary>
        public bool CanPickUp
        {
            get { return IsOwnTroop(GetFocusedTile()); }
        }

        /// <summary>The game's own name for the troop under the cursor, captured when it is picked
        /// up.</summary>
        public string FocusedTroopLabel
        {
            get
            {
                TroopPlacementTile tile = GetFocusedTile();
                return tile != null ? tile.TroopLabel : null;
            }
        }

        public Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetTileTooltip(GetFocusedTile()) : null;
        }

        public bool ClaimsAction(string actionKey)
        {
            return HexGridMoves.ClaimsAction(actionKey)
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
                return SetCursor(HexGridMoves.CenterTile);
            }

            return false;
        }

        /// <summary>The board changed under the cursor - a troop was placed, moved or removed. The
        /// cursor keeps its tile where that tile still exists, and the tile is read again only where
        /// the player is standing on the board (<paramref name="announce"/>): a change made while the
        /// cursor is on a side panel or on a button is not a landing.</summary>
        public void RebuildAfterPlacementChanged(bool announce)
        {
            Vector2Int previousCursor = _cursor;
            RefreshSnapshot();
            if (_snapshot != null && _snapshot.IsValidTile(previousCursor))
            {
                _cursor = previousCursor;
            }
            else
            {
                _cursor = GetInitialCursor();
            }

            Land(announce);
        }

        private void PlayTileCues()
        {
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

        /// <summary>Draw the game's own highlight on the tile the cursor stands on and hover it, so
        /// the game draws the troop's own details - what the screen does when the board's node takes
        /// the focus.</summary>
        public void ShowOverlay()
        {
            _adapter?.FocusTile(GetFocusedTile());
            _adapter?.SetFocusedTileOverlay(_cursor);
        }

        /// <summary>Take the highlight and the hover off again: the focus has gone to a panel, a
        /// button, or off the screen.</summary>
        public void HideOverlay()
        {
            _adapter?.HideNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private bool SkipMove(Func<Vector2Int, Vector2Int> step)
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

            TileSkipNavigator.SpeakSkipped(result.SkippedCount);
            return SetCursor(result.Target);
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
            Land(announce: true);
            return true;
        }

        /// <summary>What every landing does: the highlight, the hover the game draws its details for,
        /// and - where the player went there themselves - the tile read out with its cues.</summary>
        private void Land(bool announce)
        {
            ShowOverlay();
            if (!announce)
            {
                return;
            }

            SpeakTile();
            PlayTileCues();
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
    }
}
