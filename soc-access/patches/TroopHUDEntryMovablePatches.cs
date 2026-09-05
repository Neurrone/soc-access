using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class TroopHUDEntryMovablePatches
    {
        [HarmonyPatch(typeof(TroopHUDEntryMovable), "DecideAmount")]
        [HarmonyPostfix]
        private static void TroopHUDEntryMovableDecideAmountPostfix(TroopHUDEntryMovable __instance)
        {
            if (__instance == null || !new MoveTroopPopupAdapter(__instance).IsPresent())
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnMoveTroopPopupReady(__instance);
        }

        [HarmonyPatch(typeof(TroopHUDEntryMovable), "Reset")]
        [HarmonyPostfix]
        private static void TroopHUDEntryMovableResetPostfix(TroopHUDEntryMovable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnMoveTroopPopupClosed(__instance);
        }
    }
}
