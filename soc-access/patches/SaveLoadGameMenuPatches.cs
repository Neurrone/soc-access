using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class SaveLoadGameMenuPatches
    {
        [HarmonyPatch(typeof(SaveLoadGameMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuOnOpenedPostfix(SaveLoadGameMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSaveLoadGameMenuReady(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuOnClosedPostfix(SaveLoadGameMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSaveLoadGameMenuClosed(__instance);
        }
    }
}
