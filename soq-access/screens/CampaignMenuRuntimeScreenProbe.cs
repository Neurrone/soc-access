using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CampaignMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            CampaignMenuAdapter adapter = FindActiveCampaignMenu();
            if (adapter != null)
            {
                screens.Add(new CampaignMenuScreen(adapter));
            }
        }

        private static CampaignMenuAdapter FindActiveCampaignMenu()
        {
            CampaignMenu[] campaignMenus = Resources.FindObjectsOfTypeAll<CampaignMenu>();
            for (int i = 0; i < campaignMenus.Length; i++)
            {
                CampaignMenu campaignMenu = campaignMenus[i];
                if (!IsLiveSceneCampaignMenu(campaignMenu))
                {
                    continue;
                }

                CampaignMenuAdapter adapter = new CampaignMenuAdapter(campaignMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCampaignMenu(CampaignMenu campaignMenu)
        {
            if (campaignMenu == null)
            {
                return false;
            }

            GameObject gameObject = campaignMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
