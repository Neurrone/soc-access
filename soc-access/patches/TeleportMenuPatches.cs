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

        /// <summary>
        /// Which menu the player cancelled. <c>Cancel</c> closes the menu from inside itself, so the
        /// close hook cannot tell a cancel from a confirm on its own: this records the menu on the way
        /// into <c>Cancel</c>, and the close that follows reads it. <c>Confirm</c> never sets it, so a
        /// confirmed teleport closes with nothing recorded.
        /// </summary>
        private static TeleportMenu _cancelling;

        [HarmonyPatch(typeof(TeleportMenu), "Cancel")]
        [HarmonyPrefix]
        private static void TeleportMenuCancelPrefix(TeleportMenu __instance)
        {
            _cancelling = __instance;
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
            bool cancelled = ReferenceEquals(_cancelling, __instance);
            _cancelling = null;
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnTeleportMenuClosed(__instance, cancelled);
            }
        }
    }
}
