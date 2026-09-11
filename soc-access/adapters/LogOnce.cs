using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// A guarded game call that threw, reported once per subject for the life of the mod load.
    ///
    /// For the STATIC helper classes, which have no instance to hang a "said it already" flag on.
    /// An adapter with an instance keeps its own set instead, so its failures are reported once per
    /// menu rather than once per session.
    ///
    /// Once matters because these sit on paths the map walks per tile: a scanner snapshot reads
    /// thousands of tiles, and a warning per failure would bury the log it exists to fill. What is
    /// remembered is the SUBJECT string each call site passes, so the set is bounded by the number of
    /// call sites and holds nothing belonging to the game. There is no reset and none is needed: it
    /// lives in the mod assembly, so a hot reload replaces it wholesale and the next load reports
    /// afresh (AGENTS.md, reload safety - the same reason <c>FrameSweep</c> needs no teardown).
    /// </summary>
    public static class LogOnce
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="subject">What was being read, phrased so the log line reads as a sentence.
        /// One fixed string per call site.</param>
        public static void Warn(string subject, Exception exception)
        {
            if (!Reported.Add(subject))
            {
                return;
            }

            SocAccessMod.Instance?.LogWarning(subject + " threw: " + exception);
        }
    }
}
