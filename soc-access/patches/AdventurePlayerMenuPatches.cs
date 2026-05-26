using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventurePlayerMenuPatches
    {
        [HarmonyPatch(typeof(AdventurePlayerMenu), "Show")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuShowPostfix(AdventurePlayerMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventurePlayerMenuReady(__instance);
        }

        [HarmonyPatch(typeof(AdventurePlayerMenu), "Hide")]
        [HarmonyPrefix]
        private static void AdventurePlayerMenuHidePrefix(AdventurePlayerMenu __instance, ref bool __state)
        {
            __state = __instance != null && ((Component)__instance).gameObject.activeSelf;
        }

        [HarmonyPatch(typeof(AdventurePlayerMenu), "Hide")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuHidePostfix(AdventurePlayerMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnAdventurePlayerMenuClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(AdventurePlayerMenuEntry), "RefreshInteractable")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuEntryRefreshInteractablePostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventurePlayerMenuChanged();
        }

        [HarmonyPatch(typeof(AdventurePlayerMenuEntry), "RefreshResources")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuEntryRefreshResourcesPostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventurePlayerMenuChanged();
        }
    }
}
