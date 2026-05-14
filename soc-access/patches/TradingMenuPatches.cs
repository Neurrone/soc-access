using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class TradingMenuPatches
    {
        [HarmonyPatch(typeof(TradingMenu), "Open", new[] { typeof(TradeInitiatedPayload) })]
        [HarmonyPostfix]
        private static void TradingMenuOpenPostfix(TradingMenu __instance)
        {
            if (__instance == null || !new TradingMenuAdapter(__instance).IsPresent())
            {
                return;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnTradingMenuReady(__instance);
        }

        [HarmonyPatch(typeof(TradingMenu), "Close", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void TradingMenuClosePostfix(TradingMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnTradingMenuClosed(__instance);
        }
    }
}
