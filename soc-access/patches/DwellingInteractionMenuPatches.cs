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
    internal static class DwellingInteractionMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(DwellingInteractionMenu), "_async");

        [HarmonyPatch(typeof(DwellingInteractionMenu), "Show", new[]
        {
            typeof(IRecruitmentPoolComponent),
            typeof(int)
        })]
        [HarmonyPostfix]
        private static void DwellingInteractionMenuShowPostfix(DwellingInteractionMenu __instance)
        {
            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(DwellingInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPrefix]
        private static void DwellingInteractionMenuClosePrefix(DwellingInteractionMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) is Async;
        }

        [HarmonyPatch(typeof(DwellingInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void DwellingInteractionMenuClosePostfix(DwellingInteractionMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnDwellingInteractionClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(DwellingInteractionMenu), "HandleUpgradeClicked")]
        [HarmonyPostfix]
        private static void DwellingInteractionMenuHandleUpgradeClickedPostfix(DwellingInteractionMenu __instance)
        {
            StartWaitForUpgrade(__instance);
        }

        [HarmonyPatch(typeof(DwellingInteractionMenu), "HandleBackClicked")]
        [HarmonyPostfix]
        private static void DwellingInteractionMenuHandleBackClickedPostfix(DwellingInteractionMenu __instance)
        {
            StartWaitForDraftFromSubMenu(__instance);
        }

        private static void StartWaitForReady(DwellingInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu));
            }
        }

        private static void StartWaitForDraftFromSubMenu(DwellingInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForDraftFromSubMenu(menu));
            }
        }

        private static void StartWaitForUpgrade(DwellingInteractionMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForUpgrade(menu));
            }
        }

        private static IEnumerator WaitForReady(DwellingInteractionMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnDwellingInteractionReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForDraftFromSubMenu(DwellingInteractionMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menu);
                if (adapter.IsDraftPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnDwellingBackToTop(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private static IEnumerator WaitForUpgrade(DwellingInteractionMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menu);
                if (adapter.IsUpgradePresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnDwellingUpgradeReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
