using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class AdventureLobbyChallengeMapSelectPatches
    {
        [HarmonyPatch(typeof(ChallengeMapsMenu), "Show")]
        [HarmonyPostfix]
        private static void ChallengeMapsMenuShowPostfix(ChallengeMapsMenu __instance)
        {
            SocAccessMod.Instance?.StartCoroutine(WaitForChallengeMapSelectMenuReady(__instance));
        }

        [HarmonyPatch(typeof(ChallengeMapsMenu), "Hide")]
        [HarmonyPostfix]
        private static void ChallengeMapsMenuHidePostfix(ChallengeMapsMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyChallengeMapSelectClosed(__instance);
        }

        [HarmonyPatch(typeof(ChallengeMapsMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void ChallengeMapsMenuOnDestroyPostfix(ChallengeMapsMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyChallengeMapSelectClosed(__instance);
        }

        [HarmonyPatch(typeof(ChallengeMapsMenu), "SetSelectedEntry")]
        [HarmonyPostfix]
        private static void ChallengeMapsMenuSetSelectedEntryPostfix(ChallengeMapsMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyChallengeMapSelectSelectionChanged(__instance);
        }

        private static IEnumerator WaitForChallengeMapSelectMenuReady(ChallengeMapsMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                AdventureLobbyChallengeMapSelectAdapter adapter = new AdventureLobbyChallengeMapSelectAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyChallengeMapSelectReady(menu);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
