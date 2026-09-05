using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class TutorialMenuPatches
    {
        [HarmonyPatch(typeof(TutorialMenu), "Open")]
        [HarmonyPostfix]
        private static void TutorialMenuOpenPostfix(TutorialMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnTutorialReady(__instance);
        }

        [HarmonyPatch(typeof(TutorialMenu), "UpdatePage")]
        [HarmonyPostfix]
        private static void TutorialMenuUpdatePagePostfix(TutorialMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnTutorialChanged(__instance);
        }

        [HarmonyPatch(typeof(TutorialMenu), "Close")]
        [HarmonyPrefix]
        private static void TutorialMenuClosePrefix(TutorialMenu __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        [HarmonyPatch(typeof(TutorialMenu), "Close")]
        [HarmonyPostfix]
        private static void TutorialMenuClosePostfix(TutorialMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnTutorialClosed(__instance);
            }
        }
    }
}
