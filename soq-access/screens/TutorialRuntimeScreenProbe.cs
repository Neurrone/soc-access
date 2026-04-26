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
                    screens.Add(new TutorialSlideshowScreen(slideshowAdapter));
                    return;
                }

                TutorialSimpleAdapter simpleAdapter = new TutorialSimpleAdapter(menu);
                if (simpleAdapter.IsPresent())
                {
                    screens.Add(new TutorialSimpleScreen(simpleAdapter));
                    return;
                }
            }
        }
    }
}
