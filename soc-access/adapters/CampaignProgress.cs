using System;
using System.Collections.Generic;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Adapters
{
    internal static class CampaignProgress
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
            int available = 0;
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
                bool unlocked = i == 0 || (level != null && !level.IsLocked);
                bool isCompleted = level != null && level.IsCompleted;
                if (unlocked || isCompleted)
                {
                    available++;
                }

                if (isCompleted)
                {
                    completed++;
                }
            }

            if (total == 0)
            {
                return string.Empty;
            }

            if (completed >= total)
            {
                return GetLocalizedText("Common/CampaignSelectMenu/CampaignCompleted", "campaign completed");
            }

            List<string> parts = new List<string>();
            parts.Add(completed + " of " + total + " missions completed");
            if (available > 0)
            {
                parts.Add(available + " available");
            }

            return string.Join(". ", parts.ToArray());
        }

        public static string GetLocalizedText(string localizationKey, string fallback)
        {
            return GameText.Get(GlobalLocalizationVariables.LocalizationHandler, localizationKey, fallback);
        }
    }
}
