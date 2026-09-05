using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CampaignMenuPatches
    {
        [HarmonyPatch(typeof(CampaignMenu), "Start")]
        [HarmonyPostfix]
        private static void CampaignMenuStartPostfix(CampaignMenu __instance, ref IEnumerator __result)
        {
            __result = WaitForCampaignMenuStart(__instance, __result);
        }

        private static IEnumerator WaitForCampaignMenuStart(CampaignMenu campaignMenu, IEnumerator original)
        {
            while (original != null && original.MoveNext())
            {
                yield return original.Current;
            }

            CampaignMenuLifetimeNotifier.Attach(campaignMenu);
            SocAccessMod.Instance?.ScreenDetector?.OnCampaignMenuReady(campaignMenu);
        }
    }
}
