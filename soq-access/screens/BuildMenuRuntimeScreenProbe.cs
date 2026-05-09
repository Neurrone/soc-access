using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class BuildMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            BuildMenu[] menus = Resources.FindObjectsOfTypeAll<BuildMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                BuildMenuAdapter adapter = new BuildMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new BuildMenuScreen(adapter));
                    return;
                }
            }
        }
    }
}
