using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureLobbyPlayersPatches
    {
        [HarmonyPatch(typeof(LobbyMenu), "Show")]
        [HarmonyPostfix]
        private static void LobbyMenuShowPostfix(LobbyMenu __instance)
        {
            SocAccessPlugin.Instance?.StartCoroutine(WaitForLobbyPlayersReady(__instance));
        }

        [HarmonyPatch(typeof(LobbyMenu), "Hide")]
        [HarmonyPostfix]
        private static void LobbyMenuHidePostfix(LobbyMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void LobbyMenuOnDestroyPostfix(LobbyMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyPlayerEntry), "Refresh")]
        [HarmonyPostfix]
        private static void LobbyPlayerEntryRefreshPostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersChanged();
        }

        [HarmonyPatch(typeof(LobbyMapSettings), "RefreshMixedFactionToggle")]
        [HarmonyPostfix]
        private static void LobbyMapSettingsRefreshMixedFactionTogglePostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersChanged();
        }

        [HarmonyPatch(typeof(LobbyMultiplayerPanel), "Refresh")]
        [HarmonyPostfix]
        private static void LobbyMultiplayerPanelRefreshPostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersChanged();
        }

        [HarmonyPatch(typeof(LobbyMultiplayerPanel), "HandleInviteFriendButtonClicked")]
        [HarmonyPostfix]
        private static void LobbyMultiplayerPanelInviteFriendPostfix(LobbyMultiplayerPanel __instance)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForInviteProvidersReady(__instance));
            }
        }

        [HarmonyPatch(typeof(LobbyMultiplayerPanel), "HandleCancelInvitePopup")]
        [HarmonyPostfix]
        private static void LobbyMultiplayerPanelCancelInvitePostfix(LobbyMultiplayerPanel __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyInviteProvidersClosed(__instance);
        }

        private static IEnumerator WaitForLobbyPlayersReady(LobbyMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                AdventureLobbyPlayersAdapter adapter = new AdventureLobbyPlayersAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyPlayersReady(menu);
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForInviteProvidersReady(LobbyMultiplayerPanel panel)
        {
            yield return null;

            AdventureLobbyInviteProvidersAdapter adapter = new AdventureLobbyInviteProvidersAdapter(panel);
            if (adapter.IsPresent())
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyInviteProvidersReady(panel);
            }
        }
    }
}
