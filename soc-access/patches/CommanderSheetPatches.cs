using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CommanderSheetPatches
    {
        private static readonly HashSet<int> ActiveSheets = new HashSet<int>();

        [HarmonyPatch(typeof(CommanderSheet), "Open")]
        [HarmonyPostfix]
        private static void CommanderSheetOpenPostfix(CommanderSheet __instance)
        {
            if (__instance == null || !new CommanderSheetAdapter(__instance).IsPresent())
            {
                return;
            }

            ActiveSheets.Add(__instance.GetInstanceID());
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetReady(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheet), "Close")]
        [HarmonyPrefix]
        private static void CommanderSheetClosePrefix(CommanderSheet __instance)
        {
            if (__instance == null || !ActiveSheets.Remove(__instance.GetInstanceID()))
            {
                return;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetClosed(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheet), "HandleTutorialClicked")]
        [HarmonyPostfix]
        private static void CommanderSheetHandleTutorialClickedPostfix(CommanderSheet __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetChanged();
        }

        [HarmonyPatch(typeof(CommanderSheetModifierTabNavigation), "SetActiveTab")]
        [HarmonyPostfix]
        private static void CommanderSheetModifierTabNavigationSetActiveTabPostfix(CommanderSheetModifierTabNavigation __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetSummaryUpdateSummaryPostfix(CommanderSheetSummary __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetTroopSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetTroopSummaryUpdateSummaryPostfix(CommanderSheetTroopSummary __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheetTempEffectSummary), "UpdateSummary")]
        [HarmonyPostfix]
        private static void CommanderSheetTempEffectSummaryUpdateSummaryPostfix(CommanderSheetTempEffectSummary __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetComponentChanged(__instance);
        }
    }
}
