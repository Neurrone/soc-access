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
        private static float _lastActionTime = -100f;

        public static bool HasRecentActivity
        {
            get { return Time.realtimeSinceStartup - _lastActionTime <= 2f; }
        }

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
