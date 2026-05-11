using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MarketplaceRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            MarketplaceMenu[] menus = Resources.FindObjectsOfTypeAll<MarketplaceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MarketplaceMenuAdapter adapter = new MarketplaceMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new MarketplaceScreen(adapter));
                    return;
                }
            }
        }
    }
}
