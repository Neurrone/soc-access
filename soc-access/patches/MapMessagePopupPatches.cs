using HarmonyLib;
using SongsOfConquest.Client.Adventure;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class MapMessagePopupPatches
    {
        [HarmonyPatch(typeof(MapMessagePopup), "Show")]
        [HarmonyPostfix]
        private static void ShowPostfix(MapMessagePopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnMapMessagePopupReady(__instance);
        }

        [HarmonyPatch(typeof(MapMessagePopup), "Hide", new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void HidePrefix(MapMessagePopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnMapMessagePopupClosed(__instance);
        }
    }
}
