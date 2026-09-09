using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CampaignMapSelectPatches
    {
        private static readonly FieldInfo InformationViewField =
            AccessTools.Field(typeof(CampaignMapSelectMenu), "_informationView");
        private static readonly FieldInfo SelectedButtonField =
            AccessTools.Field(typeof(CampaignMapSelectMenu), "_selectedButton");

        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Show")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewShowPostfix(
            CampaignMapSelectedInformationView __instance,
            ICampaignMapDefinition mapDefinition,
            MapFormat map,
            string path)
        {
            // The menu is found from the view every time rather than remembered from a hook the mod
            // may have missed: a hot reload lands mid-page and the view's own Show is the first thing
            // this class sees.
            SocAccessMod.Instance?.ScreenDetector?.OnCampaignMapSelectShown(
                CampaignMapSelectScreen.FindMenu(__instance),
                __instance);
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

            SocAccessMod.Instance?.ScreenDetector?.OnCampaignMapSelectShown(__instance, informationView);
        }

        [HarmonyPatch(typeof(CampaignMapSelectedInformationView), "Dispose")]
        [HarmonyPostfix]
        private static void CampaignMapSelectedInformationViewDisposePostfix(CampaignMapSelectedInformationView __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCampaignMapSelectClosed(__instance);
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
