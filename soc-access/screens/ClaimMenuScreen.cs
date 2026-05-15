using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ClaimMenuScreen : Screen
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(ClaimMenuInstaller), "Container");

        private readonly ClaimMenuAdapter _adapter;

        public ClaimMenuScreen(ClaimMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ClaimMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<ClaimMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                ClaimMenu menu = TryResolveClaimMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                ClaimMenuAdapter adapter = new ClaimMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new ClaimMenuScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public bool Matches(ClaimMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
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

        private static ContainerWidget BuildRoot(ClaimMenuAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            ContainerWidget root = new ContainerWidget("claim-menu", title);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "claim-menu-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "claim-menu-body",
                () => adapter.Body,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildChoiceMenu(adapter));
            return root;
        }

        private static MenuWidget BuildChoiceMenu(ClaimMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("claim-menu-choices", "Choices");
            System.Collections.Generic.IReadOnlyList<ClaimMenuAdapter.ChoiceItem> choices = adapter.GetChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem choice = choices[i];
                menu.AddItem(new MenuItemWidget(
                    "claim-menu-choice-" + choice.IdSuffix,
                    choice.GetLabel,
                    null,
                    choice.Activate,
                    () => choice.Focus(),
                    () => true,
                    (Tooltip)null,
                    null,
                    () => choice.IsEnabled));
            }

            return menu;
        }

        private static ClaimMenu TryResolveClaimMenu(ClaimMenuInstaller installer)
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
                return container.Resolve<IClaimMenu>() as ClaimMenu;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(ClaimMenuInstaller installer)
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
