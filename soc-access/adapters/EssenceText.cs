using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The game's own words for an essence, said the way the game says them: the tables hold
    /// one name per type under "Units/Types/&lt;type&gt;" and one counted form beside it under
    /// "&lt;type&gt;Multiple", and the game composes both keys this way itself
    /// (<c>DetailUtilities</c>, <c>SpellCodexContent</c>).</summary>
    public static class EssenceText
    {
        public static string Name(ILocalizationHandler localization, EssenceType essenceType)
        {
            return SpokenText.Get(localization, Key(essenceType), string.Empty);
        }

        /// <summary>The essence named for a count: "3 Arcana" where the game has the counted form,
        /// and the plain name where the count is one.</summary>
        public static string Amount(ILocalizationHandler localization, EssenceType essenceType, int count)
        {
            string name = Name(localization, essenceType);
            if (count <= 1)
            {
                return name;
            }

            return SpokenText.Get(localization, Key(essenceType) + "Multiple", count + " " + name, count);
        }

        public static string Key(EssenceType essenceType)
        {
            return "Units/Types/" + essenceType;
        }
    }
}
