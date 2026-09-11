using System;
using SongsOfConquestAccess.Input;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The hex board's geometry and the keys that walk it, shared by the battlefield
    /// (<see cref="CombatHexGrid"/>) and the troop-placement board
    /// (<see cref="TroopPlacementHexGrid"/>) so the two step identically. The six directions, their
    /// six skips and the centre tile; what each key MEANS on a landing is still the grid's, which is
    /// why the centre tile is a constant here and not a move.
    ///
    /// Odd rows sit half a tile to the east, so a diagonal's x step depends on the row's parity while
    /// its y step does not.
    /// </summary>
    public static class HexGridMoves
    {
        /// <summary>The middle of both boards, which are the same size.</summary>
        public static readonly Vector2Int CenterTile = new Vector2Int(6, 4);

        private static readonly Func<Vector2Int, Vector2Int> West = point => new Vector2Int(point.x - 1, point.y);
        private static readonly Func<Vector2Int, Vector2Int> East = point => new Vector2Int(point.x + 1, point.y);
        private static readonly Func<Vector2Int, Vector2Int> NorthWest = point => Neighbor(point, north: true, east: false);
        private static readonly Func<Vector2Int, Vector2Int> NorthEast = point => Neighbor(point, north: true, east: true);
        private static readonly Func<Vector2Int, Vector2Int> SouthWest = point => Neighbor(point, north: false, east: false);
        private static readonly Func<Vector2Int, Vector2Int> SouthEast = point => Neighbor(point, north: false, east: true);

        public static Vector2Int Neighbor(Vector2Int point, bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((point.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return new Vector2Int(point.x + xDelta, point.y + yDelta);
        }

        /// <summary>The twelve walking keys and the centre-tile jump.</summary>
        public static bool ClaimsAction(string actionKey)
        {
            bool skip;
            return TryGetStep(actionKey, out skip) != null
                || actionKey == AccessibilityActions.HexGridFocusCenterTile.Key;
        }

        /// <summary>The step a walking key takes from a tile, or null where the key is not one of
        /// them. <paramref name="skip"/> says whether it is the skipping form, which runs the step
        /// until the board changes under it rather than taking it once.</summary>
        public static Func<Vector2Int, Vector2Int> TryGetStep(string actionKey, out bool skip)
        {
            skip = false;
            if (actionKey == AccessibilityActions.HexGridWest.Key)
            {
                return West;
            }

            if (actionKey == AccessibilityActions.HexGridEast.Key)
            {
                return East;
            }

            if (actionKey == AccessibilityActions.HexGridNorthWest.Key)
            {
                return NorthWest;
            }

            if (actionKey == AccessibilityActions.HexGridNorthEast.Key)
            {
                return NorthEast;
            }

            if (actionKey == AccessibilityActions.HexGridSouthWest.Key)
            {
                return SouthWest;
            }

            if (actionKey == AccessibilityActions.HexGridSouthEast.Key)
            {
                return SouthEast;
            }

            skip = true;
            if (actionKey == AccessibilityActions.HexGridSkipWest.Key)
            {
                return West;
            }

            if (actionKey == AccessibilityActions.HexGridSkipEast.Key)
            {
                return East;
            }

            if (actionKey == AccessibilityActions.HexGridSkipNorthWest.Key)
            {
                return NorthWest;
            }

            if (actionKey == AccessibilityActions.HexGridSkipNorthEast.Key)
            {
                return NorthEast;
            }

            if (actionKey == AccessibilityActions.HexGridSkipSouthWest.Key)
            {
                return SouthWest;
            }

            if (actionKey == AccessibilityActions.HexGridSkipSouthEast.Key)
            {
                return SouthEast;
            }

            skip = false;
            return null;
        }
    }
}
