using BepInEx.Configuration;

namespace SongsOfConquestAccess
{
    internal static class ModSettings
    {
        private static ConfigFile _config;
        private static ConfigEntry<bool> _readEnemyInfluence;
        private static ConfigEntry<bool> _readStoryCameraFocusChanges;

        public static bool ReadEnemyInfluence
        {
            get { return _readEnemyInfluence == null || _readEnemyInfluence.Value; }
        }

        public static bool ReadStoryCameraFocusChanges
        {
            get { return _readStoryCameraFocusChanges == null || _readStoryCameraFocusChanges.Value; }
        }

        public static void Bind(ConfigFile config)
        {
            _config = config;
            _readEnemyInfluence = config.Bind(
                "Combat",
                "ReadEnemyInfluence",
                true,
                "Whether combat tile speech should include enemy influence information.");
            _readStoryCameraFocusChanges = config.Bind(
                "Story",
                "ReadStoryCameraFocusChanges",
                true,
                "Whether story camera focus change events should be read.");
        }

        public static void SetReadEnemyInfluence(bool value)
        {
            if (_readEnemyInfluence == null)
            {
                return;
            }

            _readEnemyInfluence.Value = value;
            _config?.Save();
        }

        public static void SetReadStoryCameraFocusChanges(bool value)
        {
            if (_readStoryCameraFocusChanges == null)
            {
                return;
            }

            _readStoryCameraFocusChanges.Value = value;
            _config?.Save();
        }

        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
            _readStoryCameraFocusChanges = null;
        }
    }
}
