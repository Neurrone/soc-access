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
    internal sealed class WorldChoiceMenuScreen : Screen
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldChoiceMenuInstaller), "Container");

        private readonly WorldChoiceMenuAdapter _adapter;

        public WorldChoiceMenuScreen(WorldChoiceMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            WorldChoiceMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldChoiceMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldChoiceMenu menu = TryResolveWorldChoiceMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldChoiceMenuAdapter adapter = new WorldChoiceMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new WorldChoiceMenuScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(WorldChoiceMenuAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            ContainerWidget root = new ContainerWidget(
                "world-choice-menu",
                string.IsNullOrWhiteSpace(title) ? ModText.Get(ModStrings.Screens.WorldChoiceMenu) : title);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "world-choice-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(TroopHudMenu.Build(
                "world-choice-troops",
                GameText.Get("Commanders/Tooltip/Troops", string.Empty),
                adapter.Troops,
                () => true));

            root.AddChild(new TextWidget(
                "world-choice-body",
                () => adapter.Body,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildChoiceMenu(adapter));

            root.AddChild(new ButtonWidget(
                "world-choice-confirm",
                () => adapter.ConfirmLabel,
                adapter.ActivateConfirm,
                adapter.HideNativeTooltip,
                adapter.IsConfirmEnabled));

            root.AddChild(new ButtonWidget(
                "world-choice-cancel",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildChoiceMenu(WorldChoiceMenuAdapter adapter)
        {
            IReadOnlyList<WorldChoiceMenuAdapter.ChoiceItem> choices = adapter.GetChoices();
            MenuWidget menu = new MenuWidget("world-choice-choices", BuildChoiceMenuLabel(choices));
            for (int i = 0; i < choices.Count; i++)
            {
                WorldChoiceMenuAdapter.ChoiceItem choice = choices[i];
                menu.AddItem(new MenuItemWidget(
                    BuildChoiceId(choice, i),
                    () => choice.Label,
                    () => choice.IsEnabled ? string.Empty : ModText.Get(ModStrings.UI.StatusDisabled),
                    () => false,
                    choice.OnFocus,
                    choice.IsVisible,
                    choice.Tooltip));
            }

            return menu;
        }

        private static string BuildChoiceMenuLabel(IReadOnlyList<WorldChoiceMenuAdapter.ChoiceItem> choices)
        {
            bool hasRewards = false;
            bool hasPenalties = false;
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                WorldChoiceMenuAdapter.ChoiceItem choice = choices[i];
                if (choice == null)
                {
                    continue;
                }

                if (choice.IsPenalty)
                {
                    hasPenalties = true;
                }
                else
                {
                    hasRewards = true;
                }
            }

            if (hasRewards && hasPenalties)
            {
                return ModText.Get(ModStrings.Screens.Choices);
            }

            return hasPenalties
                ? ModText.Get(ModStrings.Screens.Penalties)
                : ModText.Get(ModStrings.Screens.Rewards);
        }

        private static string BuildChoiceId(WorldChoiceMenuAdapter.ChoiceItem choice, int index)
        {
            string prefix = choice != null && choice.IsPenalty ? "penalty" : "reward";
            return prefix + "-" + index;
        }

        private static WorldChoiceMenu TryResolveWorldChoiceMenu(WorldChoiceMenuInstaller installer)
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
                return container.Resolve<WorldChoiceMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldChoiceMenuInstaller installer)
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
