using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Adventure;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class RandomEventMenuPatches
    {
        [HarmonyPatch(typeof(RandomEventMenu), "Open", new[] { typeof(RandomEventResult) })]
        [HarmonyPostfix]
        private static void OpenPostfix(RandomEventMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnRandomEventMenuReady(__instance);
        }

        [HarmonyPatch(typeof(RandomEventMenu), "Hide")]
        [HarmonyPostfix]
        private static void HidePostfix(RandomEventMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnRandomEventMenuClosed(__instance);
        }
    }
}
