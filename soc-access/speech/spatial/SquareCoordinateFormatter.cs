using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Speech.Spatial
{
    /// <summary>
    /// A square-grid tile's coordinates as they are spoken: the counterpart of
    /// <see cref="HexCoordinateFormatter"/> for the adventure map, whose columns and rows are both
    /// whole numbers. The pair goes through one <see cref="ModStrings.Spatial.Coordinates"/> so a
    /// language can order and punctuate the two numbers its own way.
    /// </summary>
    public static class SquareCoordinateFormatter
    {
        public static string Format(Vector2Int point)
        {
            return Format(point.x, point.y);
        }

        public static string Format(int x, int y)
        {
            return ModText.Get(ModStrings.Spatial.Coordinates, x, y);
        }
    }
}
