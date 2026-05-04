using System.Collections;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class LetterboxStoryTextPatches
    {
        private static readonly FieldInfo LoreAsyncField = AccessTools.Field(typeof(LetterboxStoryText), "_loreAsync");

        [HarmonyPatch(typeof(LetterboxStoryText), "Show")]
        [HarmonyPostfix]
        private static void LetterboxStoryTextShowPostfix(LetterboxStoryText __instance)
        {
            StoryMapSuppression.Activate(__instance);
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForLetterboxStoryTextReady(__instance));
            }
        }

        [HarmonyPatch(typeof(LetterboxStoryText), "ForceHide", new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void LetterboxStoryTextForceHidePrefix(LetterboxStoryText __instance, out bool __state)
        {
            __state = __instance != null && LoreAsyncField != null && LoreAsyncField.GetValue(__instance) != null;
        }

        [HarmonyPatch(typeof(LetterboxStoryText), "ForceHide", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void LetterboxStoryTextForceHidePostfix(LetterboxStoryText __instance, bool __state)
        {
            StoryMapSuppression.Clear(__instance);
            if (__state)
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextClosed(__instance);
            }
        }

        private static IEnumerator WaitForLetterboxStoryTextReady(LetterboxStoryText storyText)
        {
            int frames = 0;
            while (storyText != null && frames < 600)
            {
                LetterboxStoryTextAdapter adapter = new LetterboxStoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnLetterboxStoryTextReady(storyText);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
