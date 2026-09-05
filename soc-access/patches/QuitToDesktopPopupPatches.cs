using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class QuitToDesktopPopupPatches
    {
        [HarmonyPatch(typeof(QuitToDesktopPopup), "OnOpened")]
        [HarmonyPostfix]
        private static void OnOpenedPostfix(QuitToDesktopPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnQuitToDesktopPopupReady(__instance);
        }

        [HarmonyPatch(typeof(QuitToDesktopPopup), "OnClosed")]
        [HarmonyPostfix]
        private static void OnClosedPostfix(QuitToDesktopPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnQuitToDesktopPopupClosed(__instance);
        }
    }
}
