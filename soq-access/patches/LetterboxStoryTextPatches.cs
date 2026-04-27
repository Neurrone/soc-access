using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class LetterboxStoryTextPatches
    {
        [HarmonyPatch(typeof(LetterboxStoryText), "Show")]
        [HarmonyPostfix]
        private static void LetterboxStoryTextShowPostfix(LetterboxStoryText __instance)
        {
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForLetterboxStoryTextReady(__instance));
            }
        }

        [HarmonyPatch(typeof(LetterboxStoryText), "ForceHide")]
        [HarmonyPostfix]
        private static void LetterboxStoryTextForceHidePostfix(LetterboxStoryText __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextHidden(__instance);
        }

        private static IEnumerator WaitForLetterboxStoryTextReady(LetterboxStoryText storyText)
        {
            int frames = 0;
            while (storyText != null && frames < 600)
            {
                LetterboxStoryTextAdapter adapter = new LetterboxStoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextShown(storyText);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
