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

        private static readonly Dictionary<MainMenu, Coroutine> PendingOpenCoroutines =
            new Dictionary<MainMenu, Coroutine>();

        [HarmonyPatch(typeof(MainMenu), "HandleSceneLoaded")]
        [HarmonyPostfix]
        private static void HandleSceneLoadedPostfix(MainMenu __instance, MainMenuSceneType loadedScene)
        {
            if (__instance == null)
            {
                return;
            }

            SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuSceneLoaded(loadedScene);

            if (loadedScene != MainMenuSceneType.MainMenu)
            {
                StopPendingOpenCoroutine(__instance);
                SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuHidden(__instance);
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

            SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuFoldoutOpened(__instance, button);
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

            SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuFoldoutClosed(__instance);
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
            SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuHidden(__instance);
        }

        private static void RestartPendingOpenCoroutine(MainMenu mainMenu)
        {
            StopPendingOpenCoroutine(mainMenu);

            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            Coroutine coroutine = plugin.StartCoroutine(WaitForMainMenuVisible(mainMenu));
            PendingOpenCoroutines[mainMenu] = coroutine;
        }

        private static void StopPendingOpenCoroutine(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                PendingOpenCoroutines.Clear();
                return;
            }

            Coroutine coroutine;
            if (!PendingOpenCoroutines.TryGetValue(mainMenu, out coroutine))
            {
                return;
            }

            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null && coroutine != null)
            {
                plugin.StopCoroutine(coroutine);
            }

            PendingOpenCoroutines.Remove(mainMenu);
        }

        private static IEnumerator WaitForMainMenuVisible(MainMenu mainMenu)
        {
            MainMenu trackedMenu = mainMenu;
            while (mainMenu != null)
            {
                GameObject leftButtonContainer = LeftButtonContainerRef(mainMenu);
                // HandleSceneLoaded is the stable "we entered the main menu scene" signal,
                // but the menu is not actually navigable until DelayedEntry enables the
                // left button container. Wait for that visual readiness point before we
                // push the accessibility screen.
                if (leftButtonContainer != null && leftButtonContainer.activeInHierarchy)
                {
                    PendingOpenCoroutines.Remove(trackedMenu);
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnMainMenuAvailable(mainMenu);
                    yield break;
                }

                yield return null;
            }

            PendingOpenCoroutines.Remove(trackedMenu);
        }
    }
}
