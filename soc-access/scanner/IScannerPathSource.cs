using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What the game answers about walking from the cursor to a scanner result. Asked only while
    /// the Distance setting says the walkable path; in straight-line mode the scanner never calls
    /// it, and a surface without a pathfinder never supplies one.
    /// </summary>
    public interface IScannerPathSource
    {
        /// <summary>The runs of steps along the path the game would walk from
        /// <paramref name="origin"/> to the result, or null when the game answers no path.</summary>
        IReadOnlyList<ScannerDirectionStep> TryGetPathDirections(Vector2Int origin, ScannerResult result);

        /// <summary>What stops the walk to a result the pathfinder could not reach: the name of the
        /// army whose zone of control the route runs into, or of the map entity standing in it.
        /// Null where terrain alone is the answer and there is nothing to name. Asked only after
        /// <see cref="TryGetPathDirections"/> has answered null.</summary>
        string TryGetPathBlockerName(Vector2Int origin, ScannerResult result);

        /// <summary>What walking from <paramref name="origin"/> to the result costs, or
        /// <see cref="float.PositiveInfinity"/> where no path reaches it. Answered out of one
        /// whole-map sweep, so the scanner can ask it for every result in a snapshot.</summary>
        float GetPathCost(Vector2Int origin, ScannerResult result);
    }
}
