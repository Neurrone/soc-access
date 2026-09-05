using HarmonyLib;
using SongsOfConquestAccess.Adapters;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;
using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PreBattleMenuPatches
    {
        private static readonly HashSet<int> ActiveMenus = new HashSet<int>();

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
            if (__instance != null && new PreBattleMenuAdapter(__instance).IsPresent())
            {
                ActiveMenus.Add(__instance.GetInstanceID());
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPreBattleMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(PreBattleMenu), "Hide")]
        [HarmonyPrefix]
        private static void PreBattleMenuHidePrefix(PreBattleMenu __instance)
        {
            if (__instance == null || !ActiveMenus.Remove(__instance.GetInstanceID()))
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPreBattleMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(PreBattleMenu), "HandleReadyButton")]
        [HarmonyPostfix]
        private static void PreBattleMenuReadyPostfix(PreBattleMenu __instance)
        {
            if (__instance == null || !ActiveMenus.Contains(__instance.GetInstanceID()))
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPreBattleMenuChanged(__instance);
        }
    }
}
