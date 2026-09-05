using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class TeleportMenuPatches
    {
        [HarmonyPatch(typeof(TeleportMenu), "Show", new[]
        {
            typeof(Vector2Int[]),
            typeof(int)
        })]
        [HarmonyPostfix]
        private static void TeleportMenuShowPostfix(TeleportMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnTeleportMenuReady(__instance);
        }

        [HarmonyPatch(typeof(TeleportMenu), "Close")]
        [HarmonyPrefix]
        private static void TeleportMenuClosePrefix(TeleportMenu __instance, out bool __state)
        {
            __state = __instance != null
                && new Adapters.TeleportMenuAdapter(__instance).IsPresent();
        }

        [HarmonyPatch(typeof(TeleportMenu), "Close")]
        [HarmonyPostfix]
        private static void TeleportMenuClosePostfix(TeleportMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnTeleportMenuClosed(__instance);
            }
        }
    }
}
