using System.Globalization;
using UnityEngine;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class HexCoordinateFormatter
    {
        public static string Format(Vector2Int point)
        {
            return Format(point, Vector2Int.zero);
        }

        public static string Format(Vector2Int point, Vector2Int origin)
        {
            int doubledX = ((point.x - origin.x) * 2) + ((point.y & 1) - (origin.y & 1));
            int y = point.y - origin.y;
            return FormatDoubledHalf(doubledX) + ", " + y.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDoubledHalf(int doubled)
        {
            if ((doubled & 1) == 0)
            {
                return (doubled / 2).ToString(CultureInfo.InvariantCulture);
            }

            string sign = doubled < 0 ? "-" : string.Empty;
            int magnitude = doubled < 0 ? -doubled : doubled;
            int whole = magnitude / 2;
            return sign + whole.ToString(CultureInfo.InvariantCulture) + ".5";
        }
    }
}
