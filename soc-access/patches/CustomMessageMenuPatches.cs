using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class CustomMessageMenuPatches
    {
        [HarmonyPatch(typeof(CustomMessageMenu), "Show", new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Action),
            typeof(Action),
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void ShowPostfix(CustomMessageMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCustomMessageMenuReady(__instance);
        }

        [HarmonyPatch(typeof(CustomMessageMenu), "Hide", new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void HidePrefix(CustomMessageMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCustomMessageMenuClosed(__instance);
        }
    }
}
