using System.Collections;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The way to a result: the runs of steps, and whether they are the straight line said because
    /// the game answered no walkable path. The flag travels with the steps rather than beside them
    /// because both places that speak a result's directions - the result readout and the bearing
    /// key - are handed the list alone and neither knows which mode built it.
    /// </summary>
    public sealed class ScannerDirections : IReadOnlyList<ScannerDirectionStep>
    {
        private readonly IReadOnlyList<ScannerDirectionStep> _steps;

        public ScannerDirections(IReadOnlyList<ScannerDirectionStep> steps, bool isStraightLineFallback)
            : this(steps, isStraightLineFallback, null)
        {
        }

        public ScannerDirections(
            IReadOnlyList<ScannerDirectionStep> steps,
            bool isStraightLineFallback,
            string blockerName)
        {
            _steps = steps ?? new List<ScannerDirectionStep>();
            IsStraightLineFallback = isStraightLineFallback;
            BlockerName = blockerName;
        }

        /// <summary>Whether the player asked for the walkable path and got the straight line
        /// instead, which is the one case the spoken form names.</summary>
        public bool IsStraightLineFallback { get; private set; }

        /// <summary>What the game says is standing in the way, where it names something: an army
        /// or a map entity. Null when terrain alone stops the route, or when these are the
        /// directions the player asked for.</summary>
        public string BlockerName { get; private set; }

        public int Count
        {
            get { return _steps.Count; }
        }

        public ScannerDirectionStep this[int index]
        {
            get { return _steps[index]; }
        }

        public IEnumerator<ScannerDirectionStep> GetEnumerator()
        {
            return _steps.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
