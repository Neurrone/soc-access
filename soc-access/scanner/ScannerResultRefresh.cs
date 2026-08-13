using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What an adapter reports when the controller re-queries a result it is
    /// about to announce: whether the thing still exists, where it is now, and
    /// what the game currently calls it. Replaces the old validate-only seam so
    /// a stale position or a renamed entity is corrected at announcement time
    /// rather than surviving until the next full rebuild.
    /// </summary>
    internal struct ScannerResultRefresh
    {
        private ScannerResultRefresh(bool isValid, Vector2Int position, string label)
        {
            IsValid = isValid;
            Position = position;
            Label = label;
        }

        public bool IsValid { get; private set; }

        public Vector2Int Position { get; private set; }

        /// <summary>
        /// The game-authored name, or null to keep the label the result was
        /// built with.
        /// </summary>
        public string Label { get; private set; }

        public static ScannerResultRefresh Invalid
        {
            get { return new ScannerResultRefresh(false, Vector2Int.zero, null); }
        }

        public static ScannerResultRefresh Valid(Vector2Int position)
        {
            return new ScannerResultRefresh(true, position, null);
        }

        public static ScannerResultRefresh Valid(Vector2Int position, string label)
        {
            return new ScannerResultRefresh(true, position, label);
        }
    }
}
