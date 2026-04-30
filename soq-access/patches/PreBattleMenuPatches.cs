using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PreBattleMenuPatches
    {
        [HarmonyPatch(typeof(PreBattleMenu), "Show", new[]
        {
            typeof(ICommanderState),
            typeof(int[]),
            typeof(ICommanderState),
            typeof(int[]),
            typeof(MapFormat)
        })]
        [HarmonyPostfix]
        private static void PreBattleMenuShowPostfix(PreBattleMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPreBattleMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(PreBattleMenu), "Hide")]
        [HarmonyPostfix]
        private static void PreBattleMenuHidePostfix(PreBattleMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPreBattleMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(PreBattleMenu), "HandleReadyButton")]
        [HarmonyPostfix]
        private static void PreBattleMenuReadyPostfix(PreBattleMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPreBattleMenuChanged(__instance);
        }
    }
}
