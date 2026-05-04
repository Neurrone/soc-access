using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CampaignMapSelectRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CampaignMapSelectMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            CampaignMapSelectAdapter adapter = FindActiveCampaignMapSelect(null);
            if (adapter != null)
            {
                screens.Add(new CampaignMapSelectScreen(adapter));
            }
        }

        private static CampaignMapSelectAdapter FindActiveCampaignMapSelect(CampaignMapSelectedInformationView targetInformationView)
        {
            CampaignMapSelectMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CampaignMapSelectMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CampaignMapSelectMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                CampaignMapSelectMenu menu = TryResolve<CampaignMapSelectMenu>(installer);
                CampaignMapSelectedInformationView informationView = TryResolve<CampaignMapSelectedInformationView>(installer);
                if (menu == null || informationView == null)
                {
                    continue;
                }

                if (targetInformationView != null && !ReferenceEquals(targetInformationView, informationView))
                {
                    continue;
                }

                CampaignMapSelectAdapter adapter = new CampaignMapSelectAdapter(menu, informationView);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(CampaignMapSelectMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(CampaignMapSelectMenuInstaller installer) where T : class
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
