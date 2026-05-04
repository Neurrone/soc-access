using System.Collections;
using HarmonyLib;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class TaleSelectPatches
    {
        [HarmonyPatch(typeof(TaleButtonLayoutCoordinator), "RebuildLayout")]
        [HarmonyPostfix]
        private static void TaleButtonLayoutCoordinatorRebuildLayoutPostfix(
            TaleButtonLayoutCoordinator __instance,
            ref IEnumerator __result)
        {
            __result = WaitForTaleSelectLayout(__instance, __result);
        }

        private static IEnumerator WaitForTaleSelectLayout(TaleButtonLayoutCoordinator coordinator, IEnumerator original)
        {
            while (original != null && original.MoveNext())
            {
                yield return original.Current;
            }

            TaleSelectLifetimeNotifier.Attach(coordinator);
            SoqAccessPlugin.Instance?.LogInfo("TaleButtonLayoutCoordinator.RebuildLayout completed; notifying screen detector");
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTaleSelectLayoutRebuilt(coordinator);
        }
    }
}
