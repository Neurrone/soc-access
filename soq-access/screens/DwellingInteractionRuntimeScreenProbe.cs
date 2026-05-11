using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DwellingInteractionRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            DwellingInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<DwellingInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menus[i]);
                if (adapter.IsUpgradePresent())
                {
                    screens.Add(new UpgradeTroopsScreen(new DwellingTroopManagementHostAdapter(adapter)));
                    continue;
                }

                if (adapter.IsDraftPresent())
                {
                    screens.Add(new DraftTroopsScreen(new DwellingTroopManagementHostAdapter(adapter)));
                }
            }
        }
    }
}
