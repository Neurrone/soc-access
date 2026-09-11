using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Entities;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class TroopOverviewPatches
    {
        private static readonly FieldInfo TownEntryTownField =
            AccessTools.Field(typeof(KingdomTroopOverviewTownEntry), "_town");
        private static readonly FieldInfo IncomeEntryMapEntitiesField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_mapEntities");
        private static readonly FieldInfo IncomeEntryCurrentCycleIndexField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_currentCycleIndex");

        [HarmonyPatch(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked")]
        [HarmonyPrefix]
        private static void TownNameClickedPrefix(KingdomTroopOverviewTownEntry __instance, ref IMapEntity __state)
        {
            __state = KingdomOverviewFocus.ReadEntity(
                __instance, TownEntryTownField, "TroopOverviewPatches failed to read town entry target");
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked")]
        [HarmonyPostfix]
        private static void TownNameClickedPostfix(IMapEntity __state)
        {
            KingdomOverviewFocus.Focus(__state);
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewIncomeEntry), "HandleButtonClicked")]
        [HarmonyPrefix]
        private static void IncomeEntryButtonClickedPrefix(KingdomTroopOverviewIncomeEntry __instance, ref IMapEntity __state)
        {
            __state = KingdomOverviewFocus.ReadCycled(
                __instance,
                IncomeEntryMapEntitiesField,
                IncomeEntryCurrentCycleIndexField,
                "TroopOverviewPatches failed to read current troop overview row target");
        }

        [HarmonyPatch(typeof(KingdomTroopOverviewIncomeEntry), "HandleButtonClicked")]
        [HarmonyPostfix]
        private static void IncomeEntryButtonClickedPostfix(IMapEntity __state)
        {
            KingdomOverviewFocus.Focus(__state);
        }
    }
}
