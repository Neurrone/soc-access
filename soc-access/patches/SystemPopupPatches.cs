using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class SystemPopupPatches
    {
        [HarmonyPatch(typeof(SystemPopup), "Show")]
        [HarmonyPostfix]
        private static void ShowPostfix(SystemPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSystemPopupReady(__instance);
        }

        [HarmonyPatch(typeof(SystemPopup), "Hide")]
        [HarmonyPrefix]
        private static void HidePrefix(SystemPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSystemPopupClosed(__instance);
        }
    }
}
