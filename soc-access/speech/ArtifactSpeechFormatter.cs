using UnityEngine;

namespace SongsOfConquestAccess.Speech
{
    internal static class ArtifactSpeechFormatter
    {
        public static string FormatName(string name, Color powerLevelColor)
        {
            string rarity = GetRarityLabel(powerLevelColor);
            return string.IsNullOrWhiteSpace(rarity) ? name : name + " (" + rarity + ")";
        }

        public static string GetRarityLabel(Color powerLevelColor)
        {
            switch (ColorUtility.ToHtmlStringRGBA(powerLevelColor))
            {
                case "D4D4D4C2":
                    return "grey";
                case "4CA41DFF":
                    return "green";
                case "327FF8FF":
                    return "blue";
                case "D245E9FF":
                    return "violet";
                case "EF7D21FF":
                    return "orange";
                default:
                    return string.Empty;
            }
        }
    }
}
