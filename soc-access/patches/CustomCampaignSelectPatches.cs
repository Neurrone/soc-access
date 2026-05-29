using Cysharp.Threading.Tasks;
using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CustomCampaignSelectPatches
    {
        [HarmonyPatch(typeof(CustomCampaignSelectMenuBehavior), "Repopulate")]
        [HarmonyPostfix]
        private static void CustomCampaignSelectMenuBehaviorRepopulatePostfix(
            CustomCampaignSelectMenuBehavior __instance,
            ref UniTask __result)
        {
            __result = WaitForCustomCampaignSelectRepopulate(__instance, __result);
        }

        [HarmonyPatch(typeof(CustomCampaignSelectMenuBehavior), "Dispose")]
        [HarmonyPostfix]
        private static void CustomCampaignSelectMenuBehaviorDisposePostfix(CustomCampaignSelectMenuBehavior __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCustomCampaignSelectClosed(__instance);
        }

        [HarmonyPatch(typeof(CustomCampaignEntry), "HandleStatusChanged")]
        [HarmonyPostfix]
        private static void CustomCampaignEntryHandleStatusChangedPostfix(CustomCampaignEntry __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCustomCampaignEntryStatusChanged(__instance);
        }

        private static async UniTask WaitForCustomCampaignSelectRepopulate(
            CustomCampaignSelectMenuBehavior behavior,
            UniTask original)
        {
            await original;
            SocAccessPlugin.Instance?.ScreenDetector?.OnCustomCampaignSelectRepopulated(behavior);
        }
    }
}
