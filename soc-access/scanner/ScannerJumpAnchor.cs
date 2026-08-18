using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Remembers the single tile the cursor left behind on the last jump, so a
    /// jump that lands somewhere unhelpful is one keystroke from being undone.
    /// The anchor is cleared once used, so return is never a toggle.
    ///
    /// This lives beside the grid widgets rather than on ScannerController
    /// because the controller does not own the cursor, and bookmark jumps set
    /// the same anchor without going through the controller at all.
    /// </summary>
    internal sealed class ScannerJumpAnchor
    {
        private bool _hasAnchor;
        private Vector2Int _anchor;

        public void Remember(Vector2Int tile)
        {
            _hasAnchor = true;
            _anchor = tile;
        }

        /// <summary>
        /// Remembers where a jump came from, but only where the cursor really
        /// left it. A grid can turn a jump down, an inspect context holds the
        /// cursor inside its own tiles, and says so with a cue rather than in
        /// its answer, so the cursor itself is what tells the two apart.
        /// Reports whether the jump happened, which is also what the scanner
        /// needs before it decides to leave the talking to the tile landed on:
        /// a refused jump keeps the anchor the player already had and is no
        /// jump at all.
        /// </summary>
        public bool RememberIfMoved(Vector2Int origin, Vector2Int cursor)
        {
            if (cursor == origin)
            {
                return false;
            }

            Remember(origin);
            return true;
        }

        public void Clear()
        {
            _hasAnchor = false;
            _anchor = Vector2Int.zero;
        }

        public bool TryTake(out Vector2Int tile)
        {
            tile = _anchor;
            if (!_hasAnchor)
            {
                return false;
            }

            Clear();
            return true;
        }
    }
}
