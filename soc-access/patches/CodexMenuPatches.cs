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

        /// <summary>The one place the window redraws its body: the game calls DrawContent from
        /// here, for a click on an article and for the first article of a tab it has just switched
        /// to. The adapter re-reads the body when this has run and not before.</summary>
        [HarmonyPatch(typeof(CodexMenu), "HandleContentButtonClicked")]
        [HarmonyPostfix]
        private static void CodexMenuContentDrawnPostfix()
        {
            CodexMenuAdapter.ContentGeneration++;
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
    }
}
