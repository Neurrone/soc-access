using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureNotificationPatches
    {
        private static readonly FieldInfo NotificationHudEntrySettingsField =
            AccessTools.Field(typeof(NotificationHUDEntry), "_settings");

        [HarmonyPatch(typeof(IconNotification), "ShowNotification")]
        [HarmonyPostfix]
        private static void IconNotificationShowNotificationPostfix(string localizedHeader, string localizedBody)
        {
            if (string.IsNullOrWhiteSpace(localizedHeader) && string.IsNullOrWhiteSpace(localizedBody))
            {
                return;
            }

            AccessibilityEventBus.Publish(new AdventureIconNotificationEvent(localizedHeader, localizedBody));
        }

        [HarmonyPatch(typeof(SimpleNotification), "Show", new Type[] { typeof(string), typeof(Vector3), typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void SimpleNotificationShowPostfix(string localizedString)
        {
            string normalized = SpeechTextSanitizer.Normalize(localizedString);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            AccessibilityEventBus.Publish(new AdventureSimpleNotificationEvent(normalized));
        }

        [HarmonyPatch(typeof(LevelUpNotification), "ShowNotification")]
        [HarmonyPostfix]
        private static void LevelUpNotificationShowNotificationPostfix(string wielderName, int reachedLevel, int factionId, Transform parentNode)
        {
            if (string.IsNullOrWhiteSpace(wielderName) && reachedLevel <= 0)
            {
                return;
            }

            AccessibilityEventBus.Publish(new CommanderLevelUpNotificationEvent(wielderName, reachedLevel));
        }

        [HarmonyPatch(typeof(NotificationHUDEntry), "SetEntry")]
        [HarmonyPostfix]
        private static void NotificationHUDEntrySetEntryPostfix(NotificationHUDEntry __instance, NotificationHUDEntryInformation entryInfo, bool setAsLastSibling)
        {
            if (entryInfo == null || entryInfo.HasBeenShown)
            {
                return;
            }

            string text = GetNotificationHudEntryText(__instance);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = entryInfo.Text;
            }

            text = SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            AccessibilityEventBus.Publish(new AdventureHudNotificationEvent(text));
        }

        [HarmonyPatch(typeof(ObjectiveAnimation), "Show")]
        [HarmonyPostfix]
        private static void ObjectiveAnimationShowPostfix(ObjectiveAnimation __instance, ObjectivesHUD.LocalizedObjective localizedObjective, bool canBeCompleted, Vector3 destination, ObjectiveAnimation.ObjectiveState state)
        {
            string text = SpeechTextSanitizer.Normalize(GetVisibleText(__instance));
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            AccessibilityEventBus.Publish(new ObjectiveNotificationEvent(text));
        }

        [HarmonyPatch(typeof(AdventureNewRoundPopup), "Show")]
        [HarmonyPostfix]
        private static void AdventureNewRoundPopupShowPostfix(AdventureNewRoundPopup __instance, bool requireConfirm, bool blockVisual, string name)
        {
            if (requireConfirm)
            {
                return;
            }

            string text = SpeechTextSanitizer.Normalize(GetVisibleText(__instance));
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            AccessibilityEventBus.Publish(new AdventureNewTurnPopupEvent(text));
        }

        [HarmonyPatch(typeof(CenteredNotification), "Show")]
        [HarmonyPostfix]
        private static void CenteredNotificationShowPostfix(string text)
        {
            string normalized = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            AccessibilityEventBus.Publish(new CenteredNotificationEvent(normalized));
        }

        [HarmonyPatch(typeof(CenteredNotificationHeavy), "Show")]
        [HarmonyPostfix]
        private static void CenteredNotificationHeavyShowPostfix(string text)
        {
            string normalized = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            AccessibilityEventBus.Publish(new CenteredHeavyNotificationEvent(normalized));
        }

        [HarmonyPatch(typeof(AdventureMenuSystem), "ShowWorldNotification")]
        [HarmonyPostfix]
        private static void ShowWorldNotificationPostfix(int entityId, int commanderId, string localizedHeader, string localizedBody, string localizedEffects)
        {
            if (string.IsNullOrWhiteSpace(localizedHeader)
                && string.IsNullOrWhiteSpace(localizedBody)
                && string.IsNullOrWhiteSpace(localizedEffects))
            {
                return;
            }

            SocAccessMod.Instance?.LogInfo("Adventure world notification for entity " + entityId + " commander " + commanderId);
            AccessibilityEventBus.Publish(new WorldMessageNotificationEvent(entityId, commanderId, localizedHeader, localizedBody, localizedEffects));
        }

        private static string GetVisibleText(Component root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(false);
            if (textMeshes == null || textMeshes.Length == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || !((Component)textMesh).gameObject.activeInHierarchy)
                {
                    continue;
                }

                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(": ");
                }

                builder.Append(text);
            }

            return builder.ToString();
        }

        private static string GetNotificationHudEntryText(NotificationHUDEntry entry)
        {
            if (entry == null || NotificationHudEntrySettingsField == null)
            {
                return string.Empty;
            }

            try
            {
                NotificationHUDEntry.Settings settings =
                    NotificationHudEntrySettingsField.GetValue(entry) as NotificationHUDEntry.Settings;
                if (settings == null || settings.InformationText == null)
                {
                    return string.Empty;
                }

                return UITextMeshTextUtility.GetEffectiveText(settings.InformationText);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read notification HUD entry text: " + exception.Message);
                return string.Empty;
            }
        }
    }
}
