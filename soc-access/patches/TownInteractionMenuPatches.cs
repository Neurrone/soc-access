using System.Collections;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class TownInteractionMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(TownInteractionMenu), "_async");

        [HarmonyPatch(typeof(TownInteractionMenu), "Show", new[]
        {
            typeof(IRecruitmentPoolComponent),
            typeof(int)
        })]
        [HarmonyPostfix]
        private static void TownInteractionMenuShowPostfix(TownInteractionMenu __instance)
        {
            StartWaitForTopLevel(__instance);
        }

        [HarmonyPatch(typeof(TownInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPrefix]
        private static void TownInteractionMenuClosePrefix(TownInteractionMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) is Async;
        }

        [HarmonyPatch(typeof(TownInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void TownInteractionMenuClosePostfix(TownInteractionMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnSettlementClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(TownInteractionMenu), "HandlePurchaseClicked")]
        [HarmonyPostfix]
        private static void TownInteractionMenuHandlePurchaseClickedPostfix(TownInteractionMenu __instance)
        {
            StartWaitForDraft(__instance);
        }

        [HarmonyPatch(typeof(TownInteractionMenu), "HandleUpgradeClicked")]
        [HarmonyPostfix]
        private static void TownInteractionMenuHandleUpgradeClickedPostfix(TownInteractionMenu __instance)
        {
            StartWaitForUpgrade(__instance);
        }

        [HarmonyPatch(typeof(TownInteractionMenu), "HandleBackClicked")]
        [HarmonyPostfix]
        private static void TownInteractionMenuHandleBackClickedPostfix(TownInteractionMenu __instance)
        {
            StartWaitForTopLevelFromSubMenu(__instance);
        }

        private static void StartWaitForTopLevel(TownInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForTopLevel(menu, fromSubMenu: false));
            }
        }

        private static void StartWaitForTopLevelFromSubMenu(TownInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForTopLevel(menu, fromSubMenu: true));
            }
        }

        private static void StartWaitForDraft(TownInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForDraft(menu));
            }
        }

        private static void StartWaitForUpgrade(TownInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForUpgrade(menu));
            }
        }

        private static IEnumerator WaitForTopLevel(TownInteractionMenu menu, bool fromSubMenu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menu);
                if (adapter.IsTopLevelPresent())
                {
                    if (fromSubMenu)
                    {
                        SocAccessMod.Instance?.ScreenDetector?.OnSettlementBackToTop(menu);
                    }
                    else
                    {
                        SocAccessMod.Instance?.ScreenDetector?.OnSettlementReady(menu);
                    }

                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForDraft(TownInteractionMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menu);
                if (adapter.IsDraftPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnSettlementDraftReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForUpgrade(TownInteractionMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menu);
                if (adapter.IsUpgradePresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnSettlementUpgradeReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
