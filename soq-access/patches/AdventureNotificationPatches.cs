using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Rewards;
using SongsOfConquestAccess.Events;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureNotificationPatches
    {
        [HarmonyPatch(typeof(AdventureMenuSystem), "ShowRewardWorldNotification", new Type[] { typeof(int), typeof(Vector2Int), typeof(List<RuntimeRewardDataContainer>) })]
        [HarmonyPostfix]
        private static void ShowRewardWorldNotificationPostfix(int commanderId, Vector2Int rewardTilePos, List<RuntimeRewardDataContainer> rewardDataContainers)
        {
            if (rewardDataContainers == null || rewardDataContainers.Count == 0)
            {
                SoqAccessPlugin.Instance?.LogInfo("Adventure reward notification had no reward data at " + FormatTile(rewardTilePos) + " for commander " + commanderId);
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo("Adventure reward notification at " + FormatTile(rewardTilePos) + " for commander " + commanderId);
            AccessibilityEventBus.Publish(new WorldRewardNotificationEvent(commanderId, rewardTilePos, rewardDataContainers));
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

            SoqAccessPlugin.Instance?.LogInfo("Adventure world notification for entity " + entityId + " commander " + commanderId);
            AccessibilityEventBus.Publish(new WorldMessageNotificationEvent(entityId, commanderId, localizedHeader, localizedBody, localizedEffects));
        }

        [HarmonyPatch(typeof(AdventureMenuSystem), "HandleDeniedMove")]
        [HarmonyPostfix]
        private static void HandleDeniedMovePostfix(AdventureMenuSystem __instance, DeniedMoveReason reason)
        {
            string localizedMessage = GetDeniedMoveSpeech(__instance, reason);
            if (string.IsNullOrWhiteSpace(localizedMessage))
            {
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo("Adventure denied move notification: " + reason);
            AccessibilityEventBus.Publish(new DeniedMoveNotificationEvent(reason, localizedMessage));
        }

        [HarmonyPatch(typeof(AdventureMenuSystem), "HandleDeniedEntityInteraction")]
        [HarmonyPostfix]
        private static void HandleDeniedEntityInteractionPostfix(OnDeniedEntityInteractionPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            // Larger denied-interaction feedback is routed by the game through
            // ShowWorldNotification(...), which is patched separately. Publishing
            // here too would announce the same visual notification twice.
            if (!payload.UseSmallerNotification)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.LocalizedMessage))
            {
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo("Adventure denied entity notification for entity " + payload.EntityId);
            AccessibilityEventBus.Publish(new DeniedEntityInteractionNotificationEvent(
                payload.EntityId,
                payload.CommanderId,
                payload.LocalizedEntityName,
                payload.LocalizedMessage));
        }

        private static string GetDeniedMoveSpeech(AdventureMenuSystem menuSystem, DeniedMoveReason reason)
        {
            string key = GetDeniedMoveLocalizationKey(reason);
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            ILocalizationHandler localizationHandler = GetLocalizationHandler(menuSystem);
            if (localizationHandler == null)
            {
                return key;
            }

            try
            {
                string text = localizationHandler.GetText(key);
                return string.IsNullOrWhiteSpace(text) ? key : text;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("Failed to localize denied move notification " + key + ": " + exception.Message);
                return key;
            }
        }

        private static string GetDeniedMoveLocalizationKey(DeniedMoveReason reason)
        {
            switch (reason)
            {
                case DeniedMoveReason.OutOfMovement:
                    return "Common/OutOfMovesNotification";
                case DeniedMoveReason.BattlefieldIsOccupied:
                    return "Common/DeniedMoveNotification/BattlefieldIsOccupied";
                case DeniedMoveReason.NotYourTurn:
                    return "Common/DeniedMoveNotification/NotYourTurn";
                case DeniedMoveReason.NoPath:
                    return "Common/DeniedMoveNotification/NoPath";
                case DeniedMoveReason.Unexplored:
                    return "Common/DeniedMoveNotification/Unexplored";
                default:
                    return string.Empty;
            }
        }

        private static ILocalizationHandler GetLocalizationHandler(AdventureMenuSystem menuSystem)
        {
            if (menuSystem == null)
            {
                return null;
            }

            try
            {
                return AccessTools.Field(typeof(AdventureMenuSystem), "_localizationHandler")?.GetValue(menuSystem) as ILocalizationHandler;
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("Failed to read AdventureMenuSystem localization handler: " + exception.Message);
                return null;
            }
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }
}
