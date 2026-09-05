using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// Lists the neighbouring tiles a road carries on into.
    ///
    /// Roads are stored as a plain per-tile byte with no connection data, and the game itself
    /// treats any of the eight neighbours as connected, so this simply reports which of them
    /// carry road. On a road painted more than one tile wide that means several directions at
    /// once, which is the point: every direction named is a neighbour that is road as well, so
    /// a curve can be followed a step at a time instead of guessing where it went.
    ///
    /// Whether a neighbour counts as road is entirely the caller's rule. Passability is
    /// deliberately not part of it, so a road tile blocked by an army or a town entrance is
    /// still named; the road really does carry on there, and what stands on it is announced
    /// when the cursor arrives.
    ///
    /// The origin is taken on trust: the caller has already decided it is road, and only asks
    /// about tiles it would name as one.
    /// </summary>
    public static class RoadConnections
    {
        /// <summary>
        /// Compass order, so the directions are always named in a consistent sweep. Offset and
        /// name are held together rather than in two arrays lined up by index, so neither can
        /// drift out of step with the other.
        /// </summary>
        private static readonly Neighbour[] Neighbours =
        {
            new Neighbour(0, 1, ScannerDirection.North),
            new Neighbour(1, 1, ScannerDirection.Northeast),
            new Neighbour(1, 0, ScannerDirection.East),
            new Neighbour(1, -1, ScannerDirection.Southeast),
            new Neighbour(0, -1, ScannerDirection.South),
            new Neighbour(-1, -1, ScannerDirection.Southwest),
            new Neighbour(-1, 0, ScannerDirection.West),
            new Neighbour(-1, 1, ScannerDirection.Northwest)
        };

        private static readonly ScannerDirection[] Empty = new ScannerDirection[0];

        public static IReadOnlyList<ScannerDirection> Compute(Vector2Int origin, Func<Vector2Int, bool> isRoad)
        {
            if (isRoad == null)
            {
                return Empty;
            }

            List<ScannerDirection> directions = new List<ScannerDirection>(Neighbours.Length);
            for (int i = 0; i < Neighbours.Length; i++)
            {
                Neighbour neighbour = Neighbours[i];
                if (isRoad(new Vector2Int(origin.x + neighbour.X, origin.y + neighbour.Y)))
                {
                    directions.Add(neighbour.Direction);
                }
            }

            return directions;
        }

        private struct Neighbour
        {
            public Neighbour(int x, int y, ScannerDirection direction)
            {
                X = x;
                Y = y;
                Direction = direction;
            }

            public readonly int X;

            public readonly int Y;

            public readonly ScannerDirection Direction;
        }
    }
}
