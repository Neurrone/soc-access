using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LevelUpScreen : Screen
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CommanderLevelUpMenuInstaller), "Container");

        private readonly LevelUpMenuAdapter _adapter;

        public LevelUpScreen(LevelUpMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommanderLevelUpMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CommanderLevelUpMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CommanderLevelUpMenu menu = TryResolveLevelUpMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                LevelUpMenuAdapter adapter = new LevelUpMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new LevelUpScreen(adapter);
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

        private static ContainerWidget BuildRoot(LevelUpMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("level-up-screen", "Level up");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "level-up-title",
                adapter.GetTitle,
                null,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "level-up-commander",
                adapter.GetCommanderIdentity,
                null,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildStatsMenu(adapter));
            root.AddChild(BuildSkillMenu(adapter));

            root.AddChild(new TextWidget(
                "level-up-max-level",
                adapter.GetMaxLevelMessage,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.IsMaxLevelMessageVisible));

            root.AddChild(new ButtonWidget(
                "level-up-close",
                "Close",
                adapter.Close,
                null,
                () => true));

            return root;
        }

        private static MenuWidget BuildStatsMenu(LevelUpMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("level-up-stats", "Stats");
            IReadOnlyList<LevelUpMenuAdapter.StatItem> stats = adapter.GetStats();
            for (int i = 0; i < stats.Count; i++)
            {
                LevelUpMenuAdapter.StatItem stat = stats[i];
                menu.AddItem(new MenuItemWidget(
                    stat.Id,
                    () => stat.Label,
                    null,
                    () => false,
                    null,
                    () => true,
                    stat.Tooltip));
            }

            if (stats.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "level-up-stats-none",
                    () => "Unavailable",
                    null,
                    () => false,
                    null,
                    () => true));
            }

            return menu;
        }

        private static MenuWidget BuildSkillMenu(LevelUpMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("level-up-skills", "Skills");
            IReadOnlyList<LevelUpMenuAdapter.SkillChoice> choices = adapter.GetSkillChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                LevelUpMenuAdapter.SkillChoice choice = choices[i];
                menu.AddItem(new MenuItemWidget(
                    choice.Id,
                    () => choice.Label,
                    () => choice.Status,
                    choice.Activate,
                    choice.OnFocus,
                    choice.IsVisible,
                    onUnfocus: choice.OnUnfocus));
            }

            if (choices.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "level-up-skills-none",
                    () => "No skill choices",
                    null,
                    () => false,
                    null,
                    () => true));
            }

            return menu;
        }

        private static CommanderLevelUpMenu TryResolveLevelUpMenu(CommanderLevelUpMenuInstaller installer)
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
                return container.Resolve<CommanderLevelUpMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(CommanderLevelUpMenuInstaller installer)
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
