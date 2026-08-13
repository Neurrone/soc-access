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
