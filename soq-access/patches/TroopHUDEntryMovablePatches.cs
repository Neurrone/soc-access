using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class TroopHUDEntryMovablePatches
    {
        private static readonly HashSet<int> ActiveMovePopups = new HashSet<int>();

        [HarmonyPatch(typeof(TroopHUDEntryMovable), "DecideAmount")]
        [HarmonyPostfix]
        private static void TroopHUDEntryMovableDecideAmountPostfix(TroopHUDEntryMovable __instance)
        {
            if (__instance == null || !new MoveTroopPopupAdapter(__instance).IsPresent())
            {
                return;
            }

            ActiveMovePopups.Add(((Object)__instance).GetInstanceID());
            SoqAccessPlugin.Instance?.ScreenDetector?.OnMoveTroopPopupReady(__instance);
        }

        [HarmonyPatch(typeof(TroopHUDEntryMovable), "Reset")]
        [HarmonyPostfix]
        private static void TroopHUDEntryMovableResetPostfix(TroopHUDEntryMovable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            int instanceId = ((Object)__instance).GetInstanceID();
            if (!ActiveMovePopups.Remove(instanceId))
            {
                return;
            }

            SoqAccessPlugin.Instance?.ScreenDetector?.OnMoveTroopPopupClosed(__instance);
        }
    }
}
