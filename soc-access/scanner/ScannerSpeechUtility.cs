using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerSpeechUtility
    {
        public static string FormatDirections(IReadOnlyList<ScannerDirectionStep> directions)
        {
            return FormatDirections(directions, ModSettings.ScannerUsesLongDirections);
        }

        internal static string FormatDirections(IReadOnlyList<ScannerDirectionStep> directions, bool useLongForm)
        {
            if (directions == null || directions.Count == 0)
            {
                return ModText.Get(ModStrings.Spatial.Here);
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < directions.Count; i++)
            {
                string step = ScannerDirectionUtility.FormatStep(directions[i], useLongForm);
                if (!string.IsNullOrWhiteSpace(step))
                {
                    parts.Add(step);
                }
            }

            return parts.Count == 0 ? ModText.Get(ModStrings.Spatial.Here) : ModText.JoinListWithCommas(parts);
        }

        /// <summary>
        /// Counts the copies of the current item. An item with only one copy
        /// says nothing rather than "1 of 1", so the readout appearing at all is
        /// the signal that there is more of this thing to walk through.
        /// </summary>
        public static string FormatResultCount(int index, int count)
        {
            return count <= 1 ? string.Empty : ModText.Get(ModStrings.Common.CountOf, index, count);
        }

        public static void AddIfPresent(List<string> parts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }
    }
}
