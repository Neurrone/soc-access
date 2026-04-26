using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CommanderSheetPatches
    {
        [HarmonyPatch(typeof(CommanderSheet), "Open")]
        [HarmonyPostfix]
        private static void CommanderSheetOpenPostfix(CommanderSheet __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetOpened(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheet), "Close")]
        [HarmonyPostfix]
        private static void CommanderSheetClosePostfix(CommanderSheet __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetClosed(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheet), "HandleTutorialClicked")]
        [HarmonyPostfix]
        private static void CommanderSheetHandleTutorialClickedPostfix(CommanderSheet __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetModifierTabNavigation), "SetActiveTab")]
        [HarmonyPostfix]
        private static void CommanderSheetModifierTabNavigationSetActiveTabPostfix(CommanderSheetModifierTabNavigation __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetSummaryUpdateSummaryPostfix(CommanderSheetSummary __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetTroopSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetTroopSummaryUpdateSummaryPostfix(CommanderSheetTroopSummary __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetTempEffectSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetTempEffectSummaryUpdateSummaryPostfix(CommanderSheetTempEffectSummary __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }
    }
}
