using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class MarketplaceMenuPatches
    {
        [HarmonyPatch(typeof(MarketplaceMenu), "Show")]
        [HarmonyPostfix]
        private static void MarketplaceMenuShowPostfix(MarketplaceMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnMarketplaceReady(__instance);
        }

        [HarmonyPatch(typeof(MarketplaceMenu), "Hide")]
        [HarmonyPrefix]
        private static void MarketplaceMenuHidePrefix(MarketplaceMenu __instance, ref bool __state)
        {
            __state = __instance != null && ((Component)__instance).gameObject.activeSelf;
        }

        [HarmonyPatch(typeof(MarketplaceMenu), "Hide")]
        [HarmonyPostfix]
        private static void MarketplaceMenuHidePostfix(MarketplaceMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnMarketplaceClosed(__instance);
            }
        }

    }
}
