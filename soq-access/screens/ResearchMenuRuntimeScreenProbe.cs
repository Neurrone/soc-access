using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ResearchMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            ResearchMenu[] menus = Resources.FindObjectsOfTypeAll<ResearchMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ResearchMenuAdapter adapter = new ResearchMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new ResearchScreen(adapter));
                    return;
                }
            }
        }
    }
}
