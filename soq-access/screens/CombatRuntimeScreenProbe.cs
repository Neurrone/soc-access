using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Utilities;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(BattleSceneInstaller), "Container");
        private static string _lastProbeDiagnostic;

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            CombatScreen screen = FindActiveCombatScreen();
            if (screen != null)
            {
                screens.Add(screen);
            }
        }

        public static CombatScreen FindActiveCombatScreen()
        {
            BattleSceneInstaller[] installers = Resources.FindObjectsOfTypeAll<BattleSceneInstaller>();
            if (installers.Length == 0)
            {
                LogProbeDiagnostic("Combat probe found no BattleSceneInstaller instances");
                return null;
            }

            int liveInstallers = 0;
            for (int i = 0; i < installers.Length; i++)
            {
                BattleSceneInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                liveInstallers++;
                DiContainer container = GetContainer(installer);
                IClientBattleFacade facade = TryResolve<IClientBattleFacade>(container);
                IBattleCursorManager cursorManager = TryResolve<IBattleCursorManager>(container);
                IBattleGridManager gridManager = TryResolve<IBattleGridManager>(container);
                IBattlePathManager pathManager = TryResolve<IBattlePathManager>(container);
                IBattleHighlightManager highlightManager = TryResolve<IBattleHighlightManager>(container);
                IBattleAttackPreviewHandler attackPreviewHandler = TryResolve<IBattleAttackPreviewHandler>(container);
                IBattleTooltipUtility tooltipUtility = TryResolve<IBattleTooltipUtility>(container);
                IInputManager inputManager = TryResolve<IInputManager>(container);
                ILocalizationHandler localization = TryResolve<ILocalizationHandler>(container);
                ICameraLookup cameraLookup = TryResolve<ICameraLookup>(container);
                IHumanBattleControllerFacade humanBattleController = TryResolve<IHumanBattleControllerFacade>(container);
                MouseKeyboardHumanBattleControllerModule mouseKeyboardInputModule = TryResolve<MouseKeyboardHumanBattleControllerModule>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                CombatAdapter adapter = new CombatAdapter(
                    installer,
                    container,
                    facade,
                    cursorManager,
                    gridManager,
                    pathManager,
                    highlightManager,
                    attackPreviewHandler,
                    tooltipUtility,
                    inputManager,
                    localization,
                    cameraLookup,
                    cartographyConverter,
                    humanBattleController,
                    mouseKeyboardInputModule);
                if (adapter.IsPresent())
                {
                    CombatEventNarrator.SyncCurrentTurnTroop(adapter);
                    LogProbeDiagnostic("Combat probe found ready battle");
                    return new CombatScreen(adapter);
                }

                LogProbeDiagnostic("Combat probe found installer but adapter is not ready: " + adapter.GetReadinessDiagnostic());
            }

            if (liveInstallers == 0)
            {
                LogProbeDiagnostic("Combat probe found " + installers.Length + " installer instances but none in a loaded scene");
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(BattleSceneInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(BattleSceneInstaller installer)
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
