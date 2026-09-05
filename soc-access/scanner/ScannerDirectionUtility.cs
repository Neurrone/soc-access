using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerDirectionUtility
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
        /// Names the direction of a single step onto a neighbouring tile. Returns false for a
        /// zero offset, and for anything further than one tile away, which is not a step.
        /// </summary>
        public static bool TryGetStepDirection(Vector2Int offset, out ScannerDirection direction)
        {
            direction = ScannerDirection.North;
            if (offset == Vector2Int.zero || Mathf.Abs(offset.x) > 1 || Mathf.Abs(offset.y) > 1)
            {
                return false;
            }

            if (offset.y > 0)
            {
                direction = offset.x > 0
                    ? ScannerDirection.Northeast
                    : offset.x < 0 ? ScannerDirection.Northwest : ScannerDirection.North;
            }
            else if (offset.y < 0)
            {
                direction = offset.x > 0
                    ? ScannerDirection.Southeast
                    : offset.x < 0 ? ScannerDirection.Southwest : ScannerDirection.South;
            }
            else
            {
                direction = offset.x > 0 ? ScannerDirection.East : ScannerDirection.West;
            }

            return true;
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

        public static string FormatStep(ScannerDirectionStep step, bool useLongForm)
        {
            if (step == null || step.Count <= 0)
            {
                return string.Empty;
            }

            return ModText.Get(
                useLongForm ? ModStrings.Scanner.DirectionStepLong : ModStrings.Scanner.DirectionStep,
                step.Count,
                FormatDirection(step.Direction, useLongForm));
        }

        /// <summary>
        /// Names a direction on its own, without a distance in front of it. Short form gives
        /// "ne"; the Long directions setting gives "northeast".
        /// </summary>
        public static string FormatDirection(ScannerDirection direction, bool useLongForm)
        {
            return ModText.Get(useLongForm ? LongForm(direction) : ShortForm(direction));
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
