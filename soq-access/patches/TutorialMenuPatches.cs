using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class TutorialMenuPatches
    {
        [HarmonyPatch(typeof(TutorialMenu), "Open")]
        [HarmonyPostfix]
        private static void TutorialMenuOpenPostfix(TutorialMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTutorialOpened(__instance);
        }

        [HarmonyPatch(typeof(TutorialMenu), "UpdatePage")]
        [HarmonyPostfix]
        private static void TutorialMenuUpdatePagePostfix(TutorialMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTutorialPageChanged(__instance);
        }

        [HarmonyPatch(typeof(TutorialMenu), "Close")]
        [HarmonyPostfix]
        private static void TutorialMenuClosePostfix(TutorialMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTutorialClosed(__instance);
        }
    }
}
