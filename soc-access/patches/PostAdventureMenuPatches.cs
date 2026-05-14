using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PostAdventureMenuPatches
    {
        [HarmonyPatch(typeof(PostAdventureMenu), "ShowVictory", new[] { typeof(AdventureGameResult) })]
        [HarmonyPostfix]
        private static void PostAdventureMenuShowVictoryPostfix(PostAdventureMenu __instance)
        {
            StartReadyWait(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "ShowDefeat", new[] { typeof(LoseConditionFulfilledPayload) })]
        [HarmonyPostfix]
        private static void PostAdventureMenuShowDefeatPostfix(PostAdventureMenu __instance)
        {
            StartReadyWait(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "HandleContinueCampaignClicked")]
        [HarmonyPrefix]
        private static void PostAdventureMenuContinuePrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "HandleRestartMapClicked")]
        [HarmonyPrefix]
        private static void PostAdventureMenuRestartPrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "HandleLoadClicked")]
        [HarmonyPrefix]
        private static void PostAdventureMenuLoadPrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "HandleQuitToMainClicked")]
        [HarmonyPrefix]
        private static void PostAdventureMenuQuitToMainPrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "HandleLinkToPlayerStats")]
        [HarmonyPrefix]
        private static void PostAdventureMenuPlayerStatsPrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        [HarmonyPatch(typeof(PostAdventureMenu), "Hide")]
        [HarmonyPrefix]
        private static void PostAdventureMenuHidePrefix(PostAdventureMenu __instance)
        {
            NotifyClosedIfTopScreen(__instance);
        }

        private static void StartReadyWait(PostAdventureMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForPostAdventureResultReady(menu));
            }
        }

        private static IEnumerator WaitForPostAdventureResultReady(PostAdventureMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 600)
            {
                PostAdventureResultAdapter adapter = new PostAdventureResultAdapter(menu);
                if (adapter.IsReadyAfterAnimation())
                {
                    SocAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureResultReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static void NotifyClosedIfTopScreen(PostAdventureMenu menu)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureResultClosed(menu);
        }
    }
}
