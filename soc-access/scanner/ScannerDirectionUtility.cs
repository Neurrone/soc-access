using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerDirectionUtility
    {
        public static IReadOnlyList<ScannerDirectionStep> BuildSquareDirections(Vector2Int origin, Vector2Int target)
        {
            List<ScannerDirectionStep> result = new List<ScannerDirectionStep>();
            int x = target.x - origin.x;
            int y = target.y - origin.y;
            if (y > 0)
            {
                result.Add(new ScannerDirectionStep(y, ModText.Get(ModStrings.Scanner.North)));
            }
            else if (y < 0)
            {
                result.Add(new ScannerDirectionStep(-y, ModText.Get(ModStrings.Scanner.South)));
            }

            if (x > 0)
            {
                result.Add(new ScannerDirectionStep(x, ModText.Get(ModStrings.Scanner.East)));
            }
            else if (x < 0)
            {
                result.Add(new ScannerDirectionStep(-x, ModText.Get(ModStrings.Scanner.West)));
            }

            return result;
        }
    }
}
