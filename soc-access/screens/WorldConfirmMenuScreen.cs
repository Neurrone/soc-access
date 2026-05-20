using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class WorldConfirmMenuScreen : Screen
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldConfirmMenuInstaller), "Container");

        private readonly WorldConfirmMenuAdapter _adapter;

        public WorldConfirmMenuScreen(WorldConfirmMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            WorldConfirmMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldConfirmMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldConfirmMenu menu = TryResolveWorldConfirmMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldConfirmMenuAdapter adapter = new WorldConfirmMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new WorldConfirmMenuScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.ActivateCancel();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(WorldConfirmMenuAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            ContainerWidget root = new ContainerWidget(
                "world-confirm-menu",
                string.IsNullOrWhiteSpace(title) ? ModText.Get(ModStrings.Screens.WorldConfirmationMenu) : title);

            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "world-confirm-title",
                () => adapter.Title,
                adapter.ClearNativeSelection,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "world-confirm-body",
                () => adapter.Body,
                adapter.ClearNativeSelection,
                includeParentLabelInAnnouncement: false));

            AddCosts(root, adapter);

            root.AddChild(new ButtonWidget(
                "world-confirm-confirm",
                () => adapter.ConfirmLabel,
                adapter.ActivateConfirm,
                adapter.ClearNativeSelection,
                adapter.IsConfirmEnabled));

            root.AddChild(new ButtonWidget(
                "world-confirm-cancel",
                () => adapter.CancelLabel,
                adapter.ActivateCancel,
                adapter.ClearNativeSelection,
                () => true));

            return root;
        }

        private static void AddCosts(ContainerWidget root, WorldConfirmMenuAdapter adapter)
        {
            IReadOnlyList<string> costs = adapter.GetCostLabels();
            for (int i = 0; i < costs.Count; i++)
            {
                int capturedIndex = i;
                root.AddChild(new TextWidget(
                    "world-confirm-cost-" + i,
                    () =>
                    {
                        IReadOnlyList<string> latestCosts = adapter.GetCostLabels();
                        return capturedIndex >= 0 && capturedIndex < latestCosts.Count
                            ? latestCosts[capturedIndex]
                            : string.Empty;
                    },
                    adapter.ClearNativeSelection,
                    includeParentLabelInAnnouncement: false));
            }
        }

        private static WorldConfirmMenu TryResolveWorldConfirmMenu(WorldConfirmMenuInstaller installer)
        {
            if (!IsLiveSceneInstaller(installer) || InstallerContainerProperty == null)
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
                return container.Resolve<WorldConfirmMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldConfirmMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
