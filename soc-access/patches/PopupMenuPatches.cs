using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PopupMenuPatches
    {
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
            NotifyReady(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void PopupMenuOnClosedPostfix(PopupMenu __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnPopupMenuClosed(__instance);
        }

        private static void NotifyReady(PopupMenu popup)
        {
            PopupMenu.Settings settings = null;
            if (popup != null)
            {
                settings = SettingsRef(popup);
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnPopupMenuReady(popup, settings);
        }
    }
}
