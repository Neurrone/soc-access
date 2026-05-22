using BepInEx.Configuration;

namespace SongsOfConquestAccess
{
    internal static class ModSettings
    {
        private static ConfigFile _config;
        private static ConfigEntry<bool> _readEnemyInfluence;
        private static ConfigEntry<bool> _readStoryCameraFocusChanges;
        private static ConfigEntry<bool> _scannerPlaysDirectionalBeep;

        public static bool ReadEnemyInfluence
        {
            get { return _readEnemyInfluence == null || _readEnemyInfluence.Value; }
        }

        public static bool ReadStoryCameraFocusChanges
        {
            get { return _readStoryCameraFocusChanges == null || _readStoryCameraFocusChanges.Value; }
        }

        public static bool ScannerPlaysDirectionalBeep
        {
            get { return _scannerPlaysDirectionalBeep != null && _scannerPlaysDirectionalBeep.Value; }
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
            _scannerPlaysDirectionalBeep = config.Bind(
                "Scanner",
                "ScannerPlaysDirectionalBeep",
                false,
                "Whether scanner result navigation should play a directional beep.");
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

        public static void SetScannerPlaysDirectionalBeep(bool value)
        {
            if (_scannerPlaysDirectionalBeep == null)
            {
                return;
            }

            _scannerPlaysDirectionalBeep.Value = value;
            _config?.Save();
        }

        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
            _readStoryCameraFocusChanges = null;
            _scannerPlaysDirectionalBeep = null;
        }
    }
}
