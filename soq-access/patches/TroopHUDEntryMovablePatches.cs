using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class TroopHUDEntryMovablePatches
    {
        private static readonly FieldInfo CurrentStateField =
            AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");
        private static readonly Dictionary<int, string> LastStatesByInstanceId = new Dictionary<int, string>();

        [HarmonyPatch(typeof(TroopHUDEntryMovable), "Update")]
        [HarmonyPostfix]
        private static void TroopHUDEntryMovableUpdatePostfix(TroopHUDEntryMovable __instance)
        {
            if (__instance == null || CurrentStateField == null)
            {
                return;
            }

            int instanceId = ((Object)__instance).GetInstanceID();
            string currentState = GetStateName(__instance);
            string previousState;
            if (LastStatesByInstanceId.TryGetValue(instanceId, out previousState)
                && previousState == currentState)
            {
                return;
            }

            LastStatesByInstanceId[instanceId] = currentState;
            if (previousState == "Deciding" || currentState == "Deciding")
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.ResyncFromRuntimeState();
            }
        }

        private static string GetStateName(TroopHUDEntryMovable movable)
        {
            object value = CurrentStateField.GetValue(movable);
            return value != null ? value.ToString() : string.Empty;
        }
    }
}
