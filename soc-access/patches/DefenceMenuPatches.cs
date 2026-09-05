using System.Collections;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class DefenceMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(DefenceMenu), "_async");

        [HarmonyPatch(typeof(DefenceMenu), "Show", new[]
        {
            typeof(IMapEntity)
        })]
        [HarmonyPostfix]
        private static void DefenceMenuShowPostfix(DefenceMenu __instance)
        {
            StartWaitForTopLevel(__instance);
        }

        [HarmonyPatch(typeof(DefenceMenu), "Hide", new[]
        {
            typeof(bool)
        })]
        [HarmonyPrefix]
        private static void DefenceMenuHidePrefix(DefenceMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) is Async;
        }

        [HarmonyPatch(typeof(DefenceMenu), "Hide", new[]
        {
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void DefenceMenuHidePostfix(DefenceMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnDefenceMenuClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(DefenceMenu), "HandlePurchaseTroopsClicked")]
        [HarmonyPostfix]
        private static void DefenceMenuHandlePurchaseTroopsClickedPostfix(DefenceMenu __instance)
        {
            StartWaitForDraft(__instance);
        }

        [HarmonyPatch(typeof(DefenceMenu), "HandleUpgradeTroopsClicked")]
        [HarmonyPostfix]
        private static void DefenceMenuHandleUpgradeTroopsClickedPostfix(DefenceMenu __instance)
        {
            StartWaitForUpgrade(__instance);
        }

        [HarmonyPatch(typeof(DefenceMenu), "ShowTopLevel")]
        [HarmonyPostfix]
        private static void DefenceMenuShowTopLevelPostfix(DefenceMenu __instance)
        {
            StartWaitForTopLevelFromSubMenu(__instance);
        }

        private static void StartWaitForTopLevel(DefenceMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForTopLevel(menu, fromSubMenu: false));
            }
        }

        private static void StartWaitForTopLevelFromSubMenu(DefenceMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForTopLevel(menu, fromSubMenu: true));
            }
        }

        private static void StartWaitForDraft(DefenceMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForDraft(menu));
            }
        }

        private static void StartWaitForUpgrade(DefenceMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForUpgrade(menu));
            }
        }

        private static IEnumerator WaitForTopLevel(DefenceMenu menu, bool fromSubMenu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menu);
                if (adapter.IsTopLevelPresent())
                {
                    if (fromSubMenu)
                    {
                        SocAccessMod.Instance?.ScreenDetector?.OnDefenceMenuBackToTop(menu);
                    }
                    else
                    {
                        SocAccessMod.Instance?.ScreenDetector?.OnDefenceMenuReady(menu);
                    }

                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForDraft(DefenceMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menu);
                if (adapter.IsDraftPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnDefenceDraftReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForUpgrade(DefenceMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menu);
                if (adapter.IsUpgradePresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnDefenceUpgradeReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
