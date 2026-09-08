using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class ResearchMenuPatches
    {
        [HarmonyPatch(typeof(ResearchMenu), "Show")]
        [HarmonyPostfix]
        private static void ResearchMenuShowPostfix(ResearchMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnResearchMenuReady(__instance);
        }

        [HarmonyPatch(typeof(ResearchMenu), "Hide")]
        [HarmonyPostfix]
        private static void ResearchMenuHidePostfix(ResearchMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnResearchMenuClosed(__instance);
        }
    }
}
