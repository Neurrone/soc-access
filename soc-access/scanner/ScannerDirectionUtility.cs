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
                result.Add(new ScannerDirectionStep(y, ScannerDirection.North));
            }
            else if (y < 0)
            {
                result.Add(new ScannerDirectionStep(-y, ScannerDirection.South));
            }

            if (x > 0)
            {
                result.Add(new ScannerDirectionStep(x, ScannerDirection.East));
            }
            else if (x < 0)
            {
                result.Add(new ScannerDirectionStep(-x, ScannerDirection.West));
            }

            return result;
        }

        /// <summary>
        /// Formats one step. Short form is the default and gives "3ne"; the Long
        /// directions setting gives "3 northeast". The count and direction are
        /// joined through a format string of their own so a translator can add a
        /// separator or reorder the two.
        /// </summary>
        public static string FormatStep(ScannerDirectionStep step)
        {
            return FormatStep(step, ModSettings.ScannerUsesLongDirections);
        }

        internal static string FormatStep(ScannerDirectionStep step, bool useLongForm)
        {
            if (step == null || step.Count <= 0)
            {
                return string.Empty;
            }

            return ModText.Get(
                useLongForm ? ModStrings.Scanner.DirectionStepLong : ModStrings.Scanner.DirectionStep,
                step.Count,
                ModText.Get(useLongForm ? LongForm(step.Direction) : ShortForm(step.Direction)));
        }

        private static ModString LongForm(ScannerDirection direction)
        {
            switch (direction)
            {
                case ScannerDirection.North:
                    return ModStrings.Scanner.North;
                case ScannerDirection.South:
                    return ModStrings.Scanner.South;
                case ScannerDirection.East:
                    return ModStrings.Scanner.East;
                case ScannerDirection.West:
                    return ModStrings.Scanner.West;
                case ScannerDirection.Northeast:
                    return ModStrings.Scanner.Northeast;
                case ScannerDirection.Northwest:
                    return ModStrings.Scanner.Northwest;
                case ScannerDirection.Southeast:
                    return ModStrings.Scanner.Southeast;
                default:
                    return ModStrings.Scanner.Southwest;
            }
        }

        private static ModString ShortForm(ScannerDirection direction)
        {
            switch (direction)
            {
                case ScannerDirection.North:
                    return ModStrings.Scanner.NorthShort;
                case ScannerDirection.South:
                    return ModStrings.Scanner.SouthShort;
                case ScannerDirection.East:
                    return ModStrings.Scanner.EastShort;
                case ScannerDirection.West:
                    return ModStrings.Scanner.WestShort;
                case ScannerDirection.Northeast:
                    return ModStrings.Scanner.NortheastShort;
                case ScannerDirection.Northwest:
                    return ModStrings.Scanner.NorthwestShort;
                case ScannerDirection.Southeast:
                    return ModStrings.Scanner.SoutheastShort;
                default:
                    return ModStrings.Scanner.SouthwestShort;
            }
        }
    }
}
