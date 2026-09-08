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
    }
}
