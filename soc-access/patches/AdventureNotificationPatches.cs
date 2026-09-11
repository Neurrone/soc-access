using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class AdventureNotificationPatches
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
            string normalized = SpokenLines.Clean(localizedString);
            if (string.IsNullOrWhiteSpace(normalized) || IsNewArtifactBadge(normalized))
            {
                return;
            }

            AccessibilityEventBus.Publish(new AdventureSimpleNotificationEvent(normalized));
        }

        /// <summary>Whether this notification is the badge the inventory paints on an artifact that
        /// has just arrived. <c>InventoryHUD.TriggerNewNotifications</c> shows the one word
        /// "Common/New" over the icon of every artifact added since the panel was drawn, which is how
        /// a purchase in the market announced itself as a bare "New". A badge is not a message: the
        /// artifact is read where the player finds it, and the badge says nothing on its own.
        /// </summary>
        private static bool IsNewArtifactBadge(string normalized)
        {
            string badge = GameText.Get("Common/New", string.Empty);
            return !string.IsNullOrWhiteSpace(badge)
                && string.Equals(normalized.Trim(), badge.Trim(), StringComparison.CurrentCultureIgnoreCase);
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

            text = SpokenLines.Clean(text);
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
            string text = SpokenLines.Clean(GetVisibleText(__instance));
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

            string text = SpokenLines.Clean(GetVisibleText(__instance));
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
            string normalized = SongsOfConquestAccess.UI.SpokenLines.Clean(text);
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
            string normalized = SongsOfConquestAccess.UI.SpokenLines.Clean(text);
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

        // On the event path, not a build path: run inside the objective and new-round postfixes to
        // compose the one announcement each of them makes.
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

            List<string> parts = new List<string>();
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || !((Component)textMesh).gameObject.activeInHierarchy)
                {
                    continue;
                }

                parts.Add(SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh)));
            }

            return ModText.JoinList(ModStrings.UI.LabelValue, parts);
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
