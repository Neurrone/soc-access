using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class ClaimMenuPatches
    {
        [HarmonyPatch(typeof(ClaimMenu), "Open", new[]
        {
            typeof(int),
            typeof(int),
            typeof(ClaimMapEntityAction[])
        })]
        [HarmonyPostfix]
        private static void ClaimMenuOpenPostfix(ClaimMenu __instance, ClaimMapEntityAction[] claimActions)
        {
            if (__instance == null || claimActions == null || claimActions.Length <= 1)
            {
                return;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnClaimMenuReady(__instance);
        }

        [HarmonyPatch(typeof(ClaimMenu), "Hide")]
        [HarmonyPrefix]
        private static void ClaimMenuHidePrefix(ClaimMenu __instance, out bool __state)
        {
            __state = __instance != null && new ClaimMenuAdapter(__instance).IsPresent();
        }

        [HarmonyPatch(typeof(ClaimMenu), "Hide")]
        [HarmonyPostfix]
        private static void ClaimMenuHidePostfix(ClaimMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnClaimMenuClosed(__instance);
            }
        }
    }
}
