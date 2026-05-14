using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Penalties;
using SongsOfConquest.Common.Rewards;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class WorldChoiceMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldChoiceMenu), "_async");

        [HarmonyPatch(typeof(WorldChoiceMenu), "ShowMenuAtPoint", new[]
        {
            typeof(int),
            typeof(string),
            typeof(string),
            typeof(List<RuntimeRewardDataContainer>),
            typeof(List<RuntimePenaltyDataContainer>)
        })]
        [HarmonyPostfix]
        private static void WorldChoiceMenuShowMenuAtPointPostfix(WorldChoiceMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnWorldChoiceMenuReady(__instance);
        }

        [HarmonyPatch(typeof(WorldChoiceMenu), "ShowMenu", new[]
        {
            typeof(ICommanderState),
            typeof(string),
            typeof(string),
            typeof(List<string>)
        })]
        [HarmonyPostfix]
        private static void WorldChoiceMenuShowMenuPostfix(WorldChoiceMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnWorldChoiceMenuReady(__instance);
        }

        [HarmonyPatch(typeof(WorldChoiceMenu), "HideMenu", new[] { typeof(int), typeof(int) })]
        [HarmonyPrefix]
        private static void WorldChoiceMenuHideMenuPrefix(WorldChoiceMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) != null;
        }

        [HarmonyPatch(typeof(WorldChoiceMenu), "HideMenu", new[] { typeof(int), typeof(int) })]
        [HarmonyPostfix]
        private static void WorldChoiceMenuHideMenuPostfix(WorldChoiceMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnWorldChoiceMenuClosed(__instance);
            }
        }
    }
}
