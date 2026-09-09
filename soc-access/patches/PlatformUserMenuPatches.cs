using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class PlatformUserMenuPatches
    {
        private static readonly FieldInfo ButtonLabelField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_buttonLabel");
        private static readonly FieldInfo UserButtonTypeField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_userButtonType");
        // No Reset, and none is wanted: a Time.realtimeSinceStartup stamp holding no game
        // reference, so a value left by a previous load is at most a wrong number of seconds. It has
        // had no reader since HasRecentActivity went; this whole class is a debug probe that
        // survived, and deleting it is the owner's call. On patch-statics.allow for that reason.
        private static float _lastActionTime = -100f;

        [HarmonyPatch(typeof(PlatformUserButtonEntry), "HandleClicked")]
        [HarmonyPostfix]
        private static void PlatformUserButtonEntryHandleClickedPostfix(PlatformUserButtonEntry __instance)
        {
            _lastActionTime = Time.realtimeSinceStartup;
            SocAccessMod.Instance?.LogInfo(
                "PlatformUserMenuDebug action clicked: type="
                + GetActionType(__instance)
                + ", label=\""
                + GetLabel(__instance)
                + "\"");
        }

        private static string GetLabel(PlatformUserButtonEntry entry)
        {
            UITextMesh label = entry != null && ButtonLabelField != null
                ? ButtonLabelField.GetValue(entry) as UITextMesh
                : null;
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(label));
        }

        private static string GetActionType(PlatformUserButtonEntry entry)
        {
            object value = entry != null && UserButtonTypeField != null ? UserButtonTypeField.GetValue(entry) : null;
            return value != null ? value.ToString() : string.Empty;
        }
    }
}
