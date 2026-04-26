using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(AdventureViewInstaller), "Container");
        private static string _lastProbeDiagnostic;

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            AdventureMapScreen screen = FindActiveAdventureMapScreen();
            if (screen != null)
            {
                screens.Add(screen);
            }
        }

        public static AdventureMapScreen FindActiveAdventureMapScreen()
        {
            return FindActiveAdventureMap();
        }

        private static AdventureMapScreen FindActiveAdventureMap()
        {
            AdventureViewInstaller[] installers = Resources.FindObjectsOfTypeAll<AdventureViewInstaller>();
            if (installers.Length == 0)
            {
                LogProbeDiagnostic("Adventure map probe found no AdventureViewInstaller instances");
                return null;
            }

            int liveInstallers = 0;
            for (int i = 0; i < installers.Length; i++)
            {
                AdventureViewInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                liveInstallers++;
                DiContainer container = GetContainer(installer);
                IClientAdventureFacade facade = TryResolve<IClientAdventureFacade>(container);
                ISelectionHandler selectionHandler = TryResolve<ISelectionHandler>(container);
                IFogManager fogManager = TryResolve<IFogManager>(container);
                IGrid grid = TryResolve<IGrid>(container);
                ICameraController cameraController = TryResolve<ICameraController>(container);
                IAdventureTooltipManager tooltipManager = TryResolve<IAdventureTooltipManager>(container);
                ILocalizationHandler localizationHandler = TryResolve<ILocalizationHandler>(container);
                ICartographyVisualManifest cartographyVisualManifest = TryResolve<ICartographyVisualManifest>(container);
                IHumanAdventureController humanAdventureController = TryResolve<IHumanAdventureController>(container);
                IHumanAdventureControllerFacade humanAdventureControllerFacade = TryResolve<IHumanAdventureControllerFacade>(container);
                IInputManager inputManager = TryResolve<IInputManager>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                AdventureMapAdapter adapter = new AdventureMapAdapter(
                    installer,
                    container,
                    facade,
                    selectionHandler,
                    fogManager,
                    grid,
                    cameraController,
                    cartographyConverter,
                    tooltipManager,
                    localizationHandler,
                    cartographyVisualManifest,
                    humanAdventureController,
                    humanAdventureControllerFacade,
                    inputManager);
                if (adapter.IsPresent())
                {
                    LogProbeDiagnostic("Adventure map probe found ready adventure map");
                    AdventureMapEventListener eventListener = new AdventureMapEventListener(
                        facade,
                        selectionHandler,
                        humanAdventureControllerFacade,
                        localizationHandler);
                    return new AdventureMapScreen(adapter, eventListener);
                }

                LogProbeDiagnostic("Adventure map probe found installer but adapter is not ready: " + adapter.GetReadinessDiagnostic());
            }

            if (liveInstallers == 0)
            {
                LogProbeDiagnostic("Adventure map probe found " + installers.Length + " installer instances but none in a loaded scene");
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(AdventureViewInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(AdventureViewInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
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

        private static object TryResolveByTypeName(DiContainer container, string typeName)
        {
            if (container == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            try
            {
                return container.Resolve(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void LogProbeDiagnostic(string message)
        {
            if (message == _lastProbeDiagnostic)
            {
                return;
            }

            _lastProbeDiagnostic = message;
            SoqAccessPlugin.Instance?.LogInfo(message);
        }
    }
}
