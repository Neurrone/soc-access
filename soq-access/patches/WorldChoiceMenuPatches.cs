using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Penalties;
using SongsOfConquest.Common.Rewards;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class WorldChoiceMenuPatches
    {
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
            SoqAccessPlugin.Instance?.ScreenDetector?.OnWorldChoiceMenuOpened(__instance);
        }

        [HarmonyPatch(typeof(WorldChoiceMenu), "HideMenu")]
        [HarmonyPostfix]
        private static void WorldChoiceMenuHideMenuPostfix(WorldChoiceMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnWorldChoiceMenuClosed(__instance);
        }
    }
}
