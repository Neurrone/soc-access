using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TutorialRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            Screen screen = FindActiveTutorialScreen();
            if (screen != null)
            {
                screens.Add(screen);
            }
        }

        public static Screen FindActiveTutorialScreen()
        {
            TutorialMenu[] menus = Resources.FindObjectsOfTypeAll<TutorialMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TutorialMenu menu = menus[i];
                if (menu == null || menu.gameObject == null || !menu.gameObject.scene.IsValid() || !menu.gameObject.scene.isLoaded)
                {
                    continue;
                }

                TutorialSlideshowAdapter slideshowAdapter = new TutorialSlideshowAdapter(menu);
                if (slideshowAdapter.IsPresent())
                {
                    return new TutorialSlideshowScreen(slideshowAdapter);
                }

                TutorialSimpleAdapter simpleAdapter = new TutorialSimpleAdapter(menu);
                if (simpleAdapter.IsPresent())
                {
                    return new TutorialSimpleScreen(simpleAdapter);
                }
            }

            return null;
        }
    }
}
