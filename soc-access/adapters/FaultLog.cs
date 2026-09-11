using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// WHAT WENT WRONG, SAID ONCE.
    ///
    /// An adapter that reflects into the game, or asks it for something it may not have, recovers
    /// rather than throwing: a missing panel reads as "not there" and a tile the game will not answer
    /// for reads as empty. That is right for the player and wrong for the next person to read the
    /// log, because a recovery nobody hears about is how a game patch turns into a silently wrong
    /// readout. So every such catch reports here, and the FIRST time each place fails it goes in the
    /// log; after that it is quiet, because these sit on per-frame paths and per-tile loops and a
    /// line each would bury everything else.
    ///
    /// One per adapter instance, so it lives exactly as long as the menu it is reading and needs no
    /// teardown of its own: the next menu starts with everything unsaid again (AGENTS.md, Screen
    /// Resolution).
    /// </summary>
    public sealed class FaultLog
    {
        private readonly HashSet<string> _said = new HashSet<string>(StringComparer.Ordinal);
        private readonly string _subject;

        public FaultLog(string subject)
        {
            _subject = subject;
        }

        /// <param name="where">What was being read, which is also what makes this the first time or
        /// not.</param>
        public void Report(string where, Exception exception)
        {
            if (!_said.Add(where))
            {
                return;
            }

            SocAccessMod.Instance?.LogWarning(
                _subject + "." + where + " failed and was recovered from: "
                + (exception != null ? exception.Message : string.Empty));
        }
    }
}
