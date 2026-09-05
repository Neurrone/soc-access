using System;
using SongsOfConquestAccess.Loader;

namespace SongsOfConquestAccess
{
    /// <summary>The only entry points called by the persistent loader.</summary>
    public static class ModEntry
    {
        private static SocAccessMod _mod;

        public static void Start(ModHost host)
        {
            if (_mod != null) throw new InvalidOperationException("Mod already started");
            _mod = new SocAccessMod(host);
            try { _mod.Start(); }
            catch
            {
                Stop();
                throw;
            }
        }

        public static void Stop()
        {
            SocAccessMod mod = _mod;
            _mod = null;
            mod?.Stop();
        }
    }
}
