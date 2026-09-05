using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureLobbyMapSelectPatches
    {
        [HarmonyPatch(typeof(MapSelectMenu), "Show")]
        [HarmonyPostfix]
        private static void MapSelectMenuShowPostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.StartCoroutine(WaitForMapSelectMenuReady(__instance));
        }

        [HarmonyPatch(typeof(MapSelectMenu), "Hide")]
        [HarmonyPostfix]
        private static void MapSelectMenuHidePostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectClosed(__instance);
        }

        [HarmonyPatch(typeof(MapSelectMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void MapSelectMenuOnDestroyPostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectClosed(__instance);
        }

        [HarmonyPatch(typeof(MapSelectMenu), "SetSelectedEntry")]
        [HarmonyPostfix]
        private static void MapSelectMenuSetSelectedEntryPostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectSelectionChanged(__instance);
        }

        [HarmonyPatch(typeof(MapSelectMenu), "FilterEntries")]
        [HarmonyPostfix]
        private static void MapSelectMenuFilterEntriesPostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectChanged(__instance);
        }

        [HarmonyPatch(typeof(MapSelectMenu), "SortSiblings")]
        [HarmonyPostfix]
        private static void MapSelectMenuSortSiblingsPostfix(MapSelectMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectChanged(__instance);
        }

        private static IEnumerator WaitForMapSelectMenuReady(MapSelectMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapSelectReady(menu);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
