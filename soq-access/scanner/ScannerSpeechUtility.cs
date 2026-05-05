using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerSpeechUtility
    {
        public static string FormatDirections(IReadOnlyList<ScannerDirectionStep> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                return "here";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < directions.Count; i++)
            {
                ScannerDirectionStep step = directions[i];
                if (step != null && step.Count > 0 && !string.IsNullOrWhiteSpace(step.Direction))
                {
                    parts.Add(step.Count + " " + step.Direction);
                }
            }

            return parts.Count == 0 ? "here" : string.Join(", ", parts.ToArray());
        }

        public static string FormatResultCount(int index, int count)
        {
            return index + " of " + count;
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
