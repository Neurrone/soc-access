using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TroopOverviewRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            KingdomTroopOverviewMenu[] menus = Resources.FindObjectsOfTypeAll<KingdomTroopOverviewMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                KingdomTroopOverviewAdapter adapter = new KingdomTroopOverviewAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new TroopOverviewScreen(adapter));
                    return;
                }
            }
        }
    }
}
