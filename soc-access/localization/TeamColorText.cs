using SongsOfConquest.Common;

namespace SongsOfConquestAccess.Localization
{
    internal static class TeamColorText
    {
        public static string Get(TeamColor color)
        {
            switch (color)
            {
                case TeamColor.Black:
                    return ModText.Get(ModStrings.TeamColors.Black);
                case TeamColor.Blue:
                    return ModText.Get(ModStrings.TeamColors.Blue);
                case TeamColor.DarkRed:
                    return ModText.Get(ModStrings.TeamColors.DarkRed);
                case TeamColor.Green:
                    return ModText.Get(ModStrings.TeamColors.Green);
                case TeamColor.Neutral:
                    return ModText.Get(ModStrings.TeamColors.Neutral);
                case TeamColor.Orange:
                    return ModText.Get(ModStrings.TeamColors.Orange);
                case TeamColor.Pink:
                    return ModText.Get(ModStrings.TeamColors.Pink);
                case TeamColor.Purple:
                    return ModText.Get(ModStrings.TeamColors.Purple);
                case TeamColor.Red:
                    return ModText.Get(ModStrings.TeamColors.Red);
                case TeamColor.Teal:
                    return ModText.Get(ModStrings.TeamColors.Teal);
                case TeamColor.Yellow:
                    return ModText.Get(ModStrings.TeamColors.Yellow);
                default:
                    return string.Empty;
            }
        }
    }
}
