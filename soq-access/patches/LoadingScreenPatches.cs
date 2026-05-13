using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class LoadingScreenPatches
    {
        [HarmonyPatch(typeof(LoadingScreenMenu), "HandleWaitForFinalizationEntered")]
        [HarmonyPostfix]
        private static void HandleWaitForFinalizationEnteredPostfix(LoadingScreenMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLoadingScreenReady(__instance);
        }

        [HarmonyPatch(typeof(LoadingScreenMenu), "FinalizeLoadingScreen")]
        [HarmonyPrefix]
        private static void FinalizeLoadingScreenPrefix(LoadingScreenMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLoadingScreenClosed(__instance);
        }

        [HarmonyPatch(typeof(LoadingScreenMenu), "Dispose")]
        [HarmonyPrefix]
        private static void DisposePrefix(LoadingScreenMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLoadingScreenClosed(__instance);
        }
    }
}
