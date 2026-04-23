using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Settings;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class MainMenuPatches
    {
        private static readonly AccessTools.FieldRef<MainMenu, IClientSettings> ClientSettingsRef =
            AccessTools.FieldRefAccess<MainMenu, IClientSettings>("_clientSettings");
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> LeftButtonContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_leftButtonContainer");

        private static readonly Type UnityCloudType = AccessTools.TypeByName("UnityCloud");
        private static readonly System.Reflection.PropertyInfo UnityCloudHasOptInConsentProperty =
            UnityCloudType != null ? AccessTools.Property(UnityCloudType, "HasOptInConsent") : null;
        private static readonly Dictionary<MainMenu, Coroutine> PendingOpenCoroutines =
            new Dictionary<MainMenu, Coroutine>();

        [HarmonyPatch(typeof(MainMenu), "ShowAnalyticsConsentIfNecessary")]
        [HarmonyPrefix]
        private static void ShowAnalyticsConsentIfNecessaryPrefix(MainMenu __instance)
        {
            IClientSettings clientSettings = null;
            if (__instance != null)
            {
                clientSettings = ClientSettingsRef(__instance);
            }

            bool? previousClientSetting = clientSettings != null ? clientSettings.OptInAnalytics : null;
            bool? previousUnityCloudSetting = GetUnityCloudOptInConsent();

            if (clientSettings != null)
            {
                clientSettings.OptInAnalytics = null;
            }

            SetUnityCloudOptInConsent(null);

            SoqAccessPlugin.Instance?.LogInfo(
                "MainMenu.ShowAnalyticsConsentIfNecessary prefix cleared analytics consent state: client="
                + NullableBoolToString(previousClientSetting)
                + " -> null, unityCloud="
                + NullableBoolToString(previousUnityCloudSetting)
                + " -> null");
        }

        [HarmonyPatch(typeof(MainMenu), "HandleSceneLoaded")]
        [HarmonyPostfix]
        private static void HandleSceneLoadedPostfix(MainMenu __instance, MainMenuSceneType loadedScene)
        {
            if (__instance == null)
            {
                return;
            }

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

        private static bool? GetUnityCloudOptInConsent()
        {
            if (UnityCloudHasOptInConsentProperty == null)
            {
                return null;
            }

            object value = UnityCloudHasOptInConsentProperty.GetValue(null, null);
            if (value == null)
            {
                return null;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return null;
        }

        private static void SetUnityCloudOptInConsent(bool? value)
        {
            if (UnityCloudHasOptInConsentProperty == null || !UnityCloudHasOptInConsentProperty.CanWrite)
            {
                return;
            }

            UnityCloudHasOptInConsentProperty.SetValue(null, value, null);
        }

        private static string NullableBoolToString(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "<null>";
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
