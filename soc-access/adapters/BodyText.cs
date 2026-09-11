using System.Collections.Generic;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE PARAGRAPHS OF ONE DRAWN BODY, SPLIT ONCE A FRAME.
    ///
    /// A dialog's body is asked for several times in one build - by the guard that says whether
    /// there is one, by the node that reads it, and by the screen's own name, which joins the
    /// paragraphs back together - and every ask ran the same three regexes over every line of it
    /// (AGENTS.md, Performance).
    ///
    /// The key is the frame AND the raw text, so a source that rewrites its line in place - the
    /// story text does, a letter at a time as it types - is followed rather than remembered, and
    /// nothing survives the frame it was read in.
    ///
    /// One per adapter instance, holding nothing but strings.
    /// </summary>
    public sealed class BodyText
    {
        private static readonly IList<string> None = new string[0];

        private string _raw;
        private IList<string> _lines;
        private int _frame = -1;

        /// <summary>The paragraphs of the text, as the game broke them.</summary>
        public IList<string> Lines(string raw)
        {
            int frame = Time.frameCount;
            if (_lines != null && _frame == frame && string.Equals(_raw, raw))
            {
                return _lines;
            }

            _frame = frame;
            _raw = raw;
            _lines = SpokenLines.Of(new[] { raw }) ?? None;
            return _lines;
        }

        /// <summary>Those paragraphs run back together into one line.</summary>
        public string Joined(string raw)
        {
            IList<string> lines = Lines(raw);
            return lines.Count == 0 ? string.Empty : string.Join(" ", lines);
        }

        /// <summary>Whether the text says anything at all, asked without the caller splitting it.
        /// </summary>
        public bool HasAny(string raw)
        {
            return Lines(raw).Count > 0;
        }
    }
}
