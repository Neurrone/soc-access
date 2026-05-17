using BepInEx.Configuration;

namespace SongsOfConquestAccess
{
    internal static class ModSettings
    {
        private static ConfigFile _config;
        private static ConfigEntry<bool> _readEnemyInfluence;

        public static bool ReadEnemyInfluence
        {
            get { return _readEnemyInfluence == null || _readEnemyInfluence.Value; }
        }

        public static void Bind(ConfigFile config)
        {
            _config = config;
            _readEnemyInfluence = config.Bind(
                "Combat",
                "ReadEnemyInfluence",
                true,
                "Whether combat tile speech should include enemy influence information.");
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

        public static void Reset()
        {
            _config = null;
            _readEnemyInfluence = null;
        }
    }
}
