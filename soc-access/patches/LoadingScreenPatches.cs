using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class LoadingScreenPatches
    {
        [HarmonyPatch(typeof(LoadingScreenMenu), "Initialize")]
        [HarmonyPrefix]
        private static void InitializePrefix(LoadingScreenMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnLoadingScreenOpening(__instance);
        }

        [HarmonyPatch(typeof(LoadingScreenMenu), "HandleWaitForFinalizationEntered")]
        [HarmonyPostfix]
        private static void HandleWaitForFinalizationEnteredPostfix(LoadingScreenMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnLoadingScreenReady(__instance);
        }

        [HarmonyPatch(typeof(LoadingScreenMenu), "FinalizeLoadingScreen")]
        [HarmonyPrefix]
        private static void FinalizeLoadingScreenPrefix(LoadingScreenMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnLoadingScreenClosed(__instance);
        }

        [HarmonyPatch(typeof(LoadingScreenMenu), "Dispose")]
        [HarmonyPrefix]
        private static void DisposePrefix(LoadingScreenMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnLoadingScreenClosed(__instance);
        }
    }
}
