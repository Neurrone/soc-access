using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PostAdventureStatsMenuPatches
    {
        [HarmonyPatch(typeof(PostAdventureStatsMenu), "ShowMenu")]
        [HarmonyPostfix]
        private static void PostAdventureStatsMenuShowPostfix(PostAdventureStatsMenu __instance)
        {
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null && __instance != null)
            {
                plugin.StartCoroutine(WaitForPostAdventureStatsReady(__instance));
            }
        }

        [HarmonyPatch(typeof(PostAdventureStatsMenu), "CloseWindow")]
        [HarmonyPrefix]
        private static void PostAdventureStatsMenuClosePrefix(PostAdventureStatsMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureStatsClosed(__instance);
        }

        private static IEnumerator WaitForPostAdventureStatsReady(PostAdventureStatsMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 600)
            {
                PostAdventureStatsAdapter adapter = new PostAdventureStatsAdapter(menu);
                if (adapter.IsReadyAfterAnimation())
                {
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnPostAdventureStatsReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
