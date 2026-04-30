using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class StoryTextPatches
    {
        [HarmonyPatch(typeof(StoryText), "Show")]
        [HarmonyPostfix]
        private static void StoryTextShowPostfix(StoryText __instance)
        {
            StoryMapSuppression.Activate(__instance);
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForStoryTextReady(__instance));
            }
        }

        [HarmonyPatch(typeof(StoryText), "ForceHide")]
        [HarmonyPostfix]
        private static void StoryTextForceHidePostfix(StoryText __instance)
        {
            StoryMapSuppression.Clear(__instance);
            SoqAccessPlugin.Instance?.ScreenDetector?.OnStoryTextHidden(__instance);
        }

        private static IEnumerator WaitForStoryTextReady(StoryText storyText)
        {
            int frames = 0;
            while (storyText != null && frames < 600)
            {
                StoryTextAdapter adapter = new StoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnStoryTextShown(storyText);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
