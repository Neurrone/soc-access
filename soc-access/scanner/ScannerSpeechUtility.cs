using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerSpeechUtility
    {
        public static string FormatDirections(IReadOnlyList<ScannerDirectionStep> directions)
        {
            return FormatDirections(directions, ModSettings.ScannerUsesLongDirections);
        }

        public static string FormatDirections(IReadOnlyList<ScannerDirectionStep> directions, bool useLongForm)
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
        /// Counts the copies of the current item, always. "1 of 8" says there
        /// are eight of these and this is the nearest, which is worth a word
        /// even when the answer is one, and the player can silence the element
        /// outright if they disagree.
        /// </summary>
        public static string FormatResultCount(int index, int count)
        {
            return ModText.Get(ModStrings.Common.CountOf, index, count);
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
