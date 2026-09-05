using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class AdventureLobbyPlayerSettingsPatches
    {
        [HarmonyPatch(typeof(LobbyPlayerSettingsMenu), "Show")]
        [HarmonyPostfix]
        private static void LobbyPlayerSettingsMenuShowPostfix(LobbyPlayerSettingsMenu __instance)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.StartCoroutine(NotifyReadyNextFrame(__instance));
        }

        [HarmonyPatch(typeof(LobbyPlayerSettingsMenu), "Close")]
        [HarmonyPostfix]
        private static void LobbyPlayerSettingsMenuClosePostfix(LobbyPlayerSettingsMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyPlayerSettingsClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyPlayerSettingsMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void LobbyPlayerSettingsMenuOnDestroyPostfix(LobbyPlayerSettingsMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyPlayerSettingsClosed(__instance);
        }

        private static IEnumerator NotifyReadyNextFrame(LobbyPlayerSettingsMenu menu)
        {
            yield return null;
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyPlayerSettingsReady(menu);
        }
    }
}
