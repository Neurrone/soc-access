using BepInEx.Logging;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// Copies everything BepInEx logs - the loader, the mod, Harmony, other plugins and Unity
    /// itself - into the ring buffer GET /log serves, so an agent that cannot watch the console
    /// reads the same stream over HTTP with a cursor instead of a tail.
    ///
    /// BepInEx raises log events on whichever thread logged, so the buffer behind this has to be
    /// thread-safe. Registered on the listener list while the dev server is up, and taken off
    /// again when it stops.
    /// </summary>
    internal sealed class BepInExLogTap : ILogListener
    {
        private readonly SeqLog _log;

        public BepInExLogTap(SeqLog log)
        {
            _log = log;
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            ILogSource source = eventArgs.Source;
            _log.Add(
                eventArgs.Level
                    + ":"
                    + (source == null ? "?" : source.SourceName)
                    + "| "
                    + eventArgs.Data
            );
        }

        public void Dispose()
        {
        }
    }
}
