using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What an adapter reports when the controller re-queries a result it is
    /// about to announce: whether the thing still exists and where it is now.
    /// Replaces the old validate-only seam so a stale position, or a group
    /// result measured from wherever the scan started, is corrected at
    /// announcement time rather than surviving until the next full rebuild.
    /// </summary>
    internal struct ScannerResultRefresh
    {
        private ScannerResultRefresh(bool isValid, Vector2Int position)
        {
            IsValid = isValid;
            Position = position;
        }

        public bool IsValid { get; private set; }

        public Vector2Int Position { get; private set; }

        public static ScannerResultRefresh Invalid
        {
            get { return new ScannerResultRefresh(false, Vector2Int.zero); }
        }

        public static ScannerResultRefresh Valid(Vector2Int position)
        {
            return new ScannerResultRefresh(true, position);
        }
    }
}
