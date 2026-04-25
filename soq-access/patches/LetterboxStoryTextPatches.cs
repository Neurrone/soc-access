using HarmonyLib;
using SongsOfConquest.Client.Adventure;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class LetterboxStoryTextPatches
    {
        [HarmonyPatch(typeof(LetterboxStoryText), "Show")]
        [HarmonyPostfix]
        private static void LetterboxStoryTextShowPostfix(LetterboxStoryText __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextShown(__instance);
        }

        [HarmonyPatch(typeof(LetterboxStoryText), "ForceHide")]
        [HarmonyPostfix]
        private static void LetterboxStoryTextForceHidePostfix(LetterboxStoryText __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextHidden(__instance);
        }
    }
}
