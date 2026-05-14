using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Economy;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class WorldConfirmMenuPatches
    {
        private static readonly Dictionary<WorldConfirmMenu, Cost> ActiveCosts = new Dictionary<WorldConfirmMenu, Cost>();

        public static Cost GetCost(WorldConfirmMenu menu)
        {
            if (menu == null)
            {
                return null;
            }

            Cost cost;
            return ActiveCosts.TryGetValue(menu, out cost) ? cost : null;
        }

        [HarmonyPatch(typeof(WorldConfirmMenu), "ShowMenuAtPoint", new[]
        {
            typeof(string),
            typeof(string),
            typeof(Cost)
        })]
        [HarmonyPostfix]
        private static void WorldConfirmMenuShowMenuAtPointPostfix(WorldConfirmMenu __instance, Cost cost)
        {
            if (__instance != null)
            {
                ActiveCosts[__instance] = cost;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnWorldConfirmMenuReady(__instance);
        }

        [HarmonyPatch(typeof(WorldConfirmMenu), "HideMenu", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void WorldConfirmMenuHideMenuPostfix(WorldConfirmMenu __instance)
        {
            if (__instance != null)
            {
                ActiveCosts.Remove(__instance);
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnWorldConfirmMenuClosed(__instance);
        }
    }
}
