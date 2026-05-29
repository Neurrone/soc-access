using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Menu.Online;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class OnlineGameListPatches
    {
        [HarmonyPatch(typeof(GameListMenu), "Initialize")]
        [HarmonyPostfix]
        private static void GameListMenuInitializePostfix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListReady(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "Dispose")]
        [HarmonyPrefix]
        private static void GameListMenuDisposePrefix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineHostGameClosed(__instance);
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListClosed(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "HandleGameListUpdated")]
        [HarmonyPostfix]
        private static void GameListMenuUpdatedPostfix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListChanged(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "HandleEntrySelected")]
        [HarmonyPostfix]
        private static void GameListMenuEntrySelectedPostfix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListChanged(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "HandleRegionChanged")]
        [HarmonyPostfix]
        private static void GameListMenuRegionChangedPostfix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListChanged(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "ShowHostGame")]
        [HarmonyPostfix]
        private static void GameListMenuShowHostGamePostfix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineHostGameReady(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "HandlePositiveHostGameButtonClick")]
        [HarmonyPrefix]
        private static void GameListMenuPositiveHostPrefix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineHostGameClosed(__instance);
        }

        [HarmonyPatch(typeof(GameListMenu), "HandleNegativeHostGameButtonClick")]
        [HarmonyPrefix]
        private static void GameListMenuNegativeHostPrefix(GameListMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineHostGameClosed(__instance);
        }

        [HarmonyPatch(typeof(GameListEntry), "Refresh")]
        [HarmonyPostfix]
        private static void GameListEntryRefreshPostfix()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnOnlineGameListChanged();
        }
    }
}
