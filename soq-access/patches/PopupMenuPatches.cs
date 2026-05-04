using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PopupMenuPatches
    {
        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");

        [HarmonyPatch(typeof(PopupMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void PopupMenuOnOpenedPostfix(PopupMenu __instance)
        {
            PopupMenu.Settings settings = null;
            if (__instance != null)
            {
                settings = SettingsRef(__instance);
            }

            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogReady(__instance, settings);
        }

        [HarmonyPatch(typeof(PopupMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void PopupMenuOnClosedPostfix(PopupMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogClosed(__instance);
        }
    }
}
