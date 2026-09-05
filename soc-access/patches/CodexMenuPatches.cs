using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CodexMenuPatches
    {
        [HarmonyPatch(typeof(CodexMenu), "Show")]
        [HarmonyPostfix]
        private static void CodexMenuShowPostfix(CodexMenu __instance)
        {
            if (__instance == null || !new CodexMenuAdapter(__instance).IsPresent())
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnCodexReady(__instance);
        }

        [HarmonyPatch(typeof(CodexMenu), "Hide")]
        [HarmonyPrefix]
        private static void CodexMenuHidePrefix(CodexMenu __instance, out bool __state)
        {
            __state = __instance != null && new CodexMenuAdapter(__instance).IsPresent();
        }

        [HarmonyPatch(typeof(CodexMenu), "Hide")]
        [HarmonyPostfix]
        private static void CodexMenuHidePostfix(CodexMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnCodexClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(CodexMenu), "SetActiveTab")]
        [HarmonyPostfix]
        private static void CodexMenuSetActiveTabPostfix(CodexMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCodexTabChanged(__instance);
        }

        [HarmonyPatch(typeof(CodexMenu), "HandleContentButtonClicked")]
        [HarmonyPostfix]
        private static void CodexMenuHandleContentButtonClickedPostfix(CodexMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCodexArticleChanged(__instance);
        }
    }
}
