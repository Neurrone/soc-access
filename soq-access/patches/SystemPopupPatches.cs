using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class SystemPopupPatches
    {
        [HarmonyPatch(typeof(SystemPopup), "Show")]
        [HarmonyPostfix]
        private static void ShowPostfix(SystemPopup __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnSystemPopupReady(__instance);
        }

        [HarmonyPatch(typeof(SystemPopup), "Hide")]
        [HarmonyPrefix]
        private static void HidePrefix(SystemPopup __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnSystemPopupClosed(__instance);
        }
    }
}
