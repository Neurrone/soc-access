using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PurchaseWielderRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PurchaseWielderMenu[] menus = Resources.FindObjectsOfTypeAll<PurchaseWielderMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    screens.Add(new PurchaseWielderScreen(adapter));
                    return;
                }
            }
        }
    }
}
