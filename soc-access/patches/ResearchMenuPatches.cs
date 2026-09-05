using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class ResearchMenuPatches
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

        [HarmonyPatch(typeof(ResearchMenu), "HandleBuildingTabSwitched", new[] { typeof(int) })]
        [HarmonyPostfix]
        private static void ResearchMenuHandleBuildingTabSwitchedPostfix(ResearchMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnResearchMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(ResearchMenu), "HandleFactionButtonClicked", new[] { typeof(int), typeof(bool) })]
        [HarmonyPostfix]
        private static void ResearchMenuHandleFactionButtonClickedPostfix(ResearchMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnResearchMenuChanged(__instance);
        }
    }
}
