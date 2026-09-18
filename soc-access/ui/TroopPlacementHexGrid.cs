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
    /// cursor walks, the navigator has nothing of its own to announce, so a landing here asks it for
    /// the node's readout again (<see cref="GraphNavigator.ReannounceFocused"/>, wired in as
    /// <c>readTile</c>) rather than saying the tile itself: the label alone said from here would
    /// leave out the node's tooltip and its usage hints.
    ///
    /// THE DRAG IS GONE FROM HERE: picking a troop up and putting it down is the graph engine's carry
    /// (<c>ui/graph/Carry.cs</c>), declared on the node by the screen, so this class no longer owns
    /// Space, Enter or Escape and holds no drag state of its own.
    /// </summary>
    public sealed class TroopPlacementHexGrid
    {
        private readonly PreBattleMenuAdapter _adapter;
        private TroopPlacementSnapshot _snapshot;
        // The menu's placement state the snapshot was taken under. Whose troops these are is part
        // of the snapshot (OwnSide), and the hot-seat hand-over changes it silently - the deployment
        // menu raises OnChanged only from a drop - so the state is read from the game again every
        // time the snapshot is used, and a snapshot taken under another one is retaken.
        private string _snapshotState;
        private Vector2Int _cursor;
        private readonly HexGridScanner _scanner;

        // How a landing is read out: the screen hands the readout to the navigator, which composes
        // the board node exactly as it composes a landing on it from a side panel. Never speech of
        // this class's own - see ReadTile.
        private readonly Action _readTile;

        public TroopPlacementHexGrid(PreBattleMenuAdapter adapter, Action readTile)
        {
            _adapter = adapter;
            _readTile = readTile;
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
                    Snapshot(),
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
            TroopPlacementSnapshot snapshot = Snapshot();
            return new TroopPlacementTileSpeechFormatter(snapshot).DescribeTile(GetFocusedTile());
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
                return SetCursor(HexGridMoves.CenterTile);
            }

            if (action.Key == AccessibilityActions.DescribeBattlefield.Key)
            {
                return SpeakDescription();
            }

            return false;
        }

        /// <summary>The authored description of this layout, the same three labelled lines the
        /// Description stop holds, said as one breath. Queued behind whatever is already speaking,
        /// as every other readout from this board is.</summary>
        private bool SpeakDescription()
        {
            SpeechPipeline.Output(new SpeechRequest(
                _adapter == null
                    ? BattlefieldText.Spoken(null, null, null)
                    : BattlefieldText.Spoken(_adapter.BattlefieldKey, _adapter.GetTerrain(), _adapter.WarnUnknownRegion),
                interrupt: false));
            return true;
        }

        /// <summary>The board changed under the cursor - a troop was placed, moved or removed, or
        /// the menu handed the placement turn to the other side. The cursor keeps its tile where
        /// that tile still exists AND the board is still the same side's; a hand-over starts it
        /// again at this side's own placement, because the tile it was on belongs to the side whose
        /// turn is over. The tile is read again only where the player is standing on the board
        /// (<paramref name="announce"/>): a change made while the cursor is on a side panel or on a
        /// button is not a landing.</summary>
        public void RebuildAfterPlacementChanged(bool announce)
        {
            Vector2Int previousCursor = _cursor;
            TroopPlacementSnapshot previous = _snapshot;
            RefreshSnapshot();
            // A refresh that found the turn handed over has already started the cursor again at the
            // new side's placement; where the side is unchanged the cursor keeps its tile as long as
            // that tile still exists.
            if (_snapshot != null && KeepsSide(previous))
            {
                _cursor = _snapshot.IsValidTile(previousCursor) ? previousCursor : GetInitialCursor();
            }

            Land(announce);
        }

        /// <summary>The menu's placement state moved on. The board is taken again, and only a
        /// hand-over is a landing: a state change that leaves the same side placing ("Waiting for
        /// opponent") moves no cursor, so there is no tile to read out again.</summary>
        public void RebuildAfterStateChanged(bool announce)
        {
            TroopPlacementSnapshot previous = _snapshot;
            RefreshSnapshot();
            if (!KeepsSide(previous))
            {
                Land(announce);
            }
        }

        /// <summary>Whether the board is still the same side's as it was in
        /// <paramref name="previous"/>. False only across a hand-over to a side that is now
        /// placing: "both ready" leaves nobody placing and so no side to start again at, and the
        /// cursor stays where the player left it.</summary>
        private bool KeepsSide(TroopPlacementSnapshot previous)
        {
            return previous == null
                || _snapshot == null
                || !_snapshot.OwnSide.HasValue
                || _snapshot.OwnSide.Equals(previous.OwnSide);
        }

        private void PlayTileCues()
        {
            PlayTileCuesFor(_cursor, 0f, 1f, 0f);
        }

        private void PlayTileCuesFor(Vector2Int point, float panOffset, float gainScale, float semitoneOffset)
        {
            TroopPlacementSnapshot snapshot = Snapshot();
            TroopPlacementTile tile = snapshot != null ? snapshot.Get(point) : null;
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
            TroopPlacementSnapshot snapshot = Snapshot();
            if (snapshot == null || !snapshot.IsValidTile(point))
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

            ReadTile();
            PlayTileCues();
        }

        /// <summary>
        /// The tile the cursor now stands on, read out - by the NAVIGATOR, deliberately, because the
        /// board node's tooltip and its usage hints are only read where a landing is composed. The
        /// label said from here instead would be the whole readout the player got.
        ///
        /// Called after whatever the same keypress has already said (a skip's "Skipped 3 tiles"),
        /// so the order the player hears is unchanged: the readout is queued behind it rather than
        /// interrupting, exactly as before.
        /// </summary>
        private void ReadTile()
        {
            if (_readTile != null)
            {
                _readTile();
            }
        }

        private TroopPlacementTile GetScannerTile(ScannerResult result)
        {
            if (result == null)
            {
                return null;
            }

            TroopPlacementSnapshot snapshot = Snapshot();
            return snapshot != null ? snapshot.Get(result.Position) : null;
        }

        /// <summary>The board as it is now, retaken when the menu's placement state has moved on
        /// since it was last taken: one property read off the game and one string comparison per
        /// use, never a walk. The hot-seat hand-over and "both ready" change whose troops these are
        /// without raising the deployment menu's OnChanged, so this is what catches them.</summary>
        private TroopPlacementSnapshot Snapshot()
        {
            string state = _adapter != null ? _adapter.PlacementState : null;
            if (_snapshot == null || !string.Equals(state, _snapshotState, StringComparison.Ordinal))
            {
                RefreshSnapshot();
            }

            return _snapshot;
        }

        /// <summary>Take the board again, under the placement state the game is in NOW. A retake
        /// that finds the turn handed to the other side starts the cursor again at that side's own
        /// placement: the tile it was standing on belongs to the side whose turn is over.</summary>
        private void RefreshSnapshot()
        {
            TroopPlacementSnapshot previous = _snapshot;
            _snapshot = _adapter != null ? _adapter.BuildSnapshot() : null;
            _snapshotState = _adapter != null ? _adapter.PlacementState : null;
            if (!KeepsSide(previous))
            {
                _cursor = GetInitialCursor();
            }
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
            TroopPlacementSnapshot snapshot = Snapshot();
            return snapshot != null ? snapshot.Get(_cursor) : null;
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
