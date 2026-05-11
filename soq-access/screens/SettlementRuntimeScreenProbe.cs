using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SettlementRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            TownInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<TownInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    screens.Add(new DraftTroopsScreen(new SettlementTroopManagementHostAdapter(adapter)));
                    continue;
                }

                if (adapter.IsUpgradePresent())
                {
                    screens.Add(new UpgradeTroopsScreen(new SettlementTroopManagementHostAdapter(adapter)));
                    continue;
                }

                if (adapter.IsTopLevelPresent())
                {
                    screens.Add(new SettlementScreen(adapter));
                }
            }
        }
    }
}
