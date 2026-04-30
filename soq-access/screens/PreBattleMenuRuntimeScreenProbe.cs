using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PreBattleMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PreBattleMenu[] menus = Resources.FindObjectsOfTypeAll<PreBattleMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PreBattleMenu menu = menus[i];
                if (menu == null || menu.gameObject == null || !menu.gameObject.scene.IsValid() || !menu.gameObject.scene.isLoaded)
                {
                    continue;
                }

                PreBattleMenuAdapter adapter = new PreBattleMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new PreBattleMenuScreen(adapter));
                }
            }
        }
    }
}
