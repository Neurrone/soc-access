using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Map;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CampaignMapSelectPatches
    {
        private static readonly FieldInfo InformationViewField =
            AccessTools.Field(typeof(CampaignMapSelectMenu), "_informationView");
        private static readonly FieldInfo SelectedButtonField =
            AccessTools.Field(typeof(CampaignMapSelectMenu), "_selectedButton");
        private static readonly Dictionary<CampaignMapSelectedInformationView, CampaignMapSelectMenu> MenusByInformationView =
            new Dictionary<CampaignMapSelectedInformationView, CampaignMapSelectMenu>();

        [HarmonyPatch(typeof(CampaignMapSelectMenu), "Initialize")]
        [HarmonyPostfix]
        private static void CampaignMapSelectMenuInitializePostfix(CampaignMapSelectMenu __instance)
        {
            CampaignMapSelectedInformationView informationView = GetInformationView(__instance);
            if (informationView != null)
            {
                MenusByInformationView[informationView] = __instance;
            }
        }

        [HarmonyPatch(typeof(CampaignMapSelectMenu), "Dispose")]
        [HarmonyPostfix]
        private static void CampaignMapSelectMenuDisposePostfix(CampaignMapSelectMenu __instance)
        {
            CampaignMapSelectedInformationView informationView = GetInformationView(__instance);
            if (informationView != null)
            {
                MenusByInformationView.Remove(informationView);
            }
        }

        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Show")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewShowPostfix(
            CampaignMapSelectedInformationView __instance,
            ICampaignMapDefinition mapDefinition,
            MapFormat map,
            string path)
        {
            CampaignMapSelectMenu menu;
            MenusByInformationView.TryGetValue(__instance, out menu);
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCampaignMapSelectShown(menu, __instance);
        }

        [HarmonyPatch(typeof(CampaignMapSelectMenu), "HandleMapButtonClicked")]
        [HarmonyPrefix]
        private static void CampaignMapSelectMenuHandleMapButtonClickedPrefix(
            CampaignMapSelectMenu __instance,
            CampaignMapButton button,
            ref bool __state)
        {
            __state = IsSelectedButton(__instance, button);
        }

        [HarmonyPatch(typeof(CampaignMapSelectMenu), "HandleMapButtonClicked")]
        [HarmonyPostfix]
        private static void CampaignMapSelectMenuHandleMapButtonClickedPostfix(
            CampaignMapSelectMenu __instance,
            bool __state)
        {
            if (__state)
            {
                return;
            }

            CampaignMapSelectedInformationView informationView = GetInformationView(__instance);
            if (informationView == null)
            {
                return;
            }

            SoqAccessPlugin.Instance?.ScreenDetector?.OnCampaignMapSelectShown(__instance, informationView);
        }

        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Dispose")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewDisposePostfix(CampaignMapSelectedInformationView __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCampaignMapSelectClosed(__instance);
        }

        private static CampaignMapSelectedInformationView GetInformationView(CampaignMapSelectMenu menu)
        {
            if (menu == null || InformationViewField == null)
            {
                return null;
            }

            return InformationViewField.GetValue(menu) as CampaignMapSelectedInformationView;
        }

        private static bool IsSelectedButton(CampaignMapSelectMenu menu, CampaignMapButton button)
        {
            if (menu == null || button == null || SelectedButtonField == null)
            {
                return false;
            }

            return ReferenceEquals(SelectedButtonField.GetValue(menu), button);
        }
    }
}
