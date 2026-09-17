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
            : this(steps, false, null)
        {
        }

        public ScannerDirections(IReadOnlyList<ScannerDirectionStep> steps, bool blocked, string blockerName)
        {
            _steps = steps ?? new List<ScannerDirectionStep>();
            Blocked = blocked;
            BlockerName = blockerName;
        }

        /// <summary>Whether something other than the ground stands between the cursor and the
        /// result. False where a route reaches it, and where the ground itself is the answer and
        /// the player has nothing to act on.</summary>
        public bool Blocked { get; private set; }

        /// <summary>The army standing in the way of a blocked result, where the player can see
        /// one. Null where the result is not blocked, and where no visible army is in the way,
        /// which is said without a name.</summary>
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
