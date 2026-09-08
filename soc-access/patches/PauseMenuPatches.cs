using _8_UILayer.ClientView.Menu.Paus;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class PauseMenuPatches
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
        private static void PauseMenuTryClosePrefix(PauseMenu __instance, PauseResponse response)
        {
            if (__instance == null || AsyncRef(__instance) == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPauseMenuClosed(__instance, HandsOver(response.Action));
        }

        /// <summary>Whether this response closes the pause menu only to open another menu in its
        /// place (MenuSystem acts on it a frame or more later), so the pause screen should span the
        /// frames in between rather than hand the map back for them.</summary>
        private static bool HandsOver(PauseResponseAction action)
        {
            return action == PauseResponseAction.OpenOptions
                || action == PauseResponseAction.OpenSaveGameMenu
                || action == PauseResponseAction.OpenLoadGameMenu
                || action == PauseResponseAction.OpenTutorials
                || action == PauseResponseAction.OpenGamepadControls;
        }
    }
}
