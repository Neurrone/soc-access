using System.Collections;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class OptionsMenuPatches
    {
        private static readonly AccessTools.FieldRef<OptionsMenu, Async<OptionsResponse>> AsyncRef =
            AccessTools.FieldRefAccess<OptionsMenu, Async<OptionsResponse>>("_async");
        private static int _languageRedrawDepth;

        [HarmonyPatch(typeof(OptionsMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void OptionsMenuOnOpenedPostfix(OptionsMenu __instance)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.StartCoroutine(NotifyReadyNextFrame(__instance));
        }

        [HarmonyPatch(typeof(OptionsMenu), "TryClose")]
        [HarmonyPrefix]
        private static void OptionsMenuTryClosePrefix(OptionsMenu __instance)
        {
            if (__instance == null || AsyncRef(__instance) == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnOptionsMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(OptionsMenu), "DrawContent")]
        [HarmonyPostfix]
        private static void OptionsMenuDrawContentPostfix(OptionsMenu __instance)
        {
            // ReDrawAfterLanguageChange rebuilds the tab list incrementally and
            // calls DrawContent while only the active tab has been re-added. If
            // we refresh accessibility there, the category menu can be rebuilt
            // from a partial native _tabs list. Treat language redraw as atomic
            // and refresh from the redraw finalizer instead.
            if (_languageRedrawDepth > 0)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnOptionsMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(OptionsMenu), "ReDrawAfterLanguageChange")]
        [HarmonyPrefix]
        private static void OptionsMenuReDrawAfterLanguageChangePrefix(OptionsMenu __instance, int activeTabIndex)
        {
            _languageRedrawDepth++;
        }

        [HarmonyPatch(typeof(OptionsMenu), "ReDrawAfterLanguageChange")]
        [HarmonyFinalizer]
        private static void OptionsMenuReDrawAfterLanguageChangeFinalizer(OptionsMenu __instance, System.Exception __exception)
        {
            if (_languageRedrawDepth > 0)
            {
                _languageRedrawDepth--;
            }

            if (__exception == null)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnOptionsMenuChanged(__instance);
            }
        }

        private static IEnumerator NotifyReadyNextFrame(OptionsMenu menu)
        {
            yield return null;
            SocAccessMod.Instance?.ScreenDetector?.OnOptionsMenuReady(menu);
        }
    }
}
