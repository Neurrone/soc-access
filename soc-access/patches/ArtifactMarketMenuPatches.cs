using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class ArtifactMarketMenuPatches
    {
        [HarmonyPatch(typeof(ArtifactMarketMenu), "Show", new[] { typeof(int), typeof(int) })]
        [HarmonyPostfix]
        private static void ArtifactMarketMenuShowPostfix(ArtifactMarketMenu __instance)
        {
            if (__instance == null || !new ArtifactMarketMenuAdapter(__instance).IsPresent())
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnArtifactMarketReady(__instance);
        }

        [HarmonyPatch(typeof(ArtifactMarketMenu), "Close")]
        [HarmonyPostfix]
        private static void ArtifactMarketMenuClosePostfix(ArtifactMarketMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnArtifactMarketClosed(__instance);
        }

        [HarmonyPatch(typeof(ArtifactMarketMenu), "HandleSwitchedCategory")]
        [HarmonyPostfix]
        private static void ArtifactMarketMenuHandleSwitchedCategoryPostfix(ArtifactMarketMenu __instance)
        {
            if (__instance == null || !new ArtifactMarketMenuAdapter(__instance).IsPresent())
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnArtifactMarketChanged();
        }
    }
}
