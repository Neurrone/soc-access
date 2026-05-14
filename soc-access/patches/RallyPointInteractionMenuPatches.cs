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
    internal static class RallyPointInteractionMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_async");

        [HarmonyPatch(typeof(RallyPointInteractionMenu), "Show", new[]
        {
            typeof(IRallyPointRecruitmentPoolComponent),
            typeof(int)
        })]
        [HarmonyPostfix]
        private static void RallyPointInteractionMenuShowPostfix(RallyPointInteractionMenu __instance)
        {
            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(RallyPointInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPrefix]
        private static void RallyPointInteractionMenuClosePrefix(RallyPointInteractionMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) is Async;
        }

        [HarmonyPatch(typeof(RallyPointInteractionMenu), "Close", new[]
        {
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void RallyPointInteractionMenuClosePostfix(RallyPointInteractionMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnRallyPointClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(RallyPointInteractionMenu), "HandleEntrySelected")]
        [HarmonyPostfix]
        private static void RallyPointInteractionMenuHandleEntrySelectedPostfix(RallyPointInteractionMenu __instance)
        {
            StartWaitForChanged(__instance);
        }

        private static void StartWaitForReady(RallyPointInteractionMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu, initialOpen: true));
            }
        }

        private static void StartWaitForChanged(RallyPointInteractionMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu, initialOpen: false));
            }
        }

        private static IEnumerator WaitForReady(RallyPointInteractionMenu menu, bool initialOpen)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                RallyPointInteractionMenuAdapter adapter = new RallyPointInteractionMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    if (initialOpen)
                    {
                        SocAccessPlugin.Instance?.ScreenDetector?.OnRallyPointReady(menu);
                    }
                    else
                    {
                        SocAccessPlugin.Instance?.ScreenDetector?.OnRallyPointChanged(menu);
                    }

                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
