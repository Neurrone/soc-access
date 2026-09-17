using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The order the scanner reads results in, and the one place that knows which of the two
    /// distances the player asked for. Straight-line mode ranks by squared tile distance from the
    /// scan origin. Walkable-path mode ranks by what the wielder pays to walk there, putting every
    /// result no path reaches after all the reachable ones and ordering those by straight line
    /// among themselves; the straight line is also the tie-break between equal costs.
    ///
    /// The landing, the reseat, the flat walk and the search all sort through one of these, which
    /// is how they agree on an order. A cost is asked for once per result and kept: a sort asks
    /// O(n log n) times and a snapshot holds thousands of results.
    /// </summary>
    public sealed class ScannerDistanceOrder
    {
        private readonly Func<ScannerResult, float> _pathCost;
        private readonly Dictionary<ScannerResult, float> _costs;

        private ScannerDistanceOrder(Vector2Int origin, Func<ScannerResult, float> pathCost)
        {
            Origin = origin;
            _pathCost = pathCost;
            _costs = pathCost != null ? new Dictionary<ScannerResult, float>() : null;
        }

        public static ScannerDistanceOrder StraightLine(Vector2Int origin)
        {
            return new ScannerDistanceOrder(origin, null);
        }

        /// <summary>Falls back to the straight line when no cost function is supplied, so a surface
        /// with no pathfinder behind it keeps working.</summary>
        public static ScannerDistanceOrder WalkablePath(Vector2Int origin, Func<ScannerResult, float> pathCost)
        {
            return new ScannerDistanceOrder(origin, pathCost);
        }

        public Vector2Int Origin { get; private set; }

        public bool RanksByPath
        {
            get { return _pathCost != null; }
        }

        /// <summary>The cost of walking to a result, or infinity where nothing reaches it.
        /// Infinity for everything in straight-line mode, which never asks.</summary>
        public float PathCost(ScannerResult result)
        {
            if (_pathCost == null || result == null)
            {
                return float.PositiveInfinity;
            }

            float cost;
            if (_costs.TryGetValue(result, out cost))
            {
                return cost;
            }

            cost = _pathCost(result);
            _costs[result] = cost;
            return cost;
        }

        /// <summary>How far apart two results are in this order, before the name and position
        /// tie-break. The search ranks by its match tier first and then by this.</summary>
        public int CompareDistance(ScannerResult left, ScannerResult right)
        {
            if (_pathCost != null)
            {
                float leftCost = PathCost(left);
                float rightCost = PathCost(right);
                bool leftReachable = !float.IsPositiveInfinity(leftCost);
                bool rightReachable = !float.IsPositiveInfinity(rightCost);
                if (leftReachable != rightReachable)
                {
                    return leftReachable ? -1 : 1;
                }

                if (leftReachable)
                {
                    int costCompare = leftCost.CompareTo(rightCost);
                    if (costCompare != 0)
                    {
                        return costCompare;
                    }
                }
            }

            return ScannerSnapshot.DistanceSquared(Origin, left.Position)
                .CompareTo(ScannerSnapshot.DistanceSquared(Origin, right.Position));
        }

        public int Compare(ScannerResult left, ScannerResult right)
        {
            int distanceCompare = CompareDistance(left, right);
            return distanceCompare != 0 ? distanceCompare : ScannerSnapshot.CompareTieBreak(left, right);
        }
    }
}
