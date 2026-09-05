using SongsOfConquestAccess.Loader.Dev;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// Everything the mod has spoken, so a developer or agent who cannot hear the screen reader can
    /// read it back over HTTP.
    ///
    /// It is the loader's own ring (<see cref="SeqLog"/>) with the mod's capacity on it, and nothing
    /// else: the sequencing, the cursor, the blocking wait for the next line and the settle poll are
    /// all one implementation.
    ///
    /// Written from the Unity main thread (the speech pump) and read from HTTP handler threads.
    /// </summary>
    public sealed class SpeechLog : SeqLog
    {
        /// <summary>How many lines are kept. Longer than the loader's own log, because the question
        /// asked of this one is usually "what did that whole walk say".</summary>
        private const int Capacity = 1000;

        public SpeechLog()
            : base(Capacity) { }
    }
}
