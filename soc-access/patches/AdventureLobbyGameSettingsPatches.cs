using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class AdventureLobbyGameSettingsPatches
    {
        [HarmonyPatch(typeof(LobbyMapSettingsMenu), "Show")]
        [HarmonyPostfix]
        private static void LobbyMapSettingsMenuShowPostfix(LobbyMapSettingsMenu __instance)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.StartCoroutine(NotifyReadyNextFrame(__instance));
        }

        [HarmonyPatch(typeof(LobbyMapSettingsMenu), "Refresh")]
        [HarmonyPostfix]
        private static void LobbyMapSettingsMenuRefreshPostfix(LobbyMapSettingsMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyGameSettingsChanged(__instance);
        }

        [HarmonyPatch(typeof(LobbyMapSettingsMenu), "CloseAndStoreSettings")]
        [HarmonyPostfix]
        private static void LobbyMapSettingsMenuCloseAndStoreSettingsPostfix(LobbyMapSettingsMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyGameSettingsClosed(__instance);
        }

        [HarmonyPatch(typeof(LobbyMapSettingsMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void LobbyMapSettingsMenuOnDestroyPostfix(LobbyMapSettingsMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyGameSettingsClosed(__instance);
        }

        private static IEnumerator NotifyReadyNextFrame(LobbyMapSettingsMenu menu)
        {
            yield return null;
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureLobbyGameSettingsReady(menu);
        }
    }
}
