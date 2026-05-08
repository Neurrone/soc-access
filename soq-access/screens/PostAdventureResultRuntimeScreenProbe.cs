using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostAdventureResultRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PostAdventureMenu[] menus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PostAdventureMenu menu = menus[i];
                PostAdventureResultAdapter adapter = new PostAdventureResultAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new PostAdventureResultScreen(adapter));
                }
            }
        }
    }
}
