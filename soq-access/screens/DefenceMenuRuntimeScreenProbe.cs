using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DefenceMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            DefenceMenu[] menus = Resources.FindObjectsOfTypeAll<DefenceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    screens.Add(new DraftTroopsScreen(new DefenceTroopManagementHostAdapter(adapter)));
                    continue;
                }

                if (adapter.IsUpgradePresent())
                {
                    screens.Add(new UpgradeTroopsScreen(new DefenceTroopManagementHostAdapter(adapter)));
                    continue;
                }

                if (adapter.IsTopLevelPresent())
                {
                    screens.Add(new DefenceMenuScreen(adapter));
                }
            }
        }
    }
}
