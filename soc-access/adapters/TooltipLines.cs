using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// A tooltip read as a single line: what a button whose face carries no text is called.
    ///
    /// <see cref="Tooltip.TextLines"/> is resolved LIVE - every read captures the game's details
    /// afresh - so it is read once here and walked from the local list. The five adapters that used
    /// to keep a copy of this asked for it in the loop condition and again in the indexer, which
    /// cost about 2N captures for an N-line tooltip on every label read.
    /// </summary>
    public static class TooltipLines
    {
        /// <summary>The tooltip's first line with anything to say, cleaned of the game's rich-text
        /// tags, or the empty string when the tooltip has none.</summary>
        public static string First(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
            if (lines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpokenLines.Clean(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>Every line but the ones a caller names, matched exactly as the game wrote
        /// them.</summary>
        public static IReadOnlyList<string> Without(IReadOnlyList<string> lines, IReadOnlyList<string> linesToRemove)
        {
            if (lines == null || lines.Count == 0 || linesToRemove == null || linesToRemove.Count == 0)
            {
                return lines ?? new string[0];
            }

            List<string> result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!Contains(linesToRemove, lines[i]))
                {
                    result.Add(lines[i]);
                }
            }

            return result;
        }

        /// <summary>Drop every copy of one line from a list being composed, matched as the game
        /// wrote it.</summary>
        public static void Remove(List<string> lines, string lineToRemove)
        {
            if (lines == null || string.IsNullOrWhiteSpace(lineToRemove))
            {
                return;
            }

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (string.Equals(lines[i], lineToRemove, StringComparison.Ordinal))
                {
                    lines.RemoveAt(i);
                }
            }
        }

        /// <summary>The list holds this line, character for character.</summary>
        public static bool Contains(IReadOnlyList<string> lines, string candidate)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (string.Equals(lines[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
