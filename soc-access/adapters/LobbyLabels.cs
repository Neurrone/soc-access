using SongsOfConquest.Common;
using SongsOfConquest.Common.Ai;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>What a lobby setting is set to, in the game's own words. A player's row and the
    /// dropdown that changes it read the same four settings from different places - the row from the
    /// lobby team state, the dropdown from the entry it draws - so the reading differs and the naming
    /// does not.</summary>
    public static class LobbyLabels
    {
        /// <summary>The index the game uses for "any faction", which has a name of its own rather
        /// than a faction definition.</summary>
        public const int RandomFactionIndex = 99;

        private const string RandomKey = "Factions/Random/Name";

        public static string Color(int colorIndex)
        {
            if (colorIndex < 0)
            {
                return string.Empty;
            }

            return TeamColorText.Get(TeamColorExtensions.GetTeamColorFromIndex(colorIndex));
        }

        public static string Faction(ILocalizationHandler localization, IFactionLookup lookup, int factionIndex)
        {
            if (factionIndex == RandomFactionIndex)
            {
                return SpokenText.Get(localization, RandomKey, string.Empty);
            }

            IFactionDefinition faction = lookup != null ? lookup.GetFaction(factionIndex) : null;
            return faction != null ? SpokenText.Get(localization, faction.NameKey, string.Empty) : string.Empty;
        }

        public static string Wielder(
            ILocalizationHandler localization,
            IWielderLookup lookup,
            CommanderReference reference)
        {
            if (reference == CommanderReference.Random)
            {
                return SpokenText.Get(localization, RandomKey, string.Empty);
            }

            ICommanderDefinition commander = lookup != null ? lookup.Get(reference) : null;
            return commander != null ? SpokenText.Get(localization, commander.NameKey, string.Empty) : string.Empty;
        }

        public static string AiDifficulty(ILocalizationHandler localization, AiDifficulty difficulty)
        {
            return SpokenText.Get(localization, "Common/AiMode/" + difficulty, string.Empty);
        }
    }
}
