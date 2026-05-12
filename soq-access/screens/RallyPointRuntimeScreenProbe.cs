using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class RallyPointRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            RallyPointInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<RallyPointInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                RallyPointInteractionMenuAdapter adapter = new RallyPointInteractionMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new RallyPointScreen(adapter));
                }
            }
        }
    }
}
