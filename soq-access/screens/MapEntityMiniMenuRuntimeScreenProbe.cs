using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MapEntityMiniMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            MapEntityMiniMenu[] menus = Resources.FindObjectsOfTypeAll<MapEntityMiniMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new MapEntityMiniMenuScreen(adapter));
                    return;
                }
            }
        }
    }
}
