using System;
using SongsOfConquestAccess.Loader;

namespace SongsOfConquestAccess
{
    /// <summary>The only entry points called by the persistent loader.</summary>
    public static class ModEntry
    {
        /// <summary>The mod's version, as the build stamped it into this assembly - the one source
        /// is <c>&lt;Version&gt;</c> in soc-access.csproj, so a release bump cannot leave the spoken
        /// startup line, the dev server and the DLL disagreeing. Read from metadata rather than from
        /// a file, which is what makes it work under the loader's load-from-bytes path (there is no
        /// path on disk to ask). Falls back on the numeric assembly version, which the build always
        /// writes.</summary>
        public static readonly string ModVersion = ReadVersion();

        private static string ReadVersion()
        {
            try
            {
                System.Reflection.Assembly assembly = typeof(ModEntry).Assembly;
                System.Reflection.AssemblyInformationalVersionAttribute stamped =
                    (System.Reflection.AssemblyInformationalVersionAttribute)
                        Attribute.GetCustomAttribute(
                            assembly,
                            typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                return stamped == null || string.IsNullOrEmpty(stamped.InformationalVersion)
                    ? assembly.GetName().Version.ToString()
                    : stamped.InformationalVersion;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

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
