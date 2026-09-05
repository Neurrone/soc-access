using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Events;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class OwnedEntitiesPatches
    {
        private static readonly FieldInfo CategoryParentField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_parent");
        private static readonly FieldInfo ClaimedEntryMapEntitiesField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_mapEntities");
        private static readonly FieldInfo ClaimedEntryCurrentCycleIndexField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_currentCycleIndex");

        [HarmonyPatch(typeof(KingdomEntityOverviewMenu), "Show")]
        [HarmonyPostfix]
        private static void KingdomEntityOverviewShowPostfix(KingdomEntityOverviewMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnOwnedEntitiesReady(__instance);
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewMenu), "Hide")]
        [HarmonyPostfix]
        private static void KingdomEntityOverviewHidePostfix(KingdomEntityOverviewMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnOwnedEntitiesClosed(__instance);
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked")]
        [HarmonyPrefix]
        private static void CategoryTextClickedPrefix(KingdomEntityOverviewCategoryEntry __instance, ref IMapEntity __state)
        {
            __state = GetCategoryParent(__instance);
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked")]
        [HarmonyPostfix]
        private static void CategoryTextClickedPostfix(IMapEntity __state)
        {
            if (__state != null)
            {
                AccessibilityEventBus.Publish(new MapCameraFocusEvent(__state.Position, announce: true));
            }
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewClaimedEntry), "HandleButtonClicked")]
        [HarmonyPrefix]
        private static void ClaimedEntryButtonClickedPrefix(KingdomEntityOverviewClaimedEntry __instance, ref IMapEntity __state)
        {
            __state = GetCurrentEntity(__instance);
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewClaimedEntry), "HandleButtonClicked")]
        [HarmonyPostfix]
        private static void ClaimedEntryButtonClickedPostfix(IMapEntity __state)
        {
            if (__state != null)
            {
                AccessibilityEventBus.Publish(new MapCameraFocusEvent(__state.Position, announce: true));
            }
        }

        private static IMapEntity GetCategoryParent(KingdomEntityOverviewCategoryEntry entry)
        {
            if (entry == null || CategoryParentField == null)
            {
                return null;
            }

            try
            {
                return CategoryParentField.GetValue(entry) as IMapEntity;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("OwnedEntitiesPatches failed to read category parent: " + ex.Message);
                return null;
            }
        }

        private static IMapEntity GetCurrentEntity(KingdomEntityOverviewClaimedEntry entry)
        {
            if (entry == null || ClaimedEntryMapEntitiesField == null || ClaimedEntryCurrentCycleIndexField == null)
            {
                return null;
            }

            try
            {
                IMapEntity[] entities = ClaimedEntryMapEntitiesField.GetValue(entry) as IMapEntity[];
                object indexValue = ClaimedEntryCurrentCycleIndexField.GetValue(entry);
                int index = indexValue is int ? (int)indexValue : -1;
                if (entities == null || index < 0 || index >= entities.Length)
                {
                    return null;
                }

                return entities[index];
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("OwnedEntitiesPatches failed to read current owned entity row target: " + ex.Message);
                return null;
            }
        }
    }
}
