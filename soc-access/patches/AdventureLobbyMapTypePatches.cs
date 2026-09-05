using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class AdventureLobbyMapTypePatches
    {
        private static readonly AccessTools.FieldRef<MapTypeMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<MapTypeMenu, CanvasGroup>("_canvasGroup");

        [HarmonyPatch(typeof(MapTypeMenu), "Show")]
        [HarmonyPostfix]
        private static void MapTypeMenuShowPostfix(MapTypeMenu __instance)
        {
            SocAccessMod.Instance?.StartCoroutine(WaitForMapTypeMenuReady(__instance));
        }

        [HarmonyPatch(typeof(MapTypeMenu), "Hide")]
        [HarmonyPostfix]
        private static void MapTypeMenuHidePostfix(MapTypeMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapTypeClosed(__instance);
        }

        private static IEnumerator WaitForMapTypeMenuReady(MapTypeMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                CanvasGroup canvasGroup = CanvasGroupRef(menu);
                GameObject gameObject = ((Component)menu).gameObject;
                if (gameObject != null
                    && gameObject.activeInHierarchy
                    && (canvasGroup == null || canvasGroup.blocksRaycasts || canvasGroup.alpha > 0.5f))
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyMapTypeReady(menu);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
