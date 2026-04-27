using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class StoryTextRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            StoryTextAdapter adapter = FindActiveStoryText();
            if (adapter != null)
            {
                screens.Add(new StoryTextScreen(adapter));
            }
        }

        private static StoryTextAdapter FindActiveStoryText()
        {
            StoryText[] storyTexts = Resources.FindObjectsOfTypeAll<StoryText>();
            for (int i = 0; i < storyTexts.Length; i++)
            {
                StoryText storyText = storyTexts[i];
                if (!IsLiveSceneStoryText(storyText))
                {
                    continue;
                }

                StoryTextAdapter adapter = new StoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneStoryText(StoryText storyText)
        {
            if (storyText == null)
            {
                return false;
            }

            GameObject gameObject = storyText.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

    }
}
