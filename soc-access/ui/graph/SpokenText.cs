using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// "Has the player already been told this?" - the one comparison every surface that drops a
    /// repeat asks.
    ///
    /// A readout, a tooltip's sections, a review buffer's head and the tooltip-kind rule each decide
    /// whether a line they are about to add is one the reading already carries, and they have to agree
    /// about it: a line one of them calls a repeat and another calls new is said once on one surface
    /// and twice on the next. The rule is trimmed, case-insensitive, whole-line - two lines are the
    /// same line when nothing but spacing and capitals separates them, and a heading that ADDS a word
    /// is a different line and still reads.
    ///
    /// Blank is never the same as anything, including another blank: a surface with nothing to say has
    /// not said this line, it has said nothing.
    /// </summary>
    public static class SpokenText
    {
        /// <summary>Whether these two are the same spoken line.</summary>
        public static bool SameLine(string left, string right)
        {
            return !TextUtil.IsBlank(left)
                && !TextUtil.IsBlank(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether any of <paramref name="lines"/> is the same spoken line as
        /// <paramref name="line"/>.</summary>
        public static bool Mentions(IList<string> lines, string line)
        {
            return Mentions(lines, lines == null ? 0 : lines.Count, line);
        }

        /// <summary>The same question asked of the FIRST <paramref name="upTo"/> of them - for a
        /// caller comparing against the part of a reading that was settled before this one, rather
        /// than against the whole of it.</summary>
        public static bool Mentions(IList<string> lines, int upTo, string line)
        {
            for (int i = 0; lines != null && i < upTo && i < lines.Count; i++)
            {
                if (SameLine(lines[i], line))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
