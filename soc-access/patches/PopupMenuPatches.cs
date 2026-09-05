using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PopupMenuPatches
    {
        private const int MaxReadyWaitFrames = 30;

        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");

        [HarmonyPatch(typeof(PopupMenu), "ShowMessage", new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(string),
            typeof(string)
        })]
        [HarmonyPostfix]
        private static void PopupMenuShowMessagePostfix(PopupMenu __instance)
        {
            NotifyReady(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "AskQuestion", new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(string),
            typeof(string),
            typeof(TroopReference[])
        })]
        [HarmonyPostfix]
        private static void PopupMenuAskQuestionPostfix(PopupMenu __instance)
        {
            NotifyReady(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "AskForInput", new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(InputFieldContentType),
            typeof(int),
            typeof(int),
            typeof(string),
            typeof(string)
        })]
        [HarmonyPostfix]
        private static void PopupMenuAskForInputPostfix(PopupMenu __instance)
        {
            if (PlatformUserMenuPatches.HasRecentActivity)
            {
                LogPlatformUserInputPopup(__instance);
            }

            NotifyReady(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void PopupMenuOnClosedPostfix(PopupMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnPopupMenuClosed(__instance);
        }

        private static void NotifyReady(PopupMenu popup)
        {
            PopupMenu.Settings settings = null;
            if (popup != null)
            {
                settings = SettingsRef(popup);
            }

            if (settings == null)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnPopupMenuReady(popup, settings);
                return;
            }

            if (TryNotifyReady(popup, settings))
            {
                return;
            }

            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(NotifyReadyWhenPresent(popup, settings));
            }
        }

        private static IEnumerator NotifyReadyWhenPresent(PopupMenu popup, PopupMenu.Settings settings)
        {
            for (int i = 0; i < MaxReadyWaitFrames; i++)
            {
                yield return null;

                if (TryNotifyReady(popup, settings))
                {
                    yield break;
                }
            }
        }

        private static bool TryNotifyReady(PopupMenu popup, PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                return false;
            }

            PopupMenuAdapter adapter = new PopupMenuAdapter(popup, settings);
            if (!adapter.IsPresent())
            {
                return false;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPopupMenuReady(popup, settings);
            return true;
        }

        private static void LogPlatformUserInputPopup(PopupMenu popup)
        {
            PopupMenu.Settings settings = popup != null ? SettingsRef(popup) : null;
            SocAccessMod.Instance?.LogInfo(
                "PlatformUserMenuDebug input popup opened: header=\""
                + GetText(settings != null ? settings.HeaderText : null)
                + "\", message=\""
                + GetText(settings != null ? settings.MessageText : null)
                + "\", positive=\""
                + GetButtonText(settings != null ? settings.PositiveButton : null)
                + "\", negative=\""
                + GetButtonText(settings != null ? settings.NegativeButton : null)
                + "\"");
        }

        private static string GetText(IUITextMesh text)
        {
            return text != null ? SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text)) : string.Empty;
        }

        private static string GetButtonText(IUIButton button)
        {
            return SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }
    }
}
