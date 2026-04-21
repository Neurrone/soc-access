using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Settings;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class MainMenuPatches
    {
        private static readonly AccessTools.FieldRef<MainMenu, IClientSettings> ClientSettingsRef =
            AccessTools.FieldRefAccess<MainMenu, IClientSettings>("_clientSettings");

        private static readonly Type UnityCloudType = AccessTools.TypeByName("UnityCloud");
        private static readonly System.Reflection.PropertyInfo UnityCloudHasOptInConsentProperty =
            UnityCloudType != null ? AccessTools.Property(UnityCloudType, "HasOptInConsent") : null;

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
    }
}
