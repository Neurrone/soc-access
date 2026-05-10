using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class OwnedEntitiesRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            KingdomEntityOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomEntityOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomEntityOverviewAdapter adapter = new KingdomEntityOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new OwnedEntitiesScreen(adapter));
                    return;
                }
            }
        }
    }
}
