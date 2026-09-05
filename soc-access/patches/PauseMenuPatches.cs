using _8_UILayer.ClientView.Menu.Paus;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class PauseMenuPatches
    {
        private static readonly AccessTools.FieldRef<PauseMenu, Async<PauseResponse>> AsyncRef =
            AccessTools.FieldRefAccess<PauseMenu, Async<PauseResponse>>("_async");

        [HarmonyPatch(typeof(PauseMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void PauseMenuOnOpenedPostfix(PauseMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnPauseMenuReady(__instance);
        }

        [HarmonyPatch(typeof(PauseMenu), "TryClose")]
        [HarmonyPrefix]
        private static void PauseMenuTryClosePrefix(PauseMenu __instance)
        {
            if (__instance == null || AsyncRef(__instance) == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPauseMenuClosed(__instance);
        }
    }
}
