using UnityEngine;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech
{
    internal static class ArtifactSpeechFormatter
    {
        public static string FormatName(string name, Color powerLevelColor)
        {
            return FormatName(null, name, powerLevelColor);
        }

        public static string FormatName(ILocalizationHandler localization, string name, Color powerLevelColor)
        {
            string color = GetRarityLabel(localization, powerLevelColor);
            return string.IsNullOrWhiteSpace(color) || string.IsNullOrWhiteSpace(name)
                ? name
                : ModText.Get(localization, ModStrings.Artifacts.NameWithColor, name, color);
        }

        public static string FormatName(
            IArtifactState artifact,
            IArtifactLookup artifactLookup,
            ILocalizationHandler localization = null)
        {
            if (artifact == null)
            {
                return string.Empty;
            }

            if (artifactLookup == null)
            {
                return artifact.Type.ToString();
            }

            string name = artifactLookup.GetLocalizedName(artifact.Type);
            return FormatName(localization, name, artifactLookup.GetPowerLevelColor(artifact.Type));
        }

        public static bool TryFormatName(
            IDetails details,
            ILocalizationHandler localization,
            out string formattedName)
        {
            formattedName = string.Empty;

            if (details is ArtifactDetails artifactDetails)
            {
                return TryFormatName(artifactDetails, localization, out formattedName);
            }

            ArtifactPreVisitDetails preVisitDetails = details as ArtifactPreVisitDetails;
            if (preVisitDetails != null)
            {
                return TryFormatName(preVisitDetails, localization, out formattedName);
            }

            return false;
        }

        public static bool TryFormatName(
            ArtifactDetails artifactDetails,
            ILocalizationHandler localization,
            out string formattedName)
        {
            formattedName = string.Empty;
            if (localization == null)
            {
                return false;
            }

            string name = localization.GetText(artifactDetails.NameKey);
            formattedName = FormatName(localization, name, artifactDetails.PowerLevelColor);
            return !string.IsNullOrWhiteSpace(formattedName);
        }

        public static bool TryFormatName(
            ArtifactPreVisitDetails artifactDetails,
            ILocalizationHandler localization,
            out string formattedName)
        {
            formattedName = string.Empty;
            if (artifactDetails == null || artifactDetails.Artifacts == null || localization == null)
            {
                return false;
            }

            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < artifactDetails.Artifacts.Length; i++)
            {
                string name;
                if (TryFormatName(artifactDetails.Artifacts[i], localization, out name))
                {
                    names.Add(name);
                }
            }

            formattedName = ModText.JoinList(localization, names);
            return !string.IsNullOrWhiteSpace(formattedName);
        }

        public static string GetRarityLabel(Color powerLevelColor)
        {
            return GetRarityLabel(null, powerLevelColor);
        }

        public static string GetRarityLabel(ILocalizationHandler localization, Color powerLevelColor)
        {
            switch (ColorUtility.ToHtmlStringRGBA(powerLevelColor))
            {
                case "D4D4D4C2":
                    return ModText.Get(localization, ModStrings.Artifacts.ColorGrey);
                case "4CA41DFF":
                    return ModText.Get(localization, ModStrings.Artifacts.ColorGreen);
                case "327FF8FF":
                    return ModText.Get(localization, ModStrings.Artifacts.ColorBlue);
                case "D245E9FF":
                    return ModText.Get(localization, ModStrings.Artifacts.ColorViolet);
                case "EF7D21FF":
                    return ModText.Get(localization, ModStrings.Artifacts.ColorOrange);
                default:
                    return string.Empty;
            }
        }
    }
}
