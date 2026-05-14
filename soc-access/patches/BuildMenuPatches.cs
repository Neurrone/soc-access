using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class BuildMenuPatches
    {
        [HarmonyPatch(typeof(BuildMenu), "Show")]
        [HarmonyPrefix]
        private static void BuildMenuShowPrefix(BuildMenu __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        [HarmonyPatch(typeof(BuildMenu), "Show")]
        [HarmonyPostfix]
        private static void BuildMenuShowPostfix(BuildMenu __instance, bool __state)
        {
            if (__state)
            {
                return;
            }

            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        private static void BuildMenuClosePrefix(BuildMenu __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        [HarmonyPatch(typeof(BuildMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void BuildMenuClosePostfix(BuildMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnBuildMenuClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(BuildMenu), "SetBuildSite")]
        [HarmonyPostfix]
        private static void BuildMenuSetBuildSitePostfix(BuildMenu __instance)
        {
            StartNotifySiteChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleSmallBuildingsClicked")]
        [HarmonyPostfix]
        private static void BuildMenuHandleSmallBuildingsClickedPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleMediumBuildingsClicked")]
        [HarmonyPostfix]
        private static void BuildMenuHandleMediumBuildingsClickedPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleLargeBuildingsClicked")]
        [HarmonyPostfix]
        private static void BuildMenuHandleLargeBuildingsClickedPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleCategorySwitch")]
        [HarmonyPostfix]
        private static void BuildMenuHandleCategorySwitchPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleCategorySwitchLeft")]
        [HarmonyPostfix]
        private static void BuildMenuHandleCategorySwitchLeftPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleCategorySwitchRight")]
        [HarmonyPostfix]
        private static void BuildMenuHandleCategorySwitchRightPostfix(BuildMenu __instance)
        {
            NotifyCategoryChanged(__instance);
        }

        private static void NotifyCategoryChanged(BuildMenu menu)
        {
            StartNotifyContentChanged(menu);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleBuildingSelected")]
        [HarmonyPostfix]
        private static void BuildMenuHandleBuildingSelectedPostfix(BuildMenu __instance)
        {
            StartNotifyContentChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleSelectBuildingDirection")]
        [HarmonyPostfix]
        private static void BuildMenuHandleSelectBuildingDirectionPostfix(BuildMenu __instance)
        {
            StartNotifyContentChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleLevelClicked")]
        [HarmonyPostfix]
        private static void BuildMenuHandleLevelClickedPostfix(BuildMenu __instance)
        {
            StartNotifyContentChanged(__instance);
        }

        [HarmonyPatch(typeof(BuildMenu), "HandleCycleTier")]
        [HarmonyPostfix]
        private static void BuildMenuHandleCycleTierPostfix(BuildMenu __instance)
        {
            StartNotifyContentChanged(__instance);
        }

        private static void StartNotifySiteChanged(BuildMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(NotifySiteChangedNextFrame(menu));
            }
        }

        private static void StartNotifyContentChanged(BuildMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(NotifyContentChangedNextFrame(menu));
            }
        }

        private static IEnumerator NotifySiteChangedNextFrame(BuildMenu menu)
        {
            yield return null;
            BuildMenuAdapter adapter = new BuildMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnBuildMenuSiteChanged(menu);
            }
        }

        private static IEnumerator NotifyContentChangedNextFrame(BuildMenu menu)
        {
            yield return null;
            BuildMenuAdapter adapter = new BuildMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnBuildMenuCategoryChanged(menu);
            }
        }

        private static void StartWaitForReady(BuildMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu));
            }
        }

        private static IEnumerator WaitForReady(BuildMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                BuildMenuAdapter adapter = new BuildMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessPlugin.Instance?.ScreenDetector?.OnBuildMenuReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
