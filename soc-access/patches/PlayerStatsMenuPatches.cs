using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class PlayerStatsMenuPatches
    {
        [HarmonyPatch(typeof(PlayerStatsMenuNavigation), "Start")]
        [HarmonyPostfix]
        private static void PlayerStatsMenuNavigationStartPostfix(PlayerStatsMenuNavigation __instance, ref IEnumerator __result)
        {
            __result = WaitForPlayerStatsReady(__instance, __result);
        }

        [HarmonyPatch(typeof(PlayerStatsMenuNavigation), "OnDestroy")]
        [HarmonyPrefix]
        private static void PlayerStatsMenuNavigationOnDestroyPrefix(PlayerStatsMenuNavigation __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnPlayerStatsClosed(__instance);
        }

        private static IEnumerator WaitForPlayerStatsReady(PlayerStatsMenuNavigation menu, IEnumerator original)
        {
            while (original != null && original.MoveNext())
            {
                yield return original.Current;
            }

            int frames = 0;
            while (menu != null && frames < 600)
            {
                PlayerStatsAdapter adapter = new PlayerStatsAdapter(menu);
                if (adapter.IsReadyAfterAnimation())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnPlayerStatsReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
