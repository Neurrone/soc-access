using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureLobbyRandomLayoutPatches
    {
        [HarmonyPatch(typeof(LobbyRandomMapSelectionMenu), "Show")]
        [HarmonyPostfix]
        private static void LobbyRandomMapSelectionMenuShowPostfix(LobbyRandomMapSelectionMenu __instance)
        {
            SocAccessMod.Instance?.StartCoroutine(WaitForRandomLayoutMenuReady(__instance));
        }

        [HarmonyPatch(typeof(LobbyRandomMapSelectionMenu), "Hide")]
        [HarmonyPostfix]
        private static void LobbyRandomMapSelectionMenuHidePostfix(LobbyRandomMapSelectionMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapSelectionMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void LobbyRandomMapSelectionMenuOnDestroyPostfix(LobbyRandomMapSelectionMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapSelectionMenu), "SetSelectedEntry")]
        [HarmonyPostfix]
        private static void LobbyRandomMapSelectionMenuSetSelectedEntryPostfix(LobbyRandomMapSelectionMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutSelectionChanged(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapPreviewEntry), "HandleDropdown")]
        [HarmonyPostfix]
        private static void LobbyRandomMapPreviewEntryHandleDropdownPostfix(LobbyRandomMapPreviewEntry __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutEntryChanged(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapPreviewEntry), "HandleKingToggleChanged")]
        [HarmonyPostfix]
        private static void LobbyRandomMapPreviewEntryHandleKingToggleChangedPostfix(LobbyRandomMapPreviewEntry __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutEntryChanged(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapPreviewEntry), "HandleBeaconToggleChanged")]
        [HarmonyPostfix]
        private static void LobbyRandomMapPreviewEntryHandleBeaconToggleChangedPostfix(LobbyRandomMapPreviewEntry __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutEntryChanged(__instance);
        }

        [HarmonyPatch(typeof(LobbyRandomMapPreviewEntry), "HandleArtifactToggleChanged")]
        [HarmonyPostfix]
        private static void LobbyRandomMapPreviewEntryHandleArtifactToggleChangedPostfix(LobbyRandomMapPreviewEntry __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutEntryChanged(__instance);
        }

        private static IEnumerator WaitForRandomLayoutMenuReady(LobbyRandomMapSelectionMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                AdventureLobbyRandomLayoutAdapter adapter = new AdventureLobbyRandomLayoutAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyRandomLayoutReady(menu);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
