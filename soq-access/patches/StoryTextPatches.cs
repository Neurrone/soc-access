using System.Collections;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class StoryTextPatches
    {
        private static readonly FieldInfo LoreAsyncField = AccessTools.Field(typeof(StoryText), "_loreAsync");

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
        [HarmonyPrefix]
        private static void StoryTextForceHidePrefix(StoryText __instance, out bool __state)
        {
            __state = __instance != null && LoreAsyncField != null && LoreAsyncField.GetValue(__instance) != null;
        }

        [HarmonyPatch(typeof(StoryText), "ForceHide")]
        [HarmonyPostfix]
        private static void StoryTextForceHidePostfix(StoryText __instance, bool __state)
        {
            StoryMapSuppression.Clear(__instance);
            if (__state)
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.OnStoryTextClosed(__instance);
            }
        }

        private static IEnumerator WaitForStoryTextReady(StoryText storyText)
        {
            int frames = 0;
            while (storyText != null && frames < 600)
            {
                StoryTextAdapter adapter = new StoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnStoryTextReady(storyText);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
