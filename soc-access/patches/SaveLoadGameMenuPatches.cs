using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class SaveLoadGameMenuPatches
    {
        [HarmonyPatch(typeof(SaveLoadGameMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuOnOpenedPostfix(SaveLoadGameMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnSaveLoadGameMenuReady(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuOnClosedPostfix(SaveLoadGameMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnSaveLoadGameMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "SpawnEntriesGameDefinitionsLoaded")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuEntriesLoadedPostfix(SaveLoadGameMenu __instance)
        {
            NotifyChanged(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "HandleSwitchedTab")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuHandleSwitchedTabPostfix(SaveLoadGameMenu __instance)
        {
            NotifyChanged(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "ClearSelection")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuClearSelectionPostfix(SaveLoadGameMenu __instance)
        {
            NotifyChanged(__instance);
        }

        [HarmonyPatch(typeof(SaveLoadGameMenu), "OnControlsChanged")]
        [HarmonyPostfix]
        private static void SaveLoadGameMenuControlsChangedPostfix(SaveLoadGameMenu __instance)
        {
            NotifyChanged(__instance);
        }

        private static void NotifyChanged(SaveLoadGameMenu menu)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnSaveLoadGameMenuChanged(menu);
        }

        private static void NotifyChangedAfterFrames(SaveLoadGameMenu menu, int frames)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.StartCoroutine(NotifyChangedCoroutine(menu, frames));
        }

        private static IEnumerator NotifyChangedCoroutine(SaveLoadGameMenu menu, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            NotifyChanged(menu);
        }
    }
}
