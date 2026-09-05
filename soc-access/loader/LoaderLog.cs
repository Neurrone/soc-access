using System;

namespace SongsOfConquestAccess.Loader
{
    /// <summary>
    /// Logging seam for the loader, wired to the BepInEx logger in
    /// <see cref="LoaderPlugin"/>. Deliberately separate from the mod's
    /// the mod logger: the loader has to keep reporting when the mod assembly
    /// failed to load, or was unloaded, and it must not depend on anything the mod owns.
    /// </summary>
    internal static class LoaderLog
    {
        private static Action<string> _info;
        private static Action<string> _warn;
        private static Action<string> _error;

        public static void Install(Action<string> info, Action<string> warn, Action<string> error)
        {
            _info = info;
            _warn = warn;
            _error = error;
        }

        public static void Info(string message)
        {
            if (_info != null)
            {
                _info(message);
            }
        }

        public static void Warn(string message)
        {
            if (_warn != null)
            {
                _warn(message);
            }
        }

        public static void Error(string message)
        {
            if (_error != null)
            {
                _error(message);
            }
        }
    }
}
