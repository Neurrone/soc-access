using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What the game answers about walking from the cursor to a scanner result. A surface without
    /// a pathfinder behind it supplies none, and the scanner then orders by the straight line and
    /// names no blocker.
    /// </summary>
    public interface IScannerPathSource
    {
        /// <summary>What stops the walk to a result no route reaches: the name of the army whose
        /// zone of control the route runs into, or of the map entity standing in it. Null where
        /// terrain alone is the answer and there is nothing to name. Asked when a result whose
        /// <see cref="GetPathCost"/> is infinite is read, in either order mode.</summary>
        string TryGetPathBlockerName(Vector2Int origin, ScannerResult result);

        /// <summary>What walking from <paramref name="origin"/> to the result costs the wielder
        /// today, or <see cref="float.PositiveInfinity"/> where an army or something built on the
        /// map stands in the way. Answered out of one whole-map sweep, so the scanner can ask it
        /// for every result in a snapshot.</summary>
        float GetPathCost(Vector2Int origin, ScannerResult result);

        /// <summary>What the same walk would cost over the terrain alone, with nothing standing in
        /// it, or <see cref="float.PositiveInfinity"/> where the ground itself is the answer. The
        /// order ranks a blocked result by this, so it sits where the walk to it would put it
        /// rather than at the back. One whole-map sweep too.</summary>
        float GetTerrainPathCost(Vector2Int origin, ScannerResult result);
    }
}
