using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CommanderSheetPatches
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
            SocAccessMod.Instance?.ScreenDetector?.OnCommanderSheetReady(__instance);
        }

        [HarmonyPatch(typeof(CommanderSheet), "Close")]
        [HarmonyPrefix]
        private static void CommanderSheetClosePrefix(CommanderSheet __instance)
        {
            if (__instance == null || !ActiveSheets.Remove(__instance.GetInstanceID()))
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnCommanderSheetClosed(__instance);
        }
    }
}
