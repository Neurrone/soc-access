using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TradingMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(TradingMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            TradingMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<TradingMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                TradingMenu menu = TryResolveTradingMenu(installers[i]);
                TradingMenuAdapter adapter = new TradingMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.LogInfo("Trading menu probe found ready trading menu");
                    screens.Add(new TradingScreen(adapter));
                    return;
                }
            }
        }

        private static TradingMenu TryResolveTradingMenu(TradingMenuInstaller installer)
        {
            if (installer == null || installer.gameObject == null || !installer.gameObject.scene.IsValid() || !installer.gameObject.scene.isLoaded)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty != null
                ? InstallerContainerProperty.GetValue(installer, null) as DiContainer
                : null;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<TradingMenu>();
            }
            catch
            {
                return null;
            }
        }
    }
}
