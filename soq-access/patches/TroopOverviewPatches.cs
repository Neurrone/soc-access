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
    internal static class TroopOverviewPatches
    {
        private static readonly FieldInfo TownEntryTownField =
            AccessTools.Field(typeof(KingdomTroopOverviewTownEntry), "_town");
        private static readonly FieldInfo IncomeEntryMapEntitiesField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_mapEntities");
        private static readonly FieldInfo IncomeEntryCurrentCycleIndexField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_currentCycleIndex");

        [HarmonyPatch(typeof(KingdomTroopOverviewMenu), "Show")]
        [HarmonyPostfix]
        private static void KingdomTroopOverviewShowPostfix(KingdomTroopOverviewMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTroopOverviewReady(__instance);
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewMenu), "Hide")]
        [HarmonyPostfix]
        private static void KingdomTroopOverviewHidePostfix(KingdomTroopOverviewMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTroopOverviewClosed(__instance);
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked")]
        [HarmonyPrefix]
        private static void TownNameClickedPrefix(KingdomTroopOverviewTownEntry __instance, ref IMapEntity __state)
        {
            __state = GetTown(__instance);
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked")]
        [HarmonyPostfix]
        private static void TownNameClickedPostfix(IMapEntity __state)
        {
            if (__state != null)
            {
                AccessibilityEventBus.Publish(new MapCameraFocusEvent(__state.Position, announce: true));
            }
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewIncomeEntry), "HandleButtonClicked")]
        [HarmonyPrefix]
        private static void IncomeEntryButtonClickedPrefix(KingdomTroopOverviewIncomeEntry __instance, ref IMapEntity __state)
        {
            __state = GetCurrentEntity(__instance);
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewIncomeEntry), "HandleButtonClicked")]
        [HarmonyPostfix]
        private static void IncomeEntryButtonClickedPostfix(IMapEntity __state)
        {
            if (__state != null)
            {
                AccessibilityEventBus.Publish(new MapCameraFocusEvent(__state.Position, announce: true));
            }
        }

        private static IMapEntity GetTown(KingdomTroopOverviewTownEntry entry)
        {
            if (entry == null || TownEntryTownField == null)
            {
                return null;
            }

            try
            {
                return TownEntryTownField.GetValue(entry) as IMapEntity;
            }
            catch (Exception ex)
            {
                SoqAccessPlugin.Instance?.LogWarning("TroopOverviewPatches failed to read town entry target: " + ex.Message);
                return null;
            }
        }

        private static IMapEntity GetCurrentEntity(KingdomTroopOverviewIncomeEntry entry)
        {
            if (entry == null || IncomeEntryMapEntitiesField == null || IncomeEntryCurrentCycleIndexField == null)
            {
                return null;
            }

            try
            {
                IMapEntity[] entities = IncomeEntryMapEntitiesField.GetValue(entry) as IMapEntity[];
                object indexValue = IncomeEntryCurrentCycleIndexField.GetValue(entry);
                int index = indexValue is int ? (int)indexValue : -1;
                if (entities == null || index < 0 || index >= entities.Length)
                {
                    return null;
                }

                return entities[index];
            }
            catch (Exception ex)
            {
                SoqAccessPlugin.Instance?.LogWarning("TroopOverviewPatches failed to read current troop overview row target: " + ex.Message);
                return null;
            }
        }
    }
}
