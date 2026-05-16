using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerFormatter
    {
        public static string FormatResult(ScannerResult result, Vector2Int origin, ScannerDirectionMode directionMode)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string label = result.Label ?? string.Empty;
            if (result.NotVisible)
            {
                label += ", unseen";
            }

            string direction = directionMode == ScannerDirectionMode.Hex
                ? FormatHexDirection(origin, result.Position)
                : FormatSquareDirection(origin, result.Position);

            return string.IsNullOrWhiteSpace(direction) ? label : label + ". " + direction;
        }

        private static string FormatSquareDirection(Vector2Int origin, Vector2Int target)
        {
            int x = target.x - origin.x;
            int y = target.y - origin.y;
            List<string> parts = new List<string>();
            if (y > 0)
            {
                parts.Add(y + " north");
            }
            else if (y < 0)
            {
                parts.Add((-y) + " south");
            }

            if (x > 0)
            {
                parts.Add(x + " east");
            }
            else if (x < 0)
            {
                parts.Add((-x) + " west");
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string FormatHexDirection(Vector2Int origin, Vector2Int target)
        {
            int x = target.x - origin.x;
            int y = target.y - origin.y;
            List<string> parts = new List<string>();
            while (x != 0 || y != 0)
            {
                if (y > 0 && x > 0)
                {
                    int step = x < y ? x : y;
                    parts.Add(step + " northeast");
                    x -= step;
                    y -= step;
                }
                else if (y > 0 && x < 0)
                {
                    int step = -x < y ? -x : y;
                    parts.Add(step + " northwest");
                    x += step;
                    y -= step;
                }
                else if (y < 0 && x > 0)
                {
                    int step = x < -y ? x : -y;
                    parts.Add(step + " southeast");
                    x -= step;
                    y += step;
                }
                else if (y < 0 && x < 0)
                {
                    int step = -x < -y ? -x : -y;
                    parts.Add(step + " southwest");
                    x += step;
                    y += step;
                }
                else if (x > 0)
                {
                    parts.Add(x + " east");
                    x = 0;
                }
                else if (x < 0)
                {
                    parts.Add((-x) + " west");
                    x = 0;
                }
                else if (y > 0)
                {
                    parts.Add(y + " northeast");
                    y = 0;
                }
                else
                {
                    parts.Add((-y) + " southwest");
                    y = 0;
                }
            }

            return string.Join(", ", parts.ToArray());
        }
    }
}
