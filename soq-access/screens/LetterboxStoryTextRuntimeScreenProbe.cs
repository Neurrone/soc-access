using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LetterboxStoryTextRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            LetterboxStoryTextAdapter adapter = FindActiveLetterboxStoryText();
            if (adapter != null)
            {
                screens.Add(new LetterboxStoryTextScreen(adapter));
            }
        }

        private static LetterboxStoryTextAdapter FindActiveLetterboxStoryText()
        {
            LetterboxStoryText[] storyTexts = Resources.FindObjectsOfTypeAll<LetterboxStoryText>();
            for (int i = 0; i < storyTexts.Length; i++)
            {
                LetterboxStoryText storyText = storyTexts[i];
                if (!IsLiveSceneStoryText(storyText))
                {
                    continue;
                }

                LetterboxStoryTextAdapter adapter = new LetterboxStoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneStoryText(LetterboxStoryText storyText)
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
