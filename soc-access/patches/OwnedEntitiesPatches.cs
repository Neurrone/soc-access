using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Entities;

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

        [HarmonyPatch(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked")]
        [HarmonyPrefix]
        private static void CategoryTextClickedPrefix(KingdomEntityOverviewCategoryEntry __instance, ref IMapEntity __state)
        {
            __state = KingdomOverviewFocus.ReadEntity(
                __instance, CategoryParentField, "OwnedEntitiesPatches failed to read category parent");
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked")]
        [HarmonyPostfix]
        private static void CategoryTextClickedPostfix(IMapEntity __state)
        {
            KingdomOverviewFocus.Focus(__state);
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewClaimedEntry), "HandleButtonClicked")]
        [HarmonyPrefix]
        private static void ClaimedEntryButtonClickedPrefix(KingdomEntityOverviewClaimedEntry __instance, ref IMapEntity __state)
        {
            __state = KingdomOverviewFocus.ReadCycled(
                __instance,
                ClaimedEntryMapEntitiesField,
                ClaimedEntryCurrentCycleIndexField,
                "OwnedEntitiesPatches failed to read current owned entity row target");
        }

        [HarmonyPatch(typeof(KingdomEntityOverviewClaimedEntry), "HandleButtonClicked")]
        [HarmonyPostfix]
        private static void ClaimedEntryButtonClickedPostfix(IMapEntity __state)
        {
            KingdomOverviewFocus.Focus(__state);
        }
    }
}
