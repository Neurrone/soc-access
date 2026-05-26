using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PlatformUserMenuPatches
    {
        private static readonly FieldInfo UserButtonsField = AccessTools.Field(typeof(PlatformUserMenu), "_userButtons");
        private static readonly FieldInfo ButtonLabelField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_buttonLabel");
        private static readonly FieldInfo UserButtonTypeField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_userButtonType");
        private static float _lastActionTime = -100f;

        public static bool HasRecentActivity
        {
            get { return Time.realtimeSinceStartup - _lastActionTime <= 2f; }
        }

        [HarmonyPatch(typeof(PlatformUserMenu), "Show")]
        [HarmonyPostfix]
        private static void PlatformUserMenuShowPostfix(PlatformUserMenu __instance)
        {
            SocAccessPlugin.Instance?.StartCoroutine(WaitForPlatformUserMenuReady(__instance));
        }

        [HarmonyPatch(typeof(PlatformUserMenu), "Hide")]
        [HarmonyPostfix]
        private static void PlatformUserMenuHidePostfix(PlatformUserMenu __instance)
        {
            SocAccessPlugin.Instance?.LogInfo("PlatformUserMenuDebug hidden");
            SocAccessPlugin.Instance?.ScreenDetector?.OnPlatformUserMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(PlatformUserMenu), "OnDestroy")]
        [HarmonyPostfix]
        private static void PlatformUserMenuOnDestroyPostfix(PlatformUserMenu __instance)
        {
            SocAccessPlugin.Instance?.LogInfo("PlatformUserMenuDebug hidden");
            SocAccessPlugin.Instance?.ScreenDetector?.OnPlatformUserMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(PlatformUserButtonEntry), "HandleClicked")]
        [HarmonyPostfix]
        private static void PlatformUserButtonEntryHandleClickedPostfix(PlatformUserButtonEntry __instance)
        {
            _lastActionTime = Time.realtimeSinceStartup;
            SocAccessPlugin.Instance?.LogInfo(
                "PlatformUserMenuDebug action clicked: type="
                + GetActionType(__instance)
                + ", label=\""
                + GetLabel(__instance)
                + "\"");
        }

        private static IEnumerator WaitForPlatformUserMenuReady(PlatformUserMenu menu)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (menu != null && Time.realtimeSinceStartup < deadline)
            {
                PlatformUserMenuAdapter adapter = PlatformUserMenuScreen.FindActiveMenu(menu);
                if (adapter != null)
                {
                    SocAccessPlugin.Instance?.LogInfo(
                        "PlatformUserMenuDebug shown: present="
                        + adapter.IsPresent()
                        + ", buttonCount="
                        + adapter.GetActions().Count
                        + ", labels=["
                        + string.Join(", ", GetLabels(menu).ToArray())
                        + "]");
                    SocAccessPlugin.Instance?.ScreenDetector?.OnPlatformUserMenuReady(menu);
                    yield break;
                }

                yield return null;
            }
        }

        private static List<string> GetLabels(PlatformUserMenu menu)
        {
            List<string> labels = new List<string>();
            List<PlatformUserButtonEntry> entries = menu != null && UserButtonsField != null
                ? UserButtonsField.GetValue(menu) as List<PlatformUserButtonEntry>
                : null;
            if (entries == null)
            {
                return labels;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                string label = GetLabel(entries[i]);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }

            return labels;
        }

        private static string GetLabel(PlatformUserButtonEntry entry)
        {
            UITextMesh label = entry != null && ButtonLabelField != null
                ? ButtonLabelField.GetValue(entry) as UITextMesh
                : null;
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(label));
        }

        private static string GetActionType(PlatformUserButtonEntry entry)
        {
            object value = entry != null && UserButtonTypeField != null ? UserButtonTypeField.GetValue(entry) : null;
            return value != null ? value.ToString() : string.Empty;
        }
    }
}
