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

            if (parts.Count == 0)
            {
                return ModText.Get(ModStrings.Spatial.Here);
            }

            string text = ModText.JoinListWithCommas(parts);
            // A player who asked for the walkable path is owed the word when they are hearing the
            // straight line instead, and the list itself is all either caller passes down.
            ScannerDirections runs = directions as ScannerDirections;
            if (runs == null || !runs.IsStraightLineFallback)
            {
                return text;
            }

            return string.IsNullOrWhiteSpace(runs.BlockerName)
                ? ModText.Get(ModStrings.Scanner.StraightLineFallback, text)
                : ModText.Get(ModStrings.Scanner.BlockedStraightLineFallback, runs.BlockerName, text);
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
    }
}
