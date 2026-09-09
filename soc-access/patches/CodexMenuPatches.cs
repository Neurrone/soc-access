using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CodexMenuPatches
    {
        /// <summary>The one place the window redraws its body: the game calls DrawContent from
        /// here, for a click on an article and for the first article of a tab it has just switched
        /// to. The adapter re-reads the body when this has run and not before.</summary>
        [HarmonyPatch(typeof(CodexMenu), "HandleContentButtonClicked")]
        [HarmonyPostfix]
        private static void CodexMenuContentDrawnPostfix()
        {
            CodexMenuAdapter.ContentGeneration++;
        }
    }
}
