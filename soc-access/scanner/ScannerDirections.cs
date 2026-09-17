using System.Collections;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The way to a result: the runs of the straight line to it, and what the game says stands in
    /// the way of walking there. The name travels with the steps rather than beside them because
    /// both places that speak a result's directions - the result readout and the bearing key - are
    /// handed the list alone.
    /// </summary>
    public sealed class ScannerDirections : IReadOnlyList<ScannerDirectionStep>
    {
        private readonly IReadOnlyList<ScannerDirectionStep> _steps;

        public ScannerDirections(IReadOnlyList<ScannerDirectionStep> steps)
            : this(steps, null)
        {
        }

        public ScannerDirections(IReadOnlyList<ScannerDirectionStep> steps, string blockerName)
        {
            _steps = steps ?? new List<ScannerDirectionStep>();
            BlockerName = blockerName;
        }

        /// <summary>What the game says is standing in the way of a result no route reaches, where
        /// it names something: an army or a map entity. Null where a route does reach the result,
        /// and where terrain alone stops it and there is nothing to name.</summary>
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
