using System;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Adapters
{
    public static class CampaignProgress
    {
        public static string BuildMissionStatus(
            ICampaignDefinition definition,
            CampaignState state,
            Func<ICampaignMapDefinition, bool> includeMap)
        {
            if (definition == null || definition.Maps == null || definition.Maps.Count == 0 || state == null)
            {
                return string.Empty;
            }

            int total = 0;
            int completed = 0;
            for (int i = 0; i < definition.Maps.Count; i++)
            {
                ICampaignMapDefinition map = definition.Maps[i];
                if (map == null || (includeMap != null && !includeMap(map)))
                {
                    continue;
                }

                total++;
                CampaignLevelState level = state.GetLevel(map);
                bool isCompleted = level != null && level.IsCompleted;
                if (isCompleted)
                {
                    completed++;
                }
            }

            if (total == 0)
            {
                return string.Empty;
            }

            return ModText.Get(ModStrings.Screens.CampaignMissionProgress, completed, total);
        }

        public static string GetLocalizedText(string localizationKey, string fallback)
        {
            return GameText.Get(GlobalLocalizationVariables.LocalizationHandler, localizationKey, fallback);
        }
    }
}
