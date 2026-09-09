using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Economy;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class WorldConfirmMenuPatches
    {
        [HarmonyPatch(typeof(WorldConfirmMenu), "ShowMenuAtPoint", new[]
        {
            typeof(string),
            typeof(string),
            typeof(Cost)
        })]
        [HarmonyPostfix]
        private static void WorldConfirmMenuShowMenuAtPointPostfix(WorldConfirmMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnWorldConfirmMenuReady(__instance);
        }

        [HarmonyPatch(typeof(WorldConfirmMenu), "HideMenu", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void WorldConfirmMenuHideMenuPostfix(WorldConfirmMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnWorldConfirmMenuClosed(__instance);
        }
    }
}
