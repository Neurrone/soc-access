using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class MainMenuPatches
    {
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> LeftButtonContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_leftButtonContainer");

        private sealed class PendingOpen { public bool Cancelled; }

        private static readonly Dictionary<MainMenu, PendingOpen> PendingOpenCoroutines =
            new Dictionary<MainMenu, PendingOpen>();

        internal static void Reset()
        {
            foreach (PendingOpen pending in PendingOpenCoroutines.Values) pending.Cancelled = true;
            PendingOpenCoroutines.Clear();
        }

        [HarmonyPatch(typeof(MainMenu), "HandleSceneLoaded")]
        [HarmonyPostfix]
        private static void HandleSceneLoadedPostfix(MainMenu __instance, MainMenuSceneType loadedScene)
        {
            if (__instance == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnMainMenuSceneLoaded(loadedScene);

            if (loadedScene != MainMenuSceneType.MainMenu)
            {
                StopPendingOpenCoroutine(__instance);
                SocAccessMod.Instance?.ScreenDetector?.OnMainMenuClosed(__instance);
                return;
            }

            RestartPendingOpenCoroutine(__instance);
        }

        [HarmonyPatch(typeof(MainMenu), "HandleFoldoutOpened")]
        [HarmonyPostfix]
        private static void HandleFoldoutOpenedPostfix(MainMenu __instance, FoldoutUIButton button)
        {
            if (__instance == null || button == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnMainMenuFoldoutReady(__instance, button);
        }

        [HarmonyPatch(typeof(FoldoutUIButton), "ForceClose")]
        [HarmonyPostfix]
        private static void FoldoutForceClosePostfix(FoldoutUIButton __instance)
        {
            if (__instance == null)
            {
                return;
            }

            MainMenu mainMenu = ((Component)__instance).GetComponentInParent<MainMenu>();
            if (mainMenu == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnMainMenuFoldoutClosed(__instance);
        }

        [HarmonyPatch(typeof(MainMenu), "OnDestroy")]
        [HarmonyPrefix]
        private static void MainMenuOnDestroyPrefix(MainMenu __instance)
        {
            if (__instance == null)
            {
                return;
            }

            StopPendingOpenCoroutine(__instance);
            SocAccessMod.Instance?.ScreenDetector?.OnMainMenuClosed(__instance);
        }

        private static void RestartPendingOpenCoroutine(MainMenu mainMenu)
        {
            StopPendingOpenCoroutine(mainMenu);

            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                return;
            }

            PendingOpen pending = new PendingOpen();
            PendingOpenCoroutines[mainMenu] = pending;
            plugin.StartCoroutine(WaitForMainMenuVisible(mainMenu, pending));
        }

        private static void StopPendingOpenCoroutine(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                Reset();
                return;
            }

            PendingOpen pending;
            if (!PendingOpenCoroutines.TryGetValue(mainMenu, out pending))
            {
                return;
            }

            pending.Cancelled = true;
            PendingOpenCoroutines.Remove(mainMenu);
        }

        private static IEnumerator WaitForMainMenuVisible(MainMenu mainMenu, PendingOpen pending)
        {
            MainMenu trackedMenu = mainMenu;
            while (mainMenu != null)
            {
                if (pending.Cancelled) yield break;
                GameObject leftButtonContainer = LeftButtonContainerRef(mainMenu);
                // HandleSceneLoaded is the stable "we entered the main menu scene" signal,
                // but the menu is not actually navigable until DelayedEntry enables the
                // left button container. Wait for that visual readiness point before we
                // push the accessibility screen.
                if (leftButtonContainer != null && leftButtonContainer.activeInHierarchy)
                {
                    PendingOpenCoroutines.Remove(trackedMenu);
                    SocAccessMod.Instance?.ScreenDetector?.OnMainMenuReady(mainMenu);
                    yield break;
                }

                yield return null;
            }

            if (!pending.Cancelled) PendingOpenCoroutines.Remove(trackedMenu);
        }
    }
}
