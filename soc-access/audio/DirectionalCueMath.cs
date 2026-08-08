using System;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    internal enum CueGridGeometry
    {
        Square,
        Hex
    }

    /// <summary>
    /// Encodes the offset from the cursor to a remote tile into cue render offsets: horizontal
    /// distance becomes pan, vertical distance becomes pitch, straight-line distance becomes gain.
    /// Ported from the retired scanner directional beep so the encoding stays familiar.
    /// </summary>
    internal static class DirectionalCueMath
    {
        private const int SquarePanSaturationTiles = 12;
        private const int HexPanSaturationTiles = 6;
        private const int SquarePitchSemitonesPerRow = 1;
        private const int HexPitchSemitonesPerRow = 2;
        private const int PitchMaxSemitones = 12;
        private const float AudibleDistanceTiles = 30f;

        /// <summary>
        /// Returns false when the target is beyond audible range, in which case the cue is dropped
        /// rather than played at zero gain. The cursor's own tile is flat and at full gain.
        /// </summary>
        public static bool TryCompute(
            Vector2Int origin,
            Vector2Int target,
            CueGridGeometry geometry,
            out float pan,
            out float semitones,
            out float gainScale)
        {
            pan = 0f;
            semitones = 0f;
            gainScale = 1f;
            if (origin == target)
            {
                return true;
            }

            int rowDelta = target.y - origin.y;
            float columnDelta;
            float distance;
            int panSaturationTiles;
            int pitchSemitonesPerRow;
            if (geometry == CueGridGeometry.Hex)
            {
                columnDelta = HexColumnDelta(origin, target);
                distance = HexDistance(origin, target);
                panSaturationTiles = HexPanSaturationTiles;
                pitchSemitonesPerRow = HexPitchSemitonesPerRow;
            }
            else
            {
                int columns = target.x - origin.x;
                columnDelta = columns;
                distance = (float)Math.Sqrt((double)columns * columns + (double)rowDelta * rowDelta);
                panSaturationTiles = SquarePanSaturationTiles;
                pitchSemitonesPerRow = SquarePitchSemitonesPerRow;
            }

            float gain = Clamp(1f - distance / AudibleDistanceTiles, 0f, 1f);
            if (gain <= 0f)
            {
                return false;
            }

            pan = Clamp(columnDelta / panSaturationTiles, -1f, 1f);
            semitones = Clamp(rowDelta * pitchSemitonesPerRow, -PitchMaxSemitones, PitchMaxSemitones);
            gainScale = gain;
            return true;
        }

        /// <summary>Odd rows sit half a column to the right on a staggered hex grid.</summary>
        public static float HexColumnDelta(Vector2Int origin, Vector2Int target)
        {
            return target.x + ((target.y & 1) == 0 ? 0f : 0.5f)
                - origin.x - ((origin.y & 1) == 0 ? 0f : 0.5f);
        }

        public static int HexDistance(Vector2Int left, Vector2Int right)
        {
            int leftX;
            int leftY;
            int leftZ;
            OffsetToCube(left, out leftX, out leftY, out leftZ);

            int rightX;
            int rightY;
            int rightZ;
            OffsetToCube(right, out rightX, out rightY, out rightZ);

            return Math.Max(Math.Abs(leftX - rightX), Math.Max(Math.Abs(leftY - rightY), Math.Abs(leftZ - rightZ)));
        }

        private static void OffsetToCube(Vector2Int point, out int cubeX, out int cubeY, out int cubeZ)
        {
            cubeX = point.x - (point.y - (point.y & 1)) / 2;
            cubeZ = point.y;
            cubeY = -cubeX - cubeZ;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
