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

        private static IEnumerator NotifyReadyNextFrame(OptionsMenu menu)
        {
            yield return null;
            SocAccessMod.Instance?.ScreenDetector?.OnOptionsMenuReady(menu);
        }
    }
}
