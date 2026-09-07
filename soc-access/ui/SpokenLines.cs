using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The lines a review buffer holds, from text the game wrote for its own renderer.
    ///
    /// A tooltip or a details block arrives as the game drew it: Unity rich-text tags
    /// ("&lt;color=#decca8&gt;", "&lt;i&gt;", "&lt;hl&gt;") and newlines where the game broke the
    /// paragraph. A screen reader must hear neither the tags nor a whole block as one breath, so
    /// every raw string is split on its newlines FIRST and each line then loses its tags and its
    /// doubled spaces. The order matters: a normaliser that collapses whitespace before splitting
    /// swallows the newlines, which is the defect the repo's rule against
    /// <c>SpeechTextSanitizer.Normalize</c> exists for.
    ///
    /// &lt;br&gt; IS A LINE BREAK, not a tag to drop: the game writes rows with it where it does not
    /// write a newline (the spellbook's essence tooltip is
    /// "&lt;hl&gt;Essence&lt;/hl&gt;&lt;br&gt;Order controls and enhances..."), and dropping it ran
    /// the two rows into one word - "EssenceOrder controls and enhances...". It becomes a newline
    /// before the split, so the rows the game drew are the lines that are heard.
    /// </summary>
    public static class SpokenLines
    {
        private static readonly Regex Tags = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex LineBreaksDrawn =
            new Regex("<br\\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Spaces = new Regex("[ \\t]+", RegexOptions.Compiled);
        private static readonly char[] LineBreaks = { '\n', '\r' };

        /// <summary>Every non-empty line of every raw string, in order, tags removed.</summary>
        public static IList<string> Of(IEnumerable<string> raw)
        {
            List<string> lines = new List<string>();
            if (raw == null)
            {
                return lines;
            }

            foreach (string text in raw)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                foreach (string part in LineBreaksDrawn.Replace(text, "\n").Split(LineBreaks))
                {
                    string line = Spaces.Replace(Tags.Replace(part, string.Empty), " ").Trim();
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines;
        }
    }
}
