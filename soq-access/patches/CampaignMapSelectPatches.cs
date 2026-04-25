using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Map;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CampaignMapSelectPatches
    {
        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Show")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewShowPostfix(
            CampaignMapSelectedInformationView __instance,
            ICampaignMapDefinition mapDefinition,
            MapFormat map,
            string path)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCampaignMapSelectAvailable(__instance);
        }

        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Dispose")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewDisposePostfix(CampaignMapSelectedInformationView __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCampaignMapSelectHidden(__instance);
        }
    }
}
