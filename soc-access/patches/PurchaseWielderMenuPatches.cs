using System.Collections;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PurchaseWielderMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(PurchaseWielderMenu), "_async");

        [HarmonyPatch(typeof(PurchaseWielderMenu), "Open", new[] { typeof(IMapEntity) })]
        [HarmonyPostfix]
        private static void PurchaseWielderMenuOpenPostfix(PurchaseWielderMenu __instance)
        {
            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(PurchaseWielderMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        private static void PurchaseWielderMenuClosePrefix(PurchaseWielderMenu __instance, out bool __state)
        {
            __state = __instance != null && AsyncField != null && AsyncField.GetValue(__instance) is Async;
        }

        [HarmonyPatch(typeof(PurchaseWielderMenu), "Close", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void PurchaseWielderMenuClosePostfix(PurchaseWielderMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnPurchaseWielderClosed(__instance);
            }
        }

        private static void StartWaitForReady(PurchaseWielderMenu menu)
        {
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu));
            }
        }

        private static IEnumerator WaitForReady(PurchaseWielderMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessPlugin.Instance?.ScreenDetector?.OnPurchaseWielderReady(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
