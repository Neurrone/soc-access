using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class BuildMenuPatches
    {
        [HarmonyPatch(typeof(BuildMenu), "Show")]
        [HarmonyPrefix]
        private static void BuildMenuShowPrefix(BuildMenu __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        [HarmonyPatch(typeof(BuildMenu), "Show")]
        [HarmonyPostfix]
        private static void BuildMenuShowPostfix(BuildMenu __instance, bool __state)
        {
            if (__state)
            {
                return;
            }

            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        private static void BuildMenuClosePrefix(BuildMenu __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        [HarmonyPatch(typeof(BuildMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void BuildMenuClosePostfix(BuildMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnBuildMenuClosed(__instance);
            }
        }

        private static void StartWaitForReady(BuildMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu));
            }
        }

        private static IEnumerator WaitForReady(BuildMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                BuildMenuAdapter adapter = new BuildMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnBuildMenuReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
